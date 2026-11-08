using System.Buffers;
using K4os.Compression.LZ4;

namespace AionMeter.Core.Protocol;

public delegate void PacketHandler(ushort opcode, ReadOnlySpan<byte> body);

/// <summary>
/// Splits one server→client byte stream into game packets.
/// <code>
/// frame  = varint len, [0xF0..0xFE], payload        (frame size = len + varintBytes − 4)
/// payload = opcode(2) body | FF FF u32 rawSize lz4-block   (the block holds more frames, possibly nested)
/// </code>
/// 0x00 bytes between frames are padding. After lost data the decoder hunts for the next heartbeat frame
/// (<c>0E 00 36</c>, ~19 per second) instead of guessing at varints, which would produce phantom damage.
/// </summary>
public sealed class FrameDecoder
{
    public const int MaxFrame = 65_535;
    public const int MaxUnbundledWait = 16_384;
    public const int MaxBundle = 8 * 1024 * 1024;
    // Per outer bundle, including every nested expansion. A per-node limit alone
    // permits a small compressed tree to cause unbounded aggregate work.
    public const int MaxExpandedBundleBytes = 32 * 1024 * 1024;
    public const int MaxBundleFrames = 65_536;
    public const int MaxBundleNodes = 1024;
    private const int MaxBundleDepth = 4;
    private const int MaxHuntBytes = 256 * 1024;

    private static readonly byte[][] HeartbeatSignatures = [[0x0E, 0x00, 0x36], [0x06, 0x00, 0x36]];

    private readonly PacketHandler _onPacket;
    private byte[] _buf = new byte[128 * 1024];
    private int _len;
    private bool _hunting;
    private int _huntedBytes;

    /// <param name="startsAligned">True when the stream was captured from its first byte (SYN seen). Otherwise the
    /// first segment may start mid-frame, so the decoder begins by hunting for a heartbeat.</param>
    public FrameDecoder(PacketHandler onPacket, bool startsAligned = true)
    {
        _onPacket = onPacket;
        _hunting = !startsAligned;
    }

    public event Action<string>? DataLost;
    public long Frames { get; private set; }
    public long Bundles { get; private set; }
    public long BadBundles { get; private set; }
    public long Resyncs { get; private set; }
    public long DroppedBytes { get; private set; }

    /// <summary>Call when the TCP layer skipped missing bytes: the next byte is not a frame boundary.</summary>
    public void Reset()
    {
        _len = 0;
        _hunting = true;
        _huntedBytes = 0;
    }

    public void Feed(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;
        if (_len + data.Length > _buf.Length)
        {
            var bigger = new byte[Math.Max(_buf.Length * 2, _len + data.Length)];
            Buffer.BlockCopy(_buf, 0, bigger, 0, _len);
            _buf = bigger;
        }
        data.CopyTo(_buf.AsSpan(_len));
        _len += data.Length;
        Drain();
    }

    private void Drain()
    {
        var pos = 0;
        while (pos < _len)
        {
            var span = _buf.AsSpan(pos, _len - pos);

            if (_hunting)
            {
                var hb = FindHeartbeat(span);
                if (hb < 0)
                {
                    var keep = Math.Min(span.Length, 2); // a signature may straddle two segments
                    var skip = span.Length - keep;
                    pos += skip;
                    DroppedBytes += skip;
                    _huntedBytes += skip;
                    if (_huntedBytes > MaxHuntBytes) _hunting = false; // signature changed? fall back to byte resync
                    break;
                }
                pos += hb;
                DroppedBytes += hb;
                _hunting = false;
                continue;
            }

            if (span[0] == 0x00)
            {
                pos++;
                continue;
            }

            if (!Wire.TryVarint(span, 0, out var len, out var vlen))
            {
                if (span.Length < 5) break; // need more bytes
                pos += Resync();
                continue;
            }

            var size = (long)len + vlen - 4;
            if (len < 6 || size > MaxFrame)
            {
                pos += Resync();
                continue;
            }

            if (size > span.Length)
            {
                if (size > MaxUnbundledWait && !IsBundle(span, vlen))
                {
                    pos += Resync();
                    continue;
                }
                break; // wait for the rest of the frame
            }

            HandleFrame(span[..(int)size], vlen);
            pos += (int)size;
        }

        if (pos > 0)
        {
            _len -= pos;
            if (_len > 0) Buffer.BlockCopy(_buf, pos, _buf, 0, _len);
        }
    }

    /// <summary>Returns how many bytes to skip. Normally switches into heartbeat hunting.</summary>
    private int Resync()
    {
        Resyncs++;
        DataLost?.Invoke("Frame alignment lost");
        DroppedBytes++;
        if (_huntedBytes <= MaxHuntBytes)
        {
            _hunting = true;
            _huntedBytes = 0;
        }
        return 1;
    }

