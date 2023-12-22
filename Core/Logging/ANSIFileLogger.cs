using DevkitServer.API.Logging;
using System.Text;

namespace DevkitServer.Core.Logging;
internal static class ANSIFileLogger
{
    private const string Source = "FILE LOG";

    private static FileStream? _ansiLog;
    internal static StreamWriter? LogWriter;
    internal static void OpenANSILog()
    {
        try
        {
            string fn = Dedicator.isStandaloneDedicatedServer ? $"Server_{Provider.serverID}" : "Client";
            string path = Path.Combine(ReadWrite.PATH, "Logs");
            string prevPath = Path.Combine(path, fn + "_Prev.ansi");
            path = Path.Combine(path, fn + ".ansi");

            bool canMove = true;
            try
            {
                if (File.Exists(prevPath))
                    File.Delete(prevPath);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, $"Unable to delete previous log - {ex.GetType().Format()} - {ex.Message.Format()}.");
                canMove = false;
            }

            if (canMove)
            {
                try
                {
                    if (File.Exists(path))
                        File.Move(path, prevPath);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(Source, $"Unable to move log to previous log - {ex.GetType().Format()} - {ex.Message.Format()}.");
                }
            }

            _ansiLog = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            LogWriter = new StreamWriter(_ansiLog, Encoding.UTF8, 1024, true);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, "Unable to open visual ANSI log.");
            Logger.DevkitServer.LogError(Source, ex);
        }
    }
    internal static void CloseANSILog()
    {
        Exception? ex1 = null, ex2 = null;
        if (LogWriter is not null)
        {
            try
            {
                LogWriter.Dispose();
                LogWriter = null;
            }
            catch (Exception ex)
            {
                ex1 = ex;
            }
        }

        if (_ansiLog is not null)
        {
            try
            {
                _ansiLog.Dispose();
            }
            catch (Exception ex)
            {
                ex2 = ex;
            }
            _ansiLog = null;
        }

        if (ex1 == null && ex2 == null)
            return;

        AggregateException ex3;
        if (ex1 != null && ex2 != null)
            ex3 = new AggregateException("Failed to close ANSI log.", ex1, ex2);
        else
            ex3 = new AggregateException("Failed to close ANSI log.", ex1 ?? ex2!);
        Logger.DevkitServer.LogError(Source, ex3);
    }
}
