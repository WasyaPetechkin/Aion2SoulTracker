using System.Buffers.Binary;
using AionMeter.Core.Protocol;

namespace SoulTracker.Core;

public sealed record NavigationView(int? WorldId,uint? ActorId,double? X,double? Y,long TimeMs,string Kind,bool IsReplay)
{
    public bool HasPosition=>X.HasValue && Y.HasValue && WorldId.HasValue;
}

/// <summary>Only positions identified as the local player are used; nearby NPCs are never a substitute.</summary>
public sealed class NavigationState
{
    private readonly object _gate=new();
    private int? _world;
    private uint? _actor;
    private double? _x,_y;
    private long _time,_mapTime;
    private string _kind="";
    private bool _replay;
    public void Reset(bool replay=false)
    { lock(_gate) {_world=null;_actor=null;_x=_y=null;_time=_mapTime=0;_kind="";_replay=replay;} }
    public void MarkGap()
    {
        // A hole in the same TCP connection does not change its known local actor ID.
        // The zone may have changed in the missing bytes: require MapLoad before showing a dot again.
        lock(_gate) {_world=null;_x=_y=null;_time=_mapTime=0;_kind="gap";}
    }
    public NavigationView View() {lock(_gate)return new(_world,_actor,_x,_y,_time,_kind,_replay);}
    public bool Process(long time,ushort op,ReadOnlySpan<byte> body)
    {
        if(op is not (0x3336 or 0x2136 or 0x2336 or 0x0238 or 0x1A37 or 0x1B37 or 0x1C37 or 0x2837 or 0x2937))return false;
        lock(_gate)
        {
            if(op==0x3336)
            {
                if(!Wire.TryVarint(body,0,out var actor,out _) || actor==0) return false;
                if(_actor.HasValue && _actor!=actor && time-_mapTime>30_000) {_x=_y=null;_time=0;}
                _actor=actor;return false;
            }
            if(op==0x2136 && body.Length>=28)
            {
                var world=BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                if(world is 0 or >9_999_999 || !Coordinates(body,16,out var x,out var y)) return false;
                _world=(int)world;_mapTime=time;
                return Set(time,x,y,"entry");
            }
            if(op==0x2336 && body.Length>=18 && body[0]==0 && Coordinates(body,6,out var tx,out var ty))
                return Set(time,tx,ty,"teleport");
            if(!_actor.HasValue || !Wire.TryVarint(body,0,out var id,out var offset) || id!=_actor) return false;
            if(op==0x0238)
            {
                var targetOffset=offset+7;
                if(!Wire.TryVarint(body,ref targetOffset,out var target) || target==_actor ||
                    !Coordinates(body,targetOffset+4,out var x,out var y) || (Math.Abs(x)<2000 && Math.Abs(y)<2000)) return false;
                return Set(time,x,y,"attack");
            }
            var skip=op switch {0x1A37=>2,0x2837 or 0x2937=>1,_=>-1};
            if(op is 0x1B37 or 0x1C37)
            {
                if(offset>=body.Length)return false;
                skip=1+((body[offset]&1)!=0 ? 1 : 0);
            }
            if(skip>=0 && Coordinates(body,offset+skip,out var mx,out var my)) return Set(time,mx,my,"movement");
            return false;
        }
    }
    private bool Set(long time,double x,double gameY,string kind)
    {_x=x;_y=-gameY;_time=time;_kind=kind;return true;}
    private static bool Coordinates(ReadOnlySpan<byte> body,int offset,out double x,out double y)
    {
        x=y=0;
        if(offset<0 || offset+12>body.Length) return false;
        x=BinaryPrimitives.ReadSingleLittleEndian(body[offset..]);
        y=BinaryPrimitives.ReadSingleLittleEndian(body[(offset+4)..]);
        var z=BinaryPrimitives.ReadSingleLittleEndian(body[(offset+8)..]);
        return double.IsFinite(x) && double.IsFinite(y) && float.IsFinite(z) &&
            Math.Abs(x)<1_000_000 && Math.Abs(y)<1_000_000 && Math.Abs(z)<1_000_000 &&
            (x==0 || Math.Abs(x)>0.0001) && (y==0 || Math.Abs(y)>0.0001);
    }
}
