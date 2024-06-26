﻿using DanielWillett.ReflectionTools;
using DevkitServer.API.Storage;
using DevkitServer.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using Backup = (string Path, System.DateTime UtcTimestamp);
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Level = SDG.Unturned.Level;
#if CLIENT
using DevkitServer.Core.UI.Extensions;
#endif

namespace DevkitServer.Levels;

/// <summary>
/// Schedules level backups for the map editor.
/// </summary>
public sealed class BackupManager : MonoBehaviour
{
    /// <summary>
    /// Date format for backup file names.
    /// </summary>
    public const string FileNameDateFormat = "yyyy-MM-dd_HH-mm-ss";

    internal const string Source = "BACKUPS";
    private Coroutine? _backupCoroutine;
    private bool _event;
    private float _lastSave;
    private int _lastSize = 52428800;
    private BackupLogs _logs = new BackupLogs();
    internal static bool PlayerHasJoinedSinceLastBackup;
    private static readonly SemaphoreSlim BackupLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Time the last backup was made (relative to <see cref="Time.realtimeSinceStartup"/>).
    /// </summary>
    public float LastSaveRealtime => _lastSave;

    /// <summary>
    /// Log tracker for the next backup.
    /// </summary>
    public BackupLogs Logs => _logs;

    /// <summary>
    /// Is a backup currently processing?
    /// </summary>
    public bool IsBackingUp { get; private set; }


    [UsedImplicitly]
    private void Start()
    {
        RefreshFromConfig(); // runs Restart in the OnReload handler.
        BackupConfiguration config = BackupConfiguration.Config;
        // if backup on startup is enabled and it didn't just save and it's not within the cooldown, backup.
        if (config.BackupOnStartup && CachedTime.RealtimeSinceStartup - _lastSave > 5f)
        {
            if (config.BackupOnStartupCooldown <= TimeSpan.Zero ||
                !TryGetLatestBackup(Level.info.name, out _, out DateTime utcTimestamp) ||
                DateTime.UtcNow - utcTimestamp > config.BackupOnStartupCooldown)
            {
                Backup(config.MaxBackups, config.MaxBackupSizeMegabytes, config.SaveBackupLogs, config.SaveOnBackup);
            }
            else
            {
                Logger.DevkitServer.LogInfo(Source, $"Skipped startup backup because a backup was made {(DateTime.UtcNow - utcTimestamp).TotalMinutes.Format("F0")} minute(s) ago.");
            }
        }
    }

    /// <summary>
    /// Reloads the config and refereshes the cached data in <see cref="BackupManager"/>.
    /// </summary>
    public void RefreshFromConfig() => BackupConfiguration.ConfigProvider.ReloadConfig();

