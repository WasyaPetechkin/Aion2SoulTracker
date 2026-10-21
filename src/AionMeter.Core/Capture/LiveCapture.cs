using System.Net;
using System.Net.NetworkInformation;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace AionMeter.Core.Capture;

public sealed class LiveCaptureOptions
{
    /// <summary>Npcap device name to force (e.g. a VPN adapter). Null = pick automatically from the game's connections.</summary>
    public string? DeviceName { get; set; }

    /// <summary>When set, every captured game packet is also written to a .pcap file in this folder.</summary>
    public string? RecordDirectory { get; set; }
}

/// <summary>
/// Passive capture of the AION 2 connection through Npcap. It looks up the game's own TCP connections in the
/// Windows TCP table, opens only the adapter that carries them (the loopback adapter when a ping accelerator relays
/// the game locally) and filters to those servers. Nothing is ever sent or injected.
/// </summary>
public sealed class LiveCapture : IEventSource
{
    private readonly LiveCaptureOptions _options;
    private readonly PacketPipeline _pipeline;
    private readonly GameConnectionGate _connections;
    private readonly object _gate = new();
    private readonly object _pipelineGate = new();
    private CancellationTokenSource? _cts;
    private Task? _monitor;
    private readonly SemaphoreSlim _refreshRequested=new(0,1);
    private void NetworkChanged(object? sender,EventArgs e) {try{_refreshRequested.Release();}catch(SemaphoreFullException){}}
    private sealed class Binding(LibPcapLiveDevice device,string filter,long needed)
    {public LibPcapLiveDevice Device=device;public string Filter=filter;public long Needed=needed;public CaptureFileWriterDevice? Writer;public PacketArrivalEventHandler? Handler;}
    private readonly Dictionary<string,Binding> _devices=[];
    private long _closedReceived,_closedDropped;
    private long _captureErrors;
    public long CaptureErrors=>Interlocked.Read(ref _captureErrors);
    public string[] ActiveAdapters {get {lock(_gate)return _devices.Values.Select(x=>Describe(x.Device)).ToArray();}}

    private long _packets;
    private long _events;
    private CaptureStatus _status = new(CaptureState.Stopped, "Capture stopped");

    public LiveCapture(GameData data, LiveCaptureOptions options)
    {
        _options = options;
        _pipeline = new PacketPipeline(data, OnEvent) { LockThreshold = 3 };
        _connections=new(s=>_pipeline.OnSegment(s.TimeMs,s.Key,s.Seq,s.Syn,s.FinOrRst,s.Payload));
    }

    public event Action<GameEvent>? EventDecoded;
    public event Action<CaptureStatus>? StatusChanged;

    public CaptureStatus Status => _status;
    public PacketPipeline Pipeline => _pipeline;
    public string? RecordingPath { get; private set; }

    /// <summary>Npcap's capture library is on this PC (its own folder, or System32 in WinPcap-compatible mode).</summary>
    public static bool IsNpcapInstalled() =>
        File.Exists(Path.Combine(Environment.SystemDirectory, "Npcap", "wpcap.dll")) ||
        File.Exists(Path.Combine(Environment.SystemDirectory, "wpcap.dll"));

    private const int KernelBufferBytes = 16 * 1024 * 1024;

    /// <summary>Packets Npcap delivered and dropped on the open adapter, and holes in the game streams; null while no
    /// adapter is open. The adapter's counters restart when it is reopened.</summary>
    public CaptureHealth? Health()
    {
        lock(_gate)
        {
            if(_devices.Count==0)return null;
            long received=_closedReceived,dropped=_closedDropped;
            foreach(var binding in _devices.Values)
                try {var stats=binding.Device.Statistics;received+=stats.ReceivedPackets;dropped+=stats.DroppedPackets+stats.InterfaceDroppedPackets;}catch(PcapException){}
            lock(_pipelineGate)return new(received,dropped,_pipeline.Gaps);
        }
    }

    public static IReadOnlyList<(string Name, string Description)> ListDevices()
    {
        return CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>()
            .Select(d => (d.Name, Describe(d)))
            .ToList();
    }

