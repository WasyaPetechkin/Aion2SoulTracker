namespace SoulTracker.Core;

public enum StreamIssue { None, TcpGap, OwnershipBuffer, PrelockBuffer, Framing, InvalidSnapshot, InvalidGain, HandlerFailure, ConnectionRestart, CaptureFailure, Capacity, Other }
public enum DiagnosticRoute { Unknown, Network, Loopback }
public sealed record CollectionDiagnostics(bool HasStoredValues,bool Trusted,bool IdentityConfirmed,
    int PendingSnapshots,long LastSnapshotMs,long LastGainMs,long AcceptedGainMessages,long AcceptedSoulAmount,
    long LastLossMs,StreamIssue LastLoss,DiagnosticRoute Route);

internal static class StreamIssueCode
{
    // Never propagate free-form reasons into a shareable report.
    public static StreamIssue FromReason(string reason) => reason switch
    {
        var x when x.StartsWith("TCP sequence gap",StringComparison.Ordinal)=>StreamIssue.TcpGap,
        var x when x.StartsWith("Ownership buffer",StringComparison.Ordinal)=>StreamIssue.OwnershipBuffer,
        var x when x.StartsWith("Pre-lock",StringComparison.Ordinal)=>StreamIssue.PrelockBuffer,
        "Invalid collection snapshot"=>StreamIssue.InvalidSnapshot,
        "Invalid soul gain" or "Soul gain timestamp moved backwards" or "Soul gain could not be applied atomically" or "Buffered soul gain could not be applied atomically"=>StreamIssue.InvalidGain,
        var x when x.StartsWith("Packet handler failed",StringComparison.Ordinal)||x.StartsWith("Packet delivery failed",StringComparison.Ordinal)=>StreamIssue.HandlerFailure,
        "TCP owner changed" or "TCP connection restarted"=>StreamIssue.ConnectionRestart,
        var x when x.StartsWith("Capture failed",StringComparison.Ordinal)=>StreamIssue.CaptureFailure,
        "Stream capacity exceeded" or "Pending collection gains overflow"=>StreamIssue.Capacity,
        var x when x.Contains("frame",StringComparison.OrdinalIgnoreCase)||x.Contains("bundle",StringComparison.OrdinalIgnoreCase)=>StreamIssue.Framing,
        _=>StreamIssue.Other
    };
}