    internal void Restart()
    {
        ThreadUtil.assertIsGameThread();

        BackupConfiguration config = BackupConfiguration.Config;

        if (_backupCoroutine != null)
        {
            StopCoroutine(_backupCoroutine);
            _backupCoroutine = null;
        }
        if (_event)
        {
            Provider.onServerDisconnected -= OnDisconnected;
            _event = false;
        }

        if (config.Behavior == BackupConfiguration.BackupBehavior.Disabled || config.MaxBackups == 0 || config.MaxBackupSizeMegabytes == 0)
        {
            LogNotEnabled();
            if (config.Behavior != BackupConfiguration.BackupBehavior.Disabled)
            {
                Logger.DevkitServer.LogWarning(Source, $"This is because \"{"max_backups".Colorize(DevkitServerModule.ModuleColor)}\" or " +
                                  $"\"{"max_total_backup_size_mb".Colorize(DevkitServerModule.ModuleColor)}\" is set to zero. " +
                                  $"Use {(-1).Format()} instead for unlimited.");
            }
            return;
        }

        if (config.MaxBackups > 0)
        {
            if (config.MaxBackupSizeMegabytes > 0)
            {
                Logger.DevkitServer.LogInfo(Source, $"Limits: {config.MaxBackups.Format()} backup{config.MaxBackups.S()}, " +
                                            $"Size: {FormattingUtil.FormatCapacity(DevkitServerUtility.ConvertMBToB(config.MaxBackupSizeMegabytes), colorize: true)}.");
            }
            else
                Logger.DevkitServer.LogInfo(Source, $"Limit: {config.MaxBackups.Format()} backup{config.MaxBackups.S()}.");
        }
        else if (config.MaxBackupSizeMegabytes > 0)
        {
            Logger.DevkitServer.LogInfo(Source, $"Size Limit: {FormattingUtil.FormatCapacity(DevkitServerUtility.ConvertMBToB(config.MaxBackupSizeMegabytes), colorize: true)}.");
        }
        else
        {
            Logger.DevkitServer.LogWarning(Source, "There are no limits set on backups, not recommended. " +
                                                   $"Set \"{"max_backups".Colorize(DevkitServerModule.ModuleColor)}\" and/or " +
                                                   $"\"{"max_total_backup_size_mb".Colorize(DevkitServerModule.ModuleColor)}\" " +
                                                   "to create a ring buffer for backups.");
        }

        switch (config.Behavior)
        {
            case BackupConfiguration.BackupBehavior.OnDisconnect:
                Provider.onServerDisconnected += OnDisconnected;
                _event = true;
                break;
            case BackupConfiguration.BackupBehavior.Interval:
                Backup();
                _backupCoroutine = StartCoroutine(IntervalBackup());
                break;
            case BackupConfiguration.BackupBehavior.Scheduled:
                _backupCoroutine = StartCoroutine(ScheduleBackup());
                break;
            default:
                Logger.DevkitServer.LogError(Source, $"Invalid value for backup behavior: {config.Behavior.ToString().Colorize(ConsoleColor.DarkRed)}.");
                LogNotEnabled();
                break;
        }
    }
    private static void LogNotEnabled() => Logger.DevkitServer.LogWarning(Source, $"Backups {"disabled".Colorize(ConsoleColor.Red)} (not recommended).");

    /// <summary>
    /// Gets the path of a backup saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetBackupPath(DateTimeOffset timestamp) => GetBackupPath(Level.info.name, timestamp);

    /// <summary>
    /// Gets the path of a backup's log folder saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetLogFolderPath(DateTimeOffset timestamp) => GetLogFolderPath(Level.info.name, timestamp);

    /// <summary>
    /// Gets the path of a backup with the name <paramref name="levelName"/> saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetBackupPath(string levelName, DateTimeOffset timestamp) =>
        Path.Combine(BackupConfiguration.BackupPath, GetBackupName(levelName, timestamp));

    /// <summary>
    /// Gets the path of a backup with the name <paramref name="levelName"/>'s log folder saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetLogFolderPath(string levelName, DateTimeOffset timestamp) =>
        Path.Combine(BackupConfiguration.BackupPath, GetLogFolderName(levelName, timestamp));

    /// <summary>
    /// Gets the file name of a backup saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetBackupName(DateTimeOffset timestamp) => GetBackupName(Level.info.name, timestamp);

    /// <summary>
    /// Gets the file name of a backup's log folder saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetLogFolderName(DateTimeOffset timestamp) => GetLogFolderName(Level.info.name, timestamp);

    /// <summary>
    /// Gets the file name of a backup with the name <paramref name="levelName"/> saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetBackupName(string levelName, DateTimeOffset timestamp) =>
        "backup_" + levelName + "_" + timestamp.UtcDateTime.ToString(FileNameDateFormat, CultureInfo.InvariantCulture) + ".zip";

