using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Util;
internal static class CommonErrors
{
    public static void LogPlayerSavedataAccessError(string fullPath)
    {
        Logger.LogError($"Error accessing player savedata directory {fullPath.Format()}, consider changing the {"player_savedata_override".Format()} value in the server config.");
    }
}
