using System.Globalization;
using System.Text;

namespace DevkitServer.Levels;
public sealed class BackupLogs
{
    public static BackupLogs? Instance
    {
        get
        {
            BackupManager? manager = DevkitServerModule.BackupManager;
            return manager != null ? manager.Logs : null;
        }
    }

    private const string Source = "BACKUP LOGS";
    public List<IBackupLog> Logs { get; } = new List<IBackupLog>();

    public T GetOrAdd<T>() where T : class, IBackupLog, new()
    {
        lock (Logs)
        {
            int index = Logs.FindIndex(x => x is T);
            if (index < 0)
            {
                T val = new T();
                Logs.Add(val);
                return val;
            }
            return (T)Logs[index];
        }
    }

    public T GetOrAddLazy<T>(Lazy<T> adder) where T : class, IBackupLog
    {
        lock (Logs)
        {
            int index = Logs.FindIndex(x => x is T);
            if (index < 0)
            {
                T val = adder.Value;
                Logs.Add(val);
                return val;
            }
            return (T)Logs[index];
        }
    }
    internal void Write(string folderPath)
    {
        if (!FileUtil.CheckDirectory(false, folderPath))
        {
            Logger.LogError($"Failed to create logs directory: {folderPath.Format()}.", method: Source);
            return;
        }

        lock (Logs)
        {
            for (int i = 0; i < Logs.Count; ++i)
            {
                IBackupLog backupLog = Logs[i];
                string name = backupLog.RelativeName;
                bool ext = name.LastIndexOf('.') != -1;
                string log = Path.Combine(folderPath, name);
                int c = 0;
                do
                {
                    if (c > 0)
                        log = Path.Combine(folderPath, name + "_" + (c + 1).ToString(CultureInfo.InvariantCulture));
                    if (!ext)
                        log += ".txt";
                    ++c;
                }
                while (File.Exists(log));

                bool wrote = false;
                try
                {
                    using (FileStream stream = new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    {
                        backupLog.Write(writer);
                        wrote = true;
                    }
                    if (backupLog is IDisposable d)
                        d.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error writing to log file: {log.Format()} of {backupLog.GetType()}.", method: Source);
                    Logger.LogError(ex, method: Source);
                    if (!wrote)
                    {
                        try
                        {
                            if (File.Exists(log))
                                File.Delete(log);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
        }
    }
}