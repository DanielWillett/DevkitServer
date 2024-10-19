using DevkitServer.Multiplayer.Actions;

namespace DevkitServer.Multiplayer;
public static class NetId64Registry
{
    private static ulong _min;
    
    public static NetId64 GetUniqueId()
    {
        if (_min == ulong.MaxValue)
            Reset();
        return new NetId64(++_min);
    }

    public static void Reset()
    {
        _min = 0;
    }
}