extern alias NSJ;
using DevkitServer.API;
using DevkitServer.Configuration.Converters;
using DevkitServer.Levels;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration;
[EarlyTypeInit(-2)]
public sealed class BackupConfiguration : SchemaConfiguration
{
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/backup_schema.json", true);
    private sealed class ConfigContainer : JsonConfigurationFile<BackupConfiguration>
    {
        internal ConfigContainer() : base(FilePath)
        {
            ReadOnlyReloading = true;
        }

        protected override void OnReload()
        {
            DevkitServerUtility.QueueOnMainThread(() =>
            {
                BackupManager? manager = DevkitServerModule.BackupManager;
                if (manager != null) manager.Restart();
            });
        }

        public override BackupConfiguration Default => new BackupConfiguration
        {
            Behavior = BackupBehavior.Scheduled,
            BackupInterval = TimeSpan.FromHours(3),
            BackupScheduleInterval = ScheduleInterval.Daily,
            UTCBackupSchedule = false,
            BackupSchedule = new DateTime[]
            {
                new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Local),  // 12:00 AM
                new DateTime(1, 1, 1, 3, 0, 0, DateTimeKind.Local),  //  3:00 AM
                new DateTime(1, 1, 1, 6, 0, 0, DateTimeKind.Local),  //  6:00 AM
                new DateTime(1, 1, 1, 9, 0, 0, DateTimeKind.Local),  //  9:00 AM
                new DateTime(1, 1, 1, 12, 0, 0, DateTimeKind.Local), // 12:00 PM
                new DateTime(1, 1, 1, 15, 0, 0, DateTimeKind.Local), //  3:00 PM
                new DateTime(1, 1, 1, 18, 0, 0, DateTimeKind.Local), //  6:00 PM
                new DateTime(1, 1, 1, 21, 0, 0, DateTimeKind.Local), //  9:00 PM
            },
            MaxBackups = 24,
            MaxBackupSizeMegabytes = 2097.152d, // 2GiB
            SaveBackupLogs = true,
            BackupOnStartupCooldown = TimeSpan.FromHours(3),
            BackupOnStartup = true,
            SkipInactiveBackup = true
        };
    }
    public enum BackupBehavior
    {
        Disabled,
        Scheduled,
        Interval,
        OnDisconnect
    }

    [CreateDirectory]
    public static readonly string BackupPath = Path.Combine(DevkitServerConfig.Directory, "Backups");
    public static readonly string FilePath = Path.Combine(DevkitServerConfig.Directory, "backup_config.json");
    private static readonly ConfigContainer IntlConfig = new ConfigContainer { Defaultable = true };
    public static IConfigProvider<BackupConfiguration> ConfigProvider => IntlConfig;
    public static BackupConfiguration Config => IntlConfig.Configuration;

    static BackupConfiguration() { IntlConfig.ReloadConfig(); }

    [JsonPropertyName("backup_behavior")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BackupBehavior Behavior { get; set; }

    [JsonPropertyName("backup_interval")]
    public TimeSpan? BackupInterval { get; set; }

    [JsonPropertyName("backup_schedule")]
    [JsonConverter(typeof(ScheduleConverter))]
    public DateTime[]? BackupSchedule { get; set; }

    [JsonPropertyName("backup_schedule_interval")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScheduleInterval BackupScheduleInterval { get; set; }

    [JsonPropertyName("backup_schedule_is_utc")]
    public bool UTCBackupSchedule { get; set; }

    [JsonPropertyName("save_backup_on_startup")]
    public bool BackupOnStartup { get; set; }

    [JsonPropertyName("backup_on_startup_cooldown")]
    public TimeSpan BackupOnStartupCooldown { get; set; }

    [JsonPropertyName("max_backups")]
    public int MaxBackups { get; set; } = -1;

    [JsonPropertyName("max_total_backup_size_mb")]
    public double MaxBackupSizeMegabytes { get; set; } = -1;

    [JsonPropertyName("skip_inactive_backup")]
    public bool SkipInactiveBackup { get; set; }

    [JsonPropertyName("save_backup_logs")]
    public bool SaveBackupLogs { get; set; }

    [JsonPropertyName("save_level_on_backup")]
    public bool SaveOnBackup { get; set; }
}