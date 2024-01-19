using DevkitServer.API;
using DevkitServer.Core.Logging.Loggers;
using DevkitServer.Plugins;
using SDG.Framework.Modules;

namespace DevkitServer.Compat;

/// <summary>
/// Plugin representing a foreign module for compatability reasons.
/// </summary>
[LoadPriority(int.MinValue)]
[Ignore]
public class ModulePlugin : CoreLogger, IDevkitServerColorPlugin
{
    /// <summary>
    /// Underlying module.
    /// </summary>
    public Module Module { get; }

    /// <inheritdoc/>
    public string PermissionPrefix { get; set; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string MenuName { get; private set; }

    /// <inheritdoc/>
    public string DataDirectory { get; }

    /// <inheritdoc/>
    public string LocalizationDirectory { get; }

    /// <inheritdoc/>
    public string CommandLocalizationDirectory { get; }

    /// <inheritdoc/>
    public PluginAssembly Assembly { get; set; } = null!;

    /// <inheritdoc/>
    public Local Translations { get; private set; }

    /// <inheritdoc/>
    public bool DeveloperMode => false;

    /// <inheritdoc/>
    public Color32 Color { get; } = new Color32(255, 255, 204, 255);

    /// <summary>
    /// Create a new plugin based on a foreign module.
    /// </summary>
    public ModulePlugin(Module module) : base(module.config.Name)
    {
        Module = module;
        Name = Module.config.Name;
        MenuName = Module.config.Name;
        PermissionPrefix = "module::" + module.config.Name.ToLowerInvariant();

        string dir = module.config.DirectoryPath;
        string dataDir = Path.Combine(dir, "Data");
        string localDir = Path.Combine(dir, "Localization");

        DataDirectory = Directory.Exists(dataDir) ? dataDir : dir;
        LocalizationDirectory = Directory.Exists(localDir) ? localDir : dir;

        string cmdLocalDir = Path.Combine(LocalizationDirectory, "Commands");
        CommandLocalizationDirectory = Directory.Exists(cmdLocalDir) ? cmdLocalDir : dir;

        if (File.Exists(Path.Combine(LocalizationDirectory, "English.dat")) || File.Exists(LocalizationDirectory + ".dat") || File.Exists(Path.Combine(LocalizationDirectory, Provider.language + ".dat")))
        {
            Translations = DevkitServerUtility.ReadLocalFromFileOrFolder(LocalizationDirectory, out _, out _);
        }
        else if (!LocalizationDirectory.Equals(DataDirectory, StringComparison.Ordinal) &&
                 (File.Exists(Path.Combine(DataDirectory, "English.dat")) || File.Exists(DataDirectory + ".dat") || File.Exists(Path.Combine(DataDirectory, Provider.language + ".dat"))))
        {
            Translations = DevkitServerUtility.ReadLocalFromFileOrFolder(DataDirectory, out _, out _);
            LocalizationDirectory = DataDirectory;
        }
        else if (!dir.Equals(DataDirectory, StringComparison.Ordinal) &&
                 (File.Exists(Path.Combine(dir, "English.dat")) || File.Exists(Path.Combine(dir, Provider.language + ".dat"))))
        {
            Translations = DevkitServerUtility.ReadLocalFromFileOrFolder(dir, out _, out _);
            LocalizationDirectory = dir;
        }
        else Translations = new Local();
    }

    /// <inheritdoc/>
    void IDevkitServerPlugin.Load()
    {
        Local lcl = Translations;

        DevkitServerUtility.UpdateLocalizationFile(ref lcl,
            new LocalDatDictionary
            {
                { "Name", Module.config.Name }
            },
            LocalizationDirectory, Directory.Exists(LocalizationDirectory)
                ? Path.Combine(LocalizationDirectory, Provider.language + ".dat")
                : LocalizationDirectory + ".dat",
            Directory.Exists(LocalizationDirectory)
                ? Path.Combine(LocalizationDirectory, "English.dat")
                : LocalizationDirectory + ".dat");

        Translations = lcl;
        if (Translations.has("Name"))
            MenuName = Translations.Translate("Name") ?? Module.config.Name;
    }

    /// <inheritdoc/>
    void IDevkitServerPlugin.Unload() { }

    string ILogSource.Source => string.Empty;
}
