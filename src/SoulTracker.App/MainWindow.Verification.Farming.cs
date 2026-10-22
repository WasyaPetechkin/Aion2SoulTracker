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
        _session.ProcessLivePetMessage(sample.LastTimeMs+199,PetProtocol.SnapshotOpcode,Convert.FromHexString("0000000001600400006004000002000000016004000046000000"));
        Check("snapshot_does_not_queue_gains",_session.DrainReceivedSouls().Count==0);
        _session.ProcessLivePetMessage(sample.LastTimeMs+200,PetProtocol.GainOpcode,Convert.FromHexString("016004000001000000"));Refresh(true);
        Check("open_world_gain_auto_pins",_preferences.HudTargets.Contains(1120));
        _preferences.HudTargets=[];SetVerificationWorld(9999);
        _session.ProcessLivePetMessage(sample.LastTimeMs+201,PetProtocol.GainOpcode,Convert.FromHexString("016004000001000000"));Refresh(true);
        Check("dungeon_gain_does_not_auto_pin",_preferences.HudTargets.Length==0);
        AutoTrackSouls.IsChecked=false;SetVerificationWorld(1110);
        FocusMode.IsChecked=true;Refresh(true);
        Check("focus_local_targets",_hud.Targets.Items.Cast<HudWindow.TargetRow>().Select(x=>x.Id).SequenceEqual(_spawns.Focus(_session.State.View(),EnabledCategories(),1110).Visible.Select(x=>x.Id)));
        SetVerificationWorld(1010);Refresh(true);
        Check("focus_excludes_opposite_only_pet",!_spawns.Focus(_session.State.View(),EnabledCategories(),1010).Visible.Any(x=>x.Id==1106));
        Check("focus_changes_with_player_world",_hud.Targets.Items.Cast<HudWindow.TargetRow>().Select(x=>x.Id).SequenceEqual(_spawns.Focus(_session.State.View(),EnabledCategories(),1010).Visible.Select(x=>x.Id)));
        SetVerificationWorld(9999);Refresh(true);Check("unknown_location_has_no_focus_targets",_hud.Targets.Items.Count==0 && _map.SpawnCount==0);
        FocusMode.IsChecked=false;SetVerificationWorld(1110);_preferences.MapFollowPlayer=true;
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
        TerritoryFilter.SelectedValue="unknown";
        Check("catalog_unknown_territory",Pets.Items.Count>0 && Pets.Items.Cast<PetRow>().All(p=>p.Territory.Contains("неизвестна")));
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
    }
}