    private static int FindHeartbeat(ReadOnlySpan<byte> span)
    {
        var best = -1;
        foreach (var sig in HeartbeatSignatures)
        {
            var i = span.IndexOf(sig);
            if (i >= 0 && (best < 0 || i < best)) best = i;
        }
        return best;
    }

    private static bool IsBundle(ReadOnlySpan<byte> frame, int vlen)
    {
        var o = vlen;
        if (o < frame.Length && frame[o] is >= 0xF0 and <= 0xFE) o++;
        return o + 1 < frame.Length && frame[o] == 0xFF && frame[o + 1] == 0xFF;
    }

    private void HandleFrame(ReadOnlySpan<byte> frame, int vlen)
    {
        Frames++;
        var o = vlen;
        if (o < frame.Length && frame[o] is >= 0xF0 and <= 0xFE) o++;
        if (o + 2 > frame.Length)
        {
            DataLost?.Invoke("Incomplete frame opcode");
            return;
        }

        if (frame[o] == 0xFF && frame[o + 1] == 0xFF)
        {
            var batch = new BundleBatch();
            try
            {
                if (!DecodeBundle(frame[(o + 2)..], 0, batch))
                {
                    BadBundles++;
                    DataLost?.Invoke(batch.Error);
                    return;
                }
                // Do not expose a valid prefix (or later siblings) of an invalid
                // compressed tree. Every pooled buffer stays alive until dispatch ends.
                foreach (var packet in batch.Packets)
                    _onPacket(packet.Opcode, packet.Body.Span);
            }
            finally
            {
                foreach (var buffer in batch.Buffers) ArrayPool<byte>.Shared.Return(buffer);
            }
            return;
        }

        var opcode = (ushort)(frame[o] << 8 | frame[o + 1]);
        _onPacket(opcode, frame[(o + 2)..]);
    }

    private readonly record struct PendingPacket(ushort Opcode, ReadOnlyMemory<byte> Body);
    private sealed class BundleBatch
    {
        public List<byte[]> Buffers { get; } = [];
        public List<PendingPacket> Packets { get; } = [];
        public int ExpandedBytes, Frames, Nodes;
        public string Error { get; private set; } = "Invalid compressed bundle";
        public bool Fail(string reason) { Error = reason; return false; }
    }

    private bool DecodeBundle(ReadOnlySpan<byte> body, int depth, BundleBatch batch)
    {
        Bundles++;
        if (depth > MaxBundleDepth || ++batch.Nodes > MaxBundleNodes)
            return batch.Fail("Compressed bundle nesting limit exceeded");
        var offset = 0;
        if (!Wire.TryU32(body, ref offset, out var rawSize) || rawSize == 0 || rawSize > MaxBundle)
            return batch.Fail("Invalid compressed bundle size");
        if (rawSize > MaxExpandedBundleBytes - batch.ExpandedBytes)
            return batch.Fail("Compressed bundle expansion limit exceeded");
        batch.ExpandedBytes += (int)rawSize;
        var target = ArrayPool<byte>.Shared.Rent((int)rawSize);
        batch.Buffers.Add(target);
        var count = LZ4Codec.Decode(body[offset..], target.AsSpan(0, (int)rawSize));
        if (count != rawSize) return batch.Fail("Invalid compressed bundle");
        return WalkInner(target.AsMemory(0, count), depth + 1, batch);
    }

    /// <summary>Validate and stage a whole decompressed tree; no resynchronization inside a bundle.</summary>
    private bool WalkInner(ReadOnlyMemory<byte> data, int depth, BundleBatch batch)
    {
        var pos = 0;
        while (pos < data.Length)
        {
            if (data.Span[pos] == 0x00)
            {
                pos++;
                continue;
            }
            var span = data.Span[pos..];
            if (!Wire.TryVarint(span, 0, out var len, out var vlen)) return batch.Fail("Invalid inner frame length");
            var size = (long)len + vlen - 4;
            if (len < 6 || size > span.Length) return batch.Fail("Truncated inner frame");
            if (++batch.Frames > MaxBundleFrames) return batch.Fail("Compressed bundle frame limit exceeded");
            Frames++;
            var frame = span[..(int)size];
            var offset = vlen;
            if (frame[offset] is >= 0xF0 and <= 0xFE) offset++;
            if (offset + 2 > frame.Length) return batch.Fail("Incomplete inner frame opcode");
            var opcode = (ushort)(frame[offset] << 8 | frame[offset + 1]);
            if (opcode == 0xFFFF)
            {
                if (!DecodeBundle(frame[(offset + 2)..], depth, batch)) return false;
            }
            else batch.Packets.Add(new(opcode, data.Slice(pos + offset + 2, (int)size - offset - 2)));
            pos += (int)size;
        }
        return true;
    }
}
