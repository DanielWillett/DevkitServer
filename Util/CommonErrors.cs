namespace DevkitServer.Util;
internal static class CommonErrors
{
    public static void LogPlayerSavedataAccessError(string fullPath)
    {
        Logger.LogError($"Error accessing player savedata directory {fullPath.Format()}, consider changing the {"player_savedata_override".Format()} value in the server config.");
    }
}