    /// <summary>
    /// Gets the file name of a backup with the name <paramref name="levelName"/>'s log folder saved at <paramref name="timestamp"/>.
    /// </summary>
    public static string GetLogFolderName(string levelName, DateTimeOffset timestamp) =>
        "logs_" + levelName + "_" + timestamp.UtcDateTime.ToString(FileNameDateFormat, CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar;

    /// <summary>
    /// Parses a backup or log folder name to get the timestamp and level name from the file name.
    /// </summary>
    public static bool TryParseBackupName(string fileName, out DateTime utcTimestamp, out string levelName)
    {
        utcTimestamp = default;
        levelName = null!;
        int offset = 7;
        if (!fileName.StartsWith("backup_", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!fileName.StartsWith("logs_", StringComparison.InvariantCultureIgnoreCase))
                return false;
            offset = 5;
        }
        int nextUnderscore = fileName.IndexOf('_', offset);
        if (nextUnderscore == -1)
            return false;
        levelName = fileName.Substring(offset, nextUnderscore - offset);
        int ext = fileName.IndexOf('.', nextUnderscore + 1);
        if (ext == -1) ext = fileName.Length;

        if (DateTime.TryParseExact(fileName.Substring(nextUnderscore + 1, ext - nextUnderscore - 1),
            FileNameDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out utcTimestamp))
        {
            utcTimestamp = utcTimestamp.ToUniversalTime();
            return true;
        }

        return false;
    }

    private void OnDisconnected(CSteamID steamid)
    {
        if (CachedTime.RealtimeSinceStartup - _lastSave > 5f)
            Backup();
        else
            Logger.DevkitServer.LogInfo(Source, $"Backup {DateTime.UtcNow.ToString(FileNameDateFormat).Format(false)} (UTC) skipped because the level was very recently backed up.");
    }

    /// <summary>
    /// Start a backup (compression will be done on another thread, fire and forget).
    /// </summary>
    public void Backup() => Backup(BackupConfiguration.Config.MaxBackups, BackupConfiguration.Config.MaxBackupSizeMegabytes, BackupConfiguration.Config.SaveBackupLogs, BackupConfiguration.Config.SaveOnBackup);

    /// <summary>
    /// Start a backup specifying whether or not to save the level as well (compression will be done on another thread, fire and forget).
    /// </summary>
    public void Backup(bool saveOnBackup) => Backup(BackupConfiguration.Config.MaxBackups, BackupConfiguration.Config.MaxBackupSizeMegabytes, BackupConfiguration.Config.SaveBackupLogs, saveOnBackup);

    /// <summary>
    /// Run a backup with the configured deletion settings (compression will be done on another thread, fire and forget).
    /// </summary>
    public void Backup(int maxBackups, double maxBackupSizeMegabytes, bool saveLogs, bool saveOnBackup)
    {
        ThreadUtil.assertIsGameThread();

        Stopwatch timer = Stopwatch.StartNew();
        BackupLogs oldLogs;
        DateTimeOffset timestamp;
        Task<LevelData> saveTask;
        BackupLock.Wait();
        IsBackingUp = true;
#if CLIENT
        EditorPauseUIExtension.OnLevelDataGatherStateUpdated();
#endif
        try
        {
            _lastSave = CachedTime.RealtimeSinceStartup;
            Logger.DevkitServer.LogInfo(Source, $"Backing up {Level.info.name.Format(false)}...");
            timestamp = DateTimeOffset.UtcNow;

            saveTask = LevelData.GatherLevelData(!saveOnBackup, token: DevkitServerModule.UnloadToken);

            oldLogs = Interlocked.Exchange(ref _logs, new BackupLogs());
            if (Provider.clients.Count == 0)
                PlayerHasJoinedSinceLastBackup = false;
        }
        catch
        {
            BackupLock.Release();
            IsBackingUp = false;
#if CLIENT
            EditorPauseUIExtension.OnLevelDataGatherStateUpdated();
#endif
            throw;
        }
        Task.Run(async () =>
        {
            string path;
            try
            {
                path = GetBackupPath(timestamp);
                await ZipAndClear(path, await saveTask.ConfigureAwait(false), maxBackups, maxBackupSizeMegabytes);
                saveTask = null!;
            }
            catch (OperationCanceledException)
            {
                path = null!;
                Logger.DevkitServer.LogError(Source, $"Cancelled backing up level {Level.info.name.Colorize(DevkitServerModule.UnturnedColor)}.");
                GC.Collect();
            }
            catch (Exception ex)
            {
                path = null!;
                Logger.DevkitServer.LogError(Source, ex, $"Error backing up level {Level.info.name.Colorize(DevkitServerModule.UnturnedColor)}.");
                GC.Collect();
            }
            finally
            {
                IsBackingUp = false;
#if CLIENT
                EditorPauseUIExtension.OnLevelDataGatherStateUpdated();
#endif
                BackupLock.Release();
            }
            if (saveLogs)
            {
                await DevkitServerUtility.ToUpdate();

                oldLogs.Write(GetLogFolderPath(timestamp));
            }

            timer.Stop();
            Logger.DevkitServer.LogInfo(Source, $"Backed up to {path.Format()} in {timer.GetElapsedMilliseconds().Format("F2")} ms.");
            GC.Collect();
        });
    }
    private async Task ZipAndClear(string path, LevelData save, int maxBackups, double maxBackupSizeMegabytes)
    {
        using MemoryStream stream = new MemoryStream(_lastSize);
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            VirtualDirectoryRoot folder = save.LevelFolderContent;
            foreach (string dir in folder.Directories)
            {
                if (string.IsNullOrEmpty(dir))
                    continue;
                archive.CreateEntry(dir[^1] != Path.DirectorySeparatorChar ? dir + Path.DirectorySeparatorChar : dir, CompressionLevel.Optimal);
            }

            foreach (VirtualFile file in folder.Files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                await using Stream zipFileEntryStream = entry.Open();
                await zipFileEntryStream.WriteAsync(file.Content).ConfigureAwait(false);
                await zipFileEntryStream.FlushAsync().ConfigureAwait(false);
            }
        }

        long streamLength = stream.Length;
        _lastSize = (int)Math.Min(1073741824L, streamLength);

        Logger.DevkitServer.LogDebug(Source, $"Zipped {FormattingUtil.FormatCapacity(streamLength, colorize: true)}.");
        ClearBackups(streamLength, maxBackups < 0 ? null : maxBackups,
            maxBackupSizeMegabytes >= 0 ? DevkitServerUtility.ConvertMBToB(maxBackupSizeMegabytes) : null);

        await using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Seek(0, SeekOrigin.Begin);
        await stream.CopyToAsync(fs).ConfigureAwait(false);
    }
    private static void ClearBackups(long extraSize, int? maxBackups, long? maxBackupSize)
    {
        if (maxBackupSize.HasValue)
        {
            long maxSize = maxBackupSize.Value - extraSize;
            if (maxSize < 0)
                maxSize = 0;
            try
            {
                while (true)
                {
                    long actualSize = CountBackupSize(Level.info.name);
                    if (actualSize > maxSize)
                    {
                        if (DeleteOldestBackups(Level.info.name, 1) < 1)
                            break;
                    }
                    else break;
                }
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error trying to calculate backup folder size.");
            }
        }
        if (maxBackups.HasValue)
        {
            try
            {
                int backupCount = CountBackups(Level.info.name);
                if (backupCount >= maxBackups.Value)
                {
                    DeleteOldestBackups(Level.info.name, backupCount - maxBackups.Value + 1);
                }
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error trying to calculate backup count.");
            }
        }
    }
    private static long CountBackupSize(string levelName)
    {
        long ttl = 0;
        string[] files = Directory.GetFileSystemEntries(BackupConfiguration.BackupPath, "*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];

            if (!TryParseBackupName(Path.GetFileName(file), out _, out string lvl) || !lvl.Equals(levelName, StringComparison.Ordinal))
                continue;

            FileInfo fileInfo = new FileInfo(file);
            if (fileInfo.Exists)
                ttl += fileInfo.Length;
            else
                ttl += FileUtil.GetDirectorySize(file);
        }

        return ttl;
    }
    private static int CountBackups(string levelName)
    {
        int ct = 0;
        string[] files = Directory.GetFiles(BackupConfiguration.BackupPath, "*.zip", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (TryParseBackupName(Path.GetFileName(file), out _, out string lvl) && lvl.Equals(levelName, StringComparison.Ordinal))
                ++ct;
        }

        return ct;
    }

