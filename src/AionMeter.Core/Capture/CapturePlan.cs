using System.Net;
namespace AionMeter.Core.Capture;

public sealed record CaptureAdapter(string Name,bool Loopback,IPAddress[] Addresses);
public static class CapturePlan
{
    public static Dictionary<string,string> Create(IEnumerable<GameConnection> connections,IEnumerable<CaptureAdapter> adapters,string? forced=null)
    {
        var devices=adapters.ToArray();var result=new Dictionary<string,string>();
        if(forced is not null)
        {if(devices.Any(x=>x.Name==forced))result[forced]="tcp and src port 13328";return result;}
        var all=connections.ToArray();var game=all.Where(c=>c.RemotePort==PacketPipeline.GamePort).ToArray();
        // Ignore the game's own local IPC sockets: they are not a game server.
        var chosen=all.Where(c=>c.LocalRelay || (game.Length>0 ? c.RemotePort==PacketPipeline.GamePort : !c.LocalIp.Equals(c.RemoteIp) && !IPAddress.IsLoopback(c.RemoteIp))).ToArray();
        foreach(var c in chosen)
        {
            var device=devices.FirstOrDefault(d=>IPAddress.IsLoopback(c.RemoteIp) ? d.Loopback : d.Addresses.Contains(c.LocalIp));
            if(device is null)continue;
            // Stable port filter covers server transfers before the next TCP-table poll.
            var filter=c.RemotePort==PacketPipeline.GamePort ? "tcp and src port 13328" : $"tcp and src host {c.RemoteIp} and src port {c.RemotePort}";
            if(result.TryGetValue(device.Name,out var old) && old!=filter)result[device.Name]=$"({old}) or ({filter})";
            else result[device.Name]=filter;
        }
        // Keep ready physical/VPN capture points open for a route switch. Only the
        // inbound game port is captured; disconnected/APIPA interfaces are excluded.
        if(game.Length>0)
            foreach(var d in devices.Where(d=>!d.Loopback && d.Addresses.Any(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !(a.GetAddressBytes()[0]==169 && a.GetAddressBytes()[1]==254))))
                result.TryAdd(d.Name,"tcp and src port 13328");
        return result;
    }
}
