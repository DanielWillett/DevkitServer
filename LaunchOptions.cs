namespace DevkitServer;
internal static class LaunchOptions
{
    /*
     *  Prevents deleting files from DevkitServer/Temp/* until needed.
     */
    public static CommandLineFlag KeepTempFiles = new CommandLineFlag(false, "-DevkitServerKeepTemporaryFiles");
}