    public void Start()
    {
        if (_monitor is not null) return;
        _cts = new CancellationTokenSource();
        NetworkChange.NetworkAddressChanged+=NetworkChanged;
        SetStatus(CaptureState.Starting, "Starting capture…");
        _monitor = Task.Run(() => MonitorLoop(_cts.Token));
    }

    public void Stop()
    {
        NetworkChange.NetworkAddressChanged-=NetworkChanged;
        _cts?.Cancel();
        try { _monitor?.Wait(2_000); } catch (AggregateException) { }
        _monitor = null;
        CloseDevices();
        SetStatus(CaptureState.Stopped, "Capture stopped");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Refresh();
            }
            catch (DllNotFoundException)
            {
                SetStatus(CaptureState.Error, "Npcap is not installed — get it from npcap.com");
            }
            catch (TypeInitializationException)
            {
                SetStatus(CaptureState.Error, "Npcap is not installed — get it from npcap.com");
            }
            catch (Exception ex) when(ex is not OutOfMemoryException)
            {
                Interlocked.Increment(ref _captureErrors);
                SetStatus(CaptureState.Error,"Capture error: "+ex.GetType().Name+": "+ex.Message);
                CloseDevices();
            }
            try
            {
                await _refreshRequested.WaitAsync(2_000, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void Refresh()
    {
        var pids=GameProcessLocator.FindGameProcessIds();
        if(pids.Length==0 && _options.DeviceName is null)
        {CloseDevices();lock(_pipelineGate)_connections.Clear();SetStatus(CaptureState.WaitingForGame,"AION 2 is not running");return;}
        var connections=GameProcessLocator.FindConnections(pids);
        lock(_pipelineGate)_connections.Update(connections,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var list=CaptureDeviceList.Instance;list.Refresh();
        var available=list.OfType<LibPcapLiveDevice>().ToArray();
        var plan=CapturePlan.Create(connections,available.Select(d=>new CaptureAdapter(d.Name,IsLoopback(d),d.Addresses.Select(a=>a.Addr?.ipAddress).OfType<IPAddress>().ToArray())),_options.DeviceName);
        var now=Environment.TickCount64;
        var failures=new List<string>();
        lock(_gate)
        {
            // Open new routes before retiring old ones. Overlap is safe: TCP sequence
            // reassembly delivers bytes once even if both capture points see them.
            foreach(var entry in plan)
            {
                if(_devices.TryGetValue(entry.Key,out var stopped) && !stopped.Device.Started)
                {CloseBinding(entry.Key);Interlocked.Increment(ref _captureErrors);SetStatus(CaptureState.Error,"Capture error: adapter capture stopped; reopening");}
                if(_devices.TryGetValue(entry.Key,out var existing))
                {existing.Needed=now;if(existing.Filter!=entry.Value){existing.Device.Filter=entry.Value;existing.Filter=entry.Value;}continue;}
                var device=available.Single(d=>d.Name==entry.Key);
                var binding=new Binding(device,entry.Value,now);
                binding.Handler=(_,e)=>OnPacketArrival(binding,e);
                try
                {
                    device.Open(new DeviceConfiguration{Mode=DeviceModes.None,ReadTimeout=100,BufferSize=KernelBufferBytes});
                    device.Filter=binding.Filter;device.OnPacketArrival+=binding.Handler;
                    if(_options.RecordDirectory is {Length:>0} dir)
                    {
                        Directory.CreateDirectory(dir);
                        var path=Path.Combine(dir,$"aion2_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.pcap");
                        binding.Writer=new CaptureFileWriterDevice(path);binding.Writer.Open(new DeviceConfiguration{LinkLayerType=device.LinkType});RecordingPath=path;
                    }
                    device.StartCapture();_devices.Add(entry.Key,binding);
                }
                catch(Exception ex) when(ex is not OutOfMemoryException)
                {Interlocked.Increment(ref _captureErrors);failures.Add(ex.Message);device.OnPacketArrival-=binding.Handler;try{device.Close();binding.Writer?.Close();}catch(PcapException){}}
            }
            // A transient empty TCP table must not drop an established capture.
            if(plan.Count>0 && failures.Count==0)
                foreach(var old in _devices.Where(x=>!plan.ContainsKey(x.Key) && now-x.Value.Needed>30_000).Select(x=>x.Key).ToArray())CloseBinding(old);
        }
        if(failures.Count>0){SetStatus(CaptureState.Error,"Capture error: "+string.Join("; ",failures));return;}
        if(_options.DeviceName is not null && plan.Count==0){SetStatus(CaptureState.Error,"Selected network adapter not found");return;}
        if(plan.Count==0 && connections.Any(c=>c.RemotePort==PacketPipeline.GamePort))
        {SetStatus(CaptureState.Error,"No Npcap adapter carries the game connection (VPN? pick one in Settings)");return;}
        ReportFlowStatus();
    }

    private void ReportFlowStatus()
    {
        int flows;long lastData;string? server;
        lock(_pipelineGate)
        {flows=_pipeline.GameFlows;lastData=_pipeline.LastGameDataMs;server=_pipeline.LockedFlows.Select(f=>f.Src.ToString()).FirstOrDefault();}
        var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if(flows>0 && now-lastData<10_000)SetStatus(CaptureState.Capturing,$"Capturing · {server}"+(RecordingPath is null ? "" : " · REC"));
        else SetStatus(CaptureState.Idle,"Connected · no game traffic for a while");
    }

    private void CloseBinding(string name)
    {
        if(!_devices.Remove(name,out var binding))return;
        var device=binding.Device;
        try{device.StopCapture();}catch(PcapException){}
        device.OnPacketArrival-=binding.Handler;
        try{var stats=device.Statistics;_closedReceived+=stats.ReceivedPackets;_closedDropped+=stats.DroppedPackets+stats.InterfaceDroppedPackets;}catch(PcapException){}
        try{device.Close();}finally{binding.Writer?.Close();}
    }
    private void CloseDevices()
    {
        lock(_gate){foreach(var name in _devices.Keys.ToArray())CloseBinding(name);RecordingPath=null;}
    }

    private void OnPacketArrival(Binding binding,PacketCapture e)
    {
        var raw=e.GetPacket();Interlocked.Increment(ref _packets);
        try
        {
            lock(_pipelineGate)
            {
                binding.Writer?.Write(raw);
                if(SegmentReader.TryRead(raw,out var seg))_connections.Push(seg);
            }
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        {Interlocked.Increment(ref _captureErrors);System.Diagnostics.Debug.WriteLine(ex);}
    }

    private void OnEvent(GameEvent e)
    {
        _events++;
        EventDecoded?.Invoke(e);
    }

    private void SetStatus(CaptureState state, string message)
    {
        var s = new CaptureStatus(state, message, Interlocked.Read(ref _packets), _events);
        _status = s;
        StatusChanged?.Invoke(s);
    }

    private static bool IsLoopback(LibPcapLiveDevice d) =>
        d.Name.Contains("Loopback", StringComparison.OrdinalIgnoreCase) ||
        (d.Description?.Contains("loopback", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string Describe(LibPcapLiveDevice d) =>
        IsLoopback(d) ? "Loopback" : string.IsNullOrWhiteSpace(d.Description) ? d.Name : d.Description!;
}

public readonly record struct CaptureHealth(long Received, long Dropped, long Gaps);

/// <summary>Link layer → TCP segment, shared by live capture and pcap replay.</summary>
public static class SegmentReader
{
    public readonly record struct Segment(long TimeMs, FlowKey Key, uint Seq, bool Syn, bool FinOrRst, byte[] Payload);

    public static bool TryRead(RawCapture raw, out Segment segment)
    {
        segment = default;
        Packet packet;
        try
        {
            packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or NotImplementedException)
        {
            return false;
        }
        var ip = packet.Extract<IPPacket>();
        var tcp = packet.Extract<TcpPacket>();
        if (ip is null || tcp is null) return false;

        var t = raw.Timeval;
        var ms = (long)t.Seconds * 1000 + (long)t.MicroSeconds / 1000;
        var key = new FlowKey(ip.SourceAddress, tcp.SourcePort, ip.DestinationAddress, tcp.DestinationPort);
        segment = new Segment(ms, key, tcp.SequenceNumber, tcp.Synchronize, tcp.Finished || tcp.Reset, tcp.PayloadData ?? []);
        return true;
    }
}
