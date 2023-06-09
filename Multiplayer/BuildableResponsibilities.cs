using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Multiplayer;
public static class BuildableResponsibilities
{
#if SERVER
    public static List<ulong>[,] Table;
#elif CLIENT
    public static List<bool>[,] Table;
#endif

#if SERVER
    // public static ulong GetPlacer(byte x, byte y) => Table.GetPlacer(instanceId);
    // public static bool IsPlacer(uint instanceId, ulong user) => Table.IsPlacer(instanceId, user);
#else
    //public static bool IsPlacer(uint instanceId) => Table.IsPlacer(instanceId);
#endif
}