    /// <summary>
    /// Get an array of all backups sorted by date (newest to oldest).
    /// </summary>
    public static Backup[] GetOrderedBackups(string levelName)
    {
        string[] entries = Directory.GetFiles(BackupConfiguration.BackupPath, "*.zip", SearchOption.TopDirectoryOnly);
        Backup[] backups = new Backup[entries.Length];
        int index = -1;
        for (int i = 0; i < entries.Length; ++i)
        {
            string entry = entries[i];

            if (!TryParseBackupName(Path.GetFileName(entry), out DateTime utcTimestamp, out string lvl) || !lvl.Equals(levelName, StringComparison.Ordinal))
                continue;

            bool added = false;
            for (int j = 0; j <= index; ++j)
            {
                if (backups[j].UtcTimestamp >= utcTimestamp)
                    continue;
                ++index;
                for (int k = index; k > j; --k)
                    backups[k] = backups[k - 1];
                backups[j] = (entry, utcTimestamp);
                added = true;
                break;
            }

            if (!added)
                backups[++index] = (entry, utcTimestamp);
        }
        if (backups.Length - 1 != index)
            Array.Resize(ref backups, index + 1);

        return backups;
    }
    private static bool TryGetMinMaxBackup(string levelName, out string path, out DateTime utcTimestamp, bool latest)
    {
        string[] entries = Directory.GetFiles(BackupConfiguration.BackupPath, "*.zip", SearchOption.TopDirectoryOnly);
        path = null!;
        utcTimestamp = latest ? DateTime.MinValue : DateTime.MaxValue;
        for (int i = 0; i < entries.Length; ++i)
        {
            string entry = entries[i];
            if (!TryParseBackupName(Path.GetFileName(entry), out DateTime dt, out string lvl) ||
                !lvl.Equals(levelName, StringComparison.Ordinal)) continue;
            if (latest)
            {
                if (utcTimestamp >= dt)
                    continue;
                utcTimestamp = dt;
                path = entry;
            }
            else
            {
                if (utcTimestamp <= dt)
                    continue;
                utcTimestamp = dt;
                path = entry;
            }
        }
        return path != null;
    }

