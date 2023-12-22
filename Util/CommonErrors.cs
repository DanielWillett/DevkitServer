namespace DevkitServer.Util;
internal static class CommonErrors
{
#if SERVER
    public static void LogPlayerSavedataAccessError(string fullPath)
    {
        Logger.DevkitServer.LogError("PLAYER SAVEDATA", $"Error accessing player savedata directory {fullPath.Format()}, consider changing the {"player_savedata_override".Format()} value in the server config.");
    }
#endif
}
