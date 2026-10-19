namespace SoulTracker.Core;

public sealed record PetDefinition(int Id, string Name, string Genus, string Source, string? NameRu = null);
public sealed record PetProgress(int Id, string Name, string Genus, string Source, int Level, int Souls, string? NameRu = null)
{
    public bool IsRegular => Genus is "Cogni" or "Fera" or "Natura" or "Varian";
    public bool Completed => Level == 3;
    public int Target => Level switch { 0 => 5, 1 => 25, 2 => 75, _ => 0 };
    public bool AwaitingLevelConfirmation => IsRegular && !Completed && Souls >= Target;
    // Equivalent progress assumes ordinary pet unlock through souls: 5 + 25 + 75.
    // Pets granted directly by a chest need not have consumed those first five souls.
    public int? EquivalentTotal => !IsRegular ? null : Level switch
    {
        0 => Math.Min(Souls, 5), 1 => 5 + Math.Min(Souls,25),
        2 => 30 + Math.Min(Souls,75), _ => 105
    };
    public int? Remaining => !IsRegular || AwaitingLevelConfirmation ? null : 105 - EquivalentTotal;
    public string Stage => Completed ? "MAX" : !IsRegular ? "—" : $"{Souls} / {Target}";
    public string RemainingText => Remaining?.ToString() ?? (AwaitingLevelConfirmation ? "Нужна синхронизация" : "—");
    public string LevelText => Level == 0 ? "Не получен" : $"Ур. {Level}";
}

public sealed record CollectionView(long SnapshotTimeMs, long LastTimeMs, bool HasSnapshot,
    bool NeedsResync, string Character, int? ServerId, string Source,
    IReadOnlyList<PetProgress> Pets, IReadOnlyList<string> History);

public sealed class CollectionState(IReadOnlyList<PetDefinition> catalog)
{
    private readonly object _gate = new();
    private Dictionary<int,int> _levels = new();
    private Dictionary<int,int> _souls = new();
    private readonly List<string> _history = new();
    private long _snapshotTime;
    private long _lastTime;
    private long _lastIdentityTime;
    private bool _hasSnapshot;
    private bool _needsResync;
    private string _character = "";
    private int? _server;
    private string _source = "Запись";

    public bool Process(long timeMs, ushort op, ReadOnlySpan<byte> body)
    {
        lock (_gate)
        {
            if (op == PetProtocol.SnapshotOpcode)
            {
                if (!PetProtocol.TrySnapshot(timeMs, body, out var s)) return false;
                _levels = s!.Levels.ToDictionary(x => x.PetId, x => x.Level);
                _souls = new(s.Souls);
                _snapshotTime = _lastTime = timeMs;
                _hasSnapshot = true;
                _needsResync = false;
                AddHistory(timeMs, $"Коллекция: {s.Levels.Count} питомца, {s.Souls.Count} счётчиков душ");
                return true;
            }
            if (op != PetProtocol.GainOpcode || !PetProtocol.TryGains(body, out var gains)) return false;
            // A gain without a snapshot cannot reveal how many souls the character already had.
            if (!_hasSnapshot || _needsResync || timeMs < _snapshotTime) return false;
            foreach (var g in gains)
            {
                var before = _souls.GetValueOrDefault(g.PetId);
                var after = checked(before + g.Amount);
                _souls[g.PetId] = after;
                var name = catalog.FirstOrDefault(x => x.Id == g.PetId)?.Name ?? $"Pet {g.PetId}";
                AddHistory(timeMs, $"{name}: {before} → {after} (+{g.Amount})");
                AdvanceOrdinaryPet(g.PetId,timeMs);
            }
            _lastTime = timeMs;
            return true;
        }
    }

    // Regular pets advance automatically at 5, then 25, then 75 current-step souls.
    // This is a game-rule transition on a trusted gain, not an invented level-up opcode.
    private void AdvanceOrdinaryPet(int id,long timeMs)
    {
        var definition=catalog.FirstOrDefault(x=>x.Id==id);
        if(definition?.Genus is not ("Cogni" or "Fera" or "Natura" or "Varian")) return;
        var level=_levels.GetValueOrDefault(id);
        var souls=_souls.GetValueOrDefault(id);
        while(level<3)
        {
            var threshold=level switch {0=>5,1=>25,_=>75};
            if(souls<threshold) break;
            souls-=threshold;
            var before=level++;
            AddHistory(timeMs,$"{definition.Name}: уровень {before} → {level}"+(level==3 ? " · MAX" : ""));
        }
        _levels[id]=level;
        _souls[id]=souls;
    }

