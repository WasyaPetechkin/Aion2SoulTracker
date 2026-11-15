namespace AionMeter.Core.Capture;

public enum CaptureIssue { None, NpcapUnavailable, MonitorFailure, AdapterStopped, AdapterOpenFailed, NoMatchingAdapter, SelectedAdapterMissing }

// Explicitly excludes endpoints, process IDs, adapter descriptions and exception text.
public sealed record CaptureDiagnostics(int Processes, int Connections, int RelayConnections,
    int NetworkReaders, int LoopbackReaders, long DiscoveryTimeMs, long CapturedPackets,
    long GameSegments, int RecognizedFlows, long LastGameDataMs, long TcpGaps,
    int PendingOwnershipSegments, long DiscardedBeforeOwnership, long Errors,
    CaptureState State, CaptureIssue Issue)
{
    public int ActiveReaders => NetworkReaders + LoopbackReaders;
}
