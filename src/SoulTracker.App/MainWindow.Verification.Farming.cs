using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SoulTracker.Core;
namespace SoulTracker.App;
public partial class MainWindow
{
    private void SetVerificationWorld(int world)
    {
        var body=new byte[28];BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4),(uint)world);
        _session!.Navigation.Process(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),0x2136,body);
    }
    private void VerifyFarmingControls(string directory,Dictionary<string,bool> checks,CollectionView sample)
    {
        void Check(string name,bool value)=>checks.Add(name,value);
        var original=_preferences.HudTargets.ToArray();
        _preferences.HudTargets=[];_preferences.MapPetId=0;_preferences.MapAllIncomplete=false;
        SetVerificationWorld(1110);_preferences.MapFollowPlayer=true;
        _session!.State.Restore(sample with {LastTimeMs=sample.LastTimeMs+100});Refresh(true);
        var all=_map.SpawnCount;
        Check("automatic_map_ignores_legacy_disabled_setting",all>0 && _map.AllIncomplete.IsChecked==true);
        _preferences.HudTargets=[1120];Refresh(true);
        Check("automatic_map_narrows_with_pin",_map.SpawnCount>0 && _map.SpawnCount<all && _map.AllIncomplete.IsChecked==false);
        var unowned=sample with {LastTimeMs=sample.LastTimeMs+101, Pets=sample.Pets.Select(p=>p.Id==1120 ? p with {Level=0, Souls=0} : p).ToArray()};
        _session.State.Restore(unowned);OwnedOnly.IsChecked=true;Search.Text="no-match";Fera.IsChecked=false;Refresh(true);
        Check("pinned_unowned_visible_outside_filters",Pets.Items.Count==1 && ((PetRow)Pets.Items[0]).Id==1120);
        _hud.UpdateLayout();var item=_hud.Targets.ItemContainerGenerator.ContainerFromIndex(0);
        var remove=FindVisual<Button>(item)!;remove.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check("hud_row_cross_untracks",_preferences.HudTargets.Length==0 && _hud.Targets.Items.Count==0);
        Search.Clear();OwnedOnly.IsChecked=false;Fera.IsChecked=true;Refresh(true);
        Check("hud_last_unpin_restores_map",_map.SpawnCount==all);
        var numeric=sample with {LastTimeMs=sample.LastTimeMs+102,Pets=sample.Pets.Select(p=>p with {Level=2, Souls=p.Id==1120 ? 2 : p.Id==1106 ? 19 : 20}).ToArray()};
        _session.State.Restore(numeric);
        var column=Pets.Columns.Single(c=>c.SortMemberPath=="SoulsSort");column.SortDirection=null;
        SortCatalog(Pets,new DataGridSortingEventArgs(column));
        var values=Pets.Items.Cast<PetRow>().Select(x=>x.SoulsSort).ToArray();
        Check("souls_sort_numeric_ascending",values.SequenceEqual(values.Order()) && values[0]==2);
        SortCatalog(Pets,new DataGridSortingEventArgs(column));Refresh(true);
        values=Pets.Items.Cast<PetRow>().Select(x=>x.SoulsSort).ToArray();
        Check("souls_sort_descending_survives_refresh",values.SequenceEqual(values.OrderDescending()));
        _preferences.HudTargets=[1120];Refresh(true);
        Check("pins_first_even_during_sort",((PetRow)Pets.Items[0]).Id==1120);
        Check("hud_portrait_and_territory",((HudWindow.TargetRow)_hud.Targets.Items[0]) is {Icon:not null,Territory.Length:>0});
        _preferences.HudTargets=[];Refresh(true);_map.Map.Fit();Render(_map,Path.Combine(directory,"map-landmarks.png"));
        Check("altgard_kibelisks",_map.Map.KibeliskCount==61);
        var hover=_map.Map.SpawnScreenPoints.First();_map.Map.HoverAt(hover);
        Check("spawn_hover_opens_tooltip",_map.Map.HoverTipVisible);_map.Map.Locked=true;
        Check("locking_closes_tooltip",!_map.Map.HoverTipVisible);_map.Map.Locked=false;
        foreach(var world in _spawns.Worlds)
        {
            _map.WorldPicker.SelectedValue=world.Key;
            Check("kibelisks_world_"+world.Key,_map.Map.KibeliskCount==(world.Value.Group=="abyss" ? 0 : world.Key is 1010 or 1110 ? 61 : 20));
        }
        // Only genuinely received deltas can auto-pin; a login snapshot never queues targets.
        _preferences.HudTargets=[];AutoTrackSouls.IsChecked=true;SetVerificationWorld(1110);
        _session.State.Restore(numeric);Refresh(true);Check("restore_does_not_auto_pin",_preferences.HudTargets.Length==0);
        _session.ProcessSyntheticPetMessage(sample.LastTimeMs+199,PetProtocol.SnapshotOpcode,Convert.FromHexString("0000000001600400006004000002000000016004000046000000"));
        Check("snapshot_does_not_queue_gains",_session.DrainReceivedSouls().Count==0);
        _session.ProcessSyntheticPetMessage(sample.LastTimeMs+200,PetProtocol.GainOpcode,Convert.FromHexString("016004000001000000"));Refresh(true);
        Check("open_world_gain_auto_pins",_preferences.HudTargets.Contains(1120));
        _preferences.HudTargets=[];SetVerificationWorld(600032);
        _session.ProcessSyntheticPetMessage(sample.LastTimeMs+201,PetProtocol.GainOpcode,Convert.FromHexString("016004000001000000"));Refresh(true);
        Check("dungeon_gain_auto_pins",_preferences.HudTargets.Contains(1120));
        AutoTrackSouls.IsChecked=false;SetVerificationWorld(1110);
        SetVerificationWorld(1110);_preferences.MapFollowPlayer=true;
        _preferences.HudTargets=original;_catalogSort="RemainingSort";_catalogDirection=ListSortDirection.Ascending;
        foreach(var c in Pets.Columns)c.SortDirection=null;
        _session.State.Restore(sample with {LastTimeMs=sample.LastTimeMs+300});Refresh(true);
        var savedPins=_preferences.HudTargets;_preferences.HudTargets=[];
        HideCompleted.IsChecked=false;
        var completed=sample with {LastTimeMs=sample.LastTimeMs+301,Pets=sample.Pets.Select(p=>p with {Level=3,Souls=0}).ToArray()};
        _session.State.Restore(completed);Refresh(true);
        Check("completed_pet_territory_visible",Pets.Items.Cast<PetRow>().Any(p=>p.Id==1106 && p.Territory.Contains("Асмо")));
        TerritoryFilter.SelectedValue="asmodians";
        Check("catalog_territory_filter",Pets.Items.Count>0 && Pets.Items.Cast<PetRow>().All(p=>_spawns.MatchesTerritory(p.Id,"asmodians")));
        Check("completed_catalog_does_not_fill_hud",_hud.Targets.Items.Count==0);
        TerritoryFilter.SelectedValue="dungeons";
        Check("catalog_dungeons",Pets.Items.Count>0 && Pets.Items.Cast<PetRow>().All(p=>_spawns.MatchesTerritory(p.Id,"dungeons")));
        Check("completed_saraswati_draupnir",Pets.Items.Cast<PetRow>().Any(p=>p.Id==1215 && p.Territory=="Драупнир" && p.BossLabel==""));
        Render(this,Path.Combine(directory,"catalog-dungeons.png"));
        TerritoryFilter.SelectedValue="worldboss";
        Check("catalog_world_bosses",Pets.Items.Count==5 && Pets.Items.Cast<PetRow>().All(p=>p.BossLabel.Contains("Мировой босс")));
        Render(this,Path.Combine(directory,"catalog-world-bosses.png"));
        TerritoryFilter.SelectedValue="chests";
        Check("catalog_chest_sources",Pets.Items.Count==23 && Pets.Items.Cast<PetRow>().All(p=>p.Territory=="Сундуки" && p.Locations.Contains("сундуков")));
        Render(this,Path.Combine(directory,"catalog-chests.png"));
        TerritoryFilter.SelectedValue="elyos";LanguagePicker.SelectedIndex=1;
        Check("territory_filter_survives_language",Equals(TerritoryFilter.SelectedValue,"elyos") && TerritoryLabel.Text=="Territory");
        LanguagePicker.SelectedIndex=0;
        _preferences.Save(Path.Combine(directory,"territory-preferences.json"));
        Check("territory_filter_saved",AppPreferences.Load(Path.Combine(directory,"territory-preferences.json")).CatalogTerritory=="elyos");
        TerritoryFilter.SelectedValue="all";Search.Text="1106";Refresh(true);
        Check("completed_pet_lookup_by_id",Pets.Items.Count==1 && ((PetRow)Pets.Items[0]).Id==1106);
        Render(this,Path.Combine(directory,"completed-territory.png"));
        Search.Clear();HideCompleted.IsChecked=true;_preferences.HudTargets=savedPins;
        _session.State.Restore(sample with {LastTimeMs=sample.LastTimeMs+302});Refresh(true);
        Render(_hud,Path.Combine(directory,"hud-portraits.png"));Render(this,Path.Combine(directory,"collection-farming.png"));
        _preferences.HudTargets=[1215];Refresh(true);
        Check("dungeon_target_empty_map_explains_source",_map.SpawnCount==0 && _map.EmptyText.Text.Contains("Драупнир"));
        Render(_map,Path.Combine(directory,"map-dungeon-target.png"));
        _preferences.HudTargets=[1215,1206,1198];Refresh(true);
        Check("hud_dungeon_and_boss_labels",_hud.Targets.Items.Cast<HudWindow.TargetRow>().Any(p=>p.Id==1215 && p.Territory=="Драупнир") &&
            _hud.Targets.Items.Cast<HudWindow.TargetRow>().Any(p=>p.Id==1206 && p.BossLabel.Contains("Мировой босс")));
        Render(_hud,Path.Combine(directory,"hud-farming-sources.png"));
        LanguagePicker.SelectedIndex=1;Render(_hud,Path.Combine(directory,"hud-farming-sources-en.png"));
        Check("hud_sources_english",_hud.Targets.Items.Cast<HudWindow.TargetRow>().Any(p=>p.Id==1215 && p.Territory=="Draupnir"));
        LanguagePicker.SelectedIndex=0;_preferences.HudTargets=savedPins;Refresh(true);
    }
}