    /// <summary>
    /// Try to get the most recent backup.
    /// </summary>
    public static bool TryGetLatestBackup(string levelName, out string path, out DateTime utcTimestamp) =>
        TryGetMinMaxBackup(levelName, out path, out utcTimestamp, true);

    /// <summary>
    /// Try to get the least recent backup.
    /// </summary>
    public static bool TryGetOldestBackup(string levelName, out string path, out DateTime utcTimestamp) =>
        TryGetMinMaxBackup(levelName, out path, out utcTimestamp, false);

    /// <summary>
    /// Try to delete <paramref name="ct"/> backups starting from the oldest one.
    /// </summary>
    /// <returns>The amount of files deleted.</returns>
    public static int DeleteOldestBackups(string levelName, int ct = 1)
    {
        if (ct <= 0)
            return 0;
        Backup[] backups = GetOrderedBackups(levelName);
        
        int ctDeleted = 0;
        for (int i = 0; i < ct; ++i)
        {
            int index = backups.Length - i - 1;
            if (index < 0)
                break;
            string path = backups[index].Path;
            bool deleted = false;
            try
            {
                File.Delete(path);
                deleted = true;
                ++ctDeleted;
                Logger.DevkitServer.LogInfo(Source, $"Deleted old backup: {backups[index].UtcTimestamp.ToString(FileNameDateFormat).Format(false)} (UTC).");
                path = GetLogFolderPath(levelName, backups[index].UtcTimestamp);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                if (!deleted)
                    ++ct;
                Logger.DevkitServer.LogError(Source, ex, $"Error trying to delete old backup: {path.Format()} (UTC).");
            }
        }

        return ctDeleted;
    }
    private IEnumerator IntervalBackup()
    {
        BackupConfiguration config = BackupConfiguration.Config;
        TimeSpan? configInterval = config.BackupInterval;
        double interval = configInterval?.TotalSeconds ?? 3600d;
        bool configSkipInactive = config.SkipInactiveBackup;
        int maxBackups = config.MaxBackups;
        double maxSizeMb = config.MaxBackupSizeMegabytes;
        bool configSaveLogs = config.SaveBackupLogs;
        bool configSaveOnBackup = config.SaveOnBackup;
        while (DevkitServerModule.IsAuthorityEditor)
        {
            Logger.DevkitServer.LogInfo(Source, $"Next Backup: {DateTime.Now.AddSeconds(interval).ToString("G").Format(false)} (LOCAL).");

            yield return new WaitForSecondsRealtime((float)interval);
            if (!DevkitServerModule.IsAuthorityEditor)
                yield break;
            if (!configSkipInactive || PlayerHasJoinedSinceLastBackup)
            {
                if (CachedTime.RealtimeSinceStartup - _lastSave > 5f)
                    Backup(maxBackups, maxSizeMb, configSaveLogs, configSaveOnBackup);
                else
                    Logger.DevkitServer.LogInfo(Source, $"Backup {DateTime.Now.ToString(FileNameDateFormat).Format(false)} (LOCAL) skipped because the level was very recently backed up.");
            }
            else
            {
                Logger.DevkitServer.LogInfo(Source, $"Backup {DateTime.Now.ToString(FileNameDateFormat).Format(false)} (LOCAL) skipped because of inactivity.");
            }
        }
    }
    private IEnumerator ScheduleBackup()
    {
        BackupConfiguration config = BackupConfiguration.Config;
        DateTime[]? configSchedule = config.BackupSchedule;
        bool configUtc = config.UTCBackupSchedule;
        bool configSkipInactive = config.SkipInactiveBackup;
        int maxBackups = config.MaxBackups;
        double maxSizeMb = config.MaxBackupSizeMegabytes;
        bool configSaveLogs = config.SaveBackupLogs;
        bool configSaveOnBackup = config.SaveOnBackup;
        ScheduleInterval configScheduleInterval = config.BackupScheduleInterval;
        if (configSchedule == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Schedule not specified for {"Scheduled".Colorize(ConsoleColor.Green)} backup mode.");
            LogNotEnabled();
            yield break;
        }
        while (DevkitServerModule.IsAuthorityEditor)
        {
            DateTime? next = DevkitServerUtility.FindNextSchedule(configSchedule, false, configScheduleInterval);
            if (!next.HasValue)
            {
                Logger.DevkitServer.LogWarning(Source, "Schedule has no queued upcoming events.");
                LogNotEnabled();
                yield break;
            }
            Logger.DevkitServer.LogInfo(Source, $"Next Backup: {next.Value.ToString("G").Format(false)} ({(configUtc ? "UTC" : "LOCAL")}).");
            DateTime now = configUtc ? DateTime.UtcNow : DateTime.Now;
            yield return new WaitForSecondsRealtime((float)(next.Value - now).TotalSeconds);
            if (!DevkitServerModule.IsAuthorityEditor)
                yield break;
            if (!configSkipInactive || PlayerHasJoinedSinceLastBackup)
            {
                if (CachedTime.RealtimeSinceStartup - _lastSave > 5f)
                    Backup(maxBackups, maxSizeMb, configSaveLogs, configSaveOnBackup);
                else
                    Logger.DevkitServer.LogInfo(Source, $"Backup {DateTime.Now.ToString(FileNameDateFormat).Format(false)} (LOCAL) skipped because the level was very recently backed up.");
            }
            else
            {
                Logger.DevkitServer.LogInfo(Source, $"Backup {DateTime.Now.ToString(FileNameDateFormat).Format(false)} (LOCAL) skipped because of inactivity.");
            }
        }
    }
}