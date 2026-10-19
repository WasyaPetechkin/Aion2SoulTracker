using AionMeter.Core.Protocol;

namespace SoulTracker.Core;

public sealed record PetLevel(int PetId, int AppearanceId, int Level);
public sealed record PetSnapshot(long TimeMs, int EquippedId, IReadOnlyList<PetLevel> Levels,
    IReadOnlyDictionary<int, int> Souls, int ParsedBytes, int TrailingBytes);
public sealed record SoulGain(int PetId, int Amount);

/// <summary>
/// Layout observed in the user's Global EU capture, 2026-10-15. Only the two verified messages are decoded.
/// Counts are LEB128; IDs, levels and counters are little-endian u32. Trailing snapshot metadata is not decoded.
/// </summary>
public static class PetProtocol
{
    public const ushort SnapshotOpcode = 0x0090;
    public const ushort GainOpcode = 0x0D90;
    private const int MaxEntries = 4096;

    public static bool TrySnapshot(long timeMs, ReadOnlySpan<byte> body, out PetSnapshot? snapshot)
    {
        snapshot = null;
        var o = 0;
        if (!Wire.TryU32(body, ref o, out var equipped) || !ValidId(equipped, true) ||
            !Wire.TryVarint(body, ref o, out var levelCount) || levelCount > MaxEntries ||
            (long)o + levelCount * 12L > body.Length) return false;
        var levels = new List<PetLevel>();
        var ids = new HashSet<int>();
        for (var i = 0; i < levelCount; i++)
        {
            if (!Wire.TryU32(body, ref o, out var id) || !ValidId(id) || !ids.Add((int)id) ||
                !Wire.TryU32(body, ref o, out var appearance) || !ValidId(appearance) ||
                !Wire.TryU32(body, ref o, out var level) || level is < 1 or > 3) return false;
            levels.Add(new((int)id, (int)appearance, (int)level));
        }
        if (!Wire.TryVarint(body, ref o, out var soulCount) || soulCount > MaxEntries ||
            (long)o + soulCount * 8L > body.Length) return false;
        var souls = new Dictionary<int, int>();
        for (var i = 0; i < soulCount; i++)
        {
            if (!Wire.TryU32(body, ref o, out var id) || !ValidId(id) ||
                !Wire.TryU32(body, ref o, out var amount) || amount > 1_000_000 ||
                !souls.TryAdd((int)id, (int)amount)) return false;
        }
        snapshot = new(timeMs, (int)equipped, levels, souls, o, body.Length - o);
        return true;
    }

    public static bool TryGains(ReadOnlySpan<byte> body, out IReadOnlyList<SoulGain> gains)
    {
        gains = [];
        var o = 0;
        if (!Wire.TryVarint(body, ref o, out var n) || n is 0 or > MaxEntries ||
            (long)o + n * 8L != body.Length) return false;
        var parsed = new List<SoulGain>();
        for (var i = 0; i < n; i++)
        {
            if (!Wire.TryU32(body, ref o, out var id) || !ValidId(id) ||
                !Wire.TryU32(body, ref o, out var amount) || amount is 0 or > 1_000_000) return false;
            parsed.Add(new((int)id, (int)amount));
        }
        gains = parsed;
        return true;
    }

    private static bool ValidId(uint id, bool zero = false) => (zero || id > 0) && id <= 10_000_000;
}