    public bool Restore(CollectionView saved)
    {
        lock(_gate)
        {
            // JSON can supply null for non-nullable record properties. Validate everything before
            // replacing any live data, including timestamps later used by cache migration history.
            if(saved is null || !saved.HasSnapshot || saved.SnapshotTimeMs<=0 || saved.LastTimeMs<saved.SnapshotTimeMs ||
               saved.LastTimeMs>DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() ||
               saved.Pets is null || saved.History is null || saved.Character is null ||
               saved.Pets.Count>4096 || saved.Pets.Any(x=>x is null || x.Id is <=0 or >10_000_000 || x.Level is <0 or >3 || x.Souls is <0 or >1_000_000) ||
               saved.Pets.Select(x=>x.Id).Distinct().Count()!=saved.Pets.Count || saved.History.Any(x=>x is null)) return false;
            _levels=saved.Pets.Where(x=>x.Level>0).ToDictionary(x=>x.Id,x=>x.Level);
            _souls=saved.Pets.ToDictionary(x=>x.Id,x=>x.Souls);
            _snapshotTime=saved.SnapshotTimeMs;
            _lastTime=saved.LastTimeMs;
            _character=saved.Character;
            _server=saved.ServerId;
            _lastIdentityTime=saved.LastTimeMs;
            _hasSnapshot=true;
            _needsResync=true; // A saved cache does not establish continuity during app downtime.
            _source="Сохранённая запись; ожидается синхронизация при входе";
            _history.Clear();
            _history.AddRange(saved.History.TakeLast(100));
            // Upgrade old-version caches that stopped at e.g. level 2, 75/75.
            foreach(var id in _souls.Keys.ToArray()) AdvanceOrdinaryPet(id,_lastTime);
            return true;
        }
    }

    public void SetSource(string source) { lock (_gate) _source = source; }
    public void MarkUncertain(string reason)
    {
        lock (_gate)
        {
            _needsResync = true;
            AddHistory(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), reason);
        }
    }
    public void Identify(long timeMs, string name, int serverId)
    {
        lock (_gate)
        {
            // A placeholder name or an unvalidated identity tail is not a character change.
            // The parser reports an unknown server as 0; retain a known server for the same name.
            if (string.IsNullOrWhiteSpace(name)) return;
            int? server=serverId>0 ? serverId : _character==name ? _server : null;
            if (_character.Length > 0 && (_character != name || (_server.HasValue && server.HasValue && _server != server)))
            {
                // Do not attribute a previous character's cached counts to a new character.
                // On EU, the fresh snapshot can precede SelfInfo by several seconds.
                if (_snapshotTime <= _lastIdentityTime || timeMs - _snapshotTime > 30_000)
                {
                    _levels.Clear(); _souls.Clear(); _hasSnapshot = false; _needsResync = true;
                }
            }
            _character = name;
            _server = server;
            _lastIdentityTime = timeMs;
        }
    }
    public CollectionView View()
    {
        lock (_gate)
        {
            var all = catalog.Select(x => new PetProgress(x.Id, x.Name, x.Genus, x.Source,
                _levels.GetValueOrDefault(x.Id), _souls.GetValueOrDefault(x.Id), x.NameRu)).ToList();
            foreach (var id in _levels.Keys.Union(_souls.Keys).Except(catalog.Select(x => x.Id)))
                all.Add(new(id, $"Pet {id}", "Unknown", "", _levels.GetValueOrDefault(id), _souls.GetValueOrDefault(id)));
            return new(_snapshotTime,_lastTime,_hasSnapshot,_needsResync,_character,_server,_source,all,_history.ToArray());
        }
    }
    private void AddHistory(long ms, string text)
    {
        _history.Add($"{DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime():HH:mm:ss}  {text}");
        if (_history.Count > 100) _history.RemoveAt(0);
    }
}
