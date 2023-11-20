using DevkitServer.Launcher.Models;
using JetBrains.Annotations;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using SDG.Framework.IO;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.Launcher;

public class DevkitServerAutoUpdateComponent : MonoBehaviour
{
    public static event UpdateReady? OnUpdateReady;
    public static event Action<DevkitServerAutoUpdateComponent>? OnAutoUpdaterInitialized;
    public float CheckTimer { get; set; } = 120f;
    public float ShutdownTimer { get; set; } = 120f;
    public static bool DebugLog
    {
        get
        {
            DevkitServerAutoUpdateComponent? inst = Instance;
            return inst != null && inst._logger.ShouldLogDebug;
        }
        set
        {
            DevkitServerAutoUpdateComponent? inst = Instance;
            if (inst != null)
                inst._logger.ShouldLogDebug = value;
        }
    }

    private readonly CommandLineFlag _autoRestartFlag = new CommandLineFlag(false, "-AutoRestartDevkitServerUpdates");
    private readonly CommandLineFloat _checkTimerCmd = new CommandLineFloat("-DevkitServerCheckUpdateInterval");
    private readonly CommandLineFloat _shutdownTimerCmd = new CommandLineFloat("-DevkitServerCheckUpdateShutdownDelay");

    private NuGetResource? _indexResource;
    private float _nuGetIndexExpiration = -1f;
    private DevkitServerLauncherLogger _logger = null!;

    public static DevkitServerAutoUpdateComponent? Instance { get; private set; }

    [UsedImplicitly]
    private void Awake()
    {
        if (Instance != null)
            Destroy(Instance);

        _logger = new DevkitServerLauncherLogger
        {
            ShouldLogDebug = false
        };
        
        Instance = this;
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    [UsedImplicitly]
    private void Start()
    {
        StartCoroutine(CheckForUpdates());
    }

    private IEnumerator CheckForUpdates()
    {
        _logger.LogInformation("Initialized DevkitServer auto-updater.");
        OnAutoUpdaterInitialized?.Invoke(this);

        while (true)
        {
            float checkTimer = _checkTimerCmd.hasValue ? _checkTimerCmd.value : CheckTimer;
            if (checkTimer <= 0f)
                checkTimer = CheckTimer <= 0f ? 120f : CheckTimer;
            
            yield return new WaitForSecondsRealtime(checkTimer);

            Assembly thisAssembly = Assembly.GetExecutingAssembly();

            Module thisModule = ModuleHook.modules.Find(x => Array.IndexOf(x.assemblies, thisAssembly) != -1) ??
                                throw new Exception("Unable to find module: DevkitServer.Launcher");

            string modulePath = thisModule.config.DirectoryPath;
            string packagePath = Path.Combine(modulePath, "Packages");

            if (_indexResource == null || (_nuGetIndexExpiration >= 0 && _nuGetIndexExpiration < Time.realtimeSinceStartup))
            {
                _logger.LogDebug("Downloading NuGet index...");
                using UnityWebRequest getIndexRequest = UnityWebRequest.Get(DevkitServerLauncherModule.NugetAPIIndex);
                yield return getIndexRequest.SendWebRequest();

                if (getIndexRequest.responseCode is 200 or 304 && getIndexRequest.downloadHandler?.data is { Length: > 0 })
                {
                    NuGetIndex? index = IOUtility.jsonDeserializer.deserialize<NuGetIndex>(getIndexRequest.downloadHandler.data, 0);

                    if (index?.Resources == null || (index.Version == null ? null : NuGetVersion.Parse(index.Version)) is not { Major: 3 })
                    {
                        _logger.LogError($"Invalid NuGet index receieved from: {DevkitServerLauncherModule.NugetAPIIndex}.");
                        if (_indexResource == null)
                            continue;
                        _nuGetIndexExpiration = Time.realtimeSinceStartup + CheckTimer;
                    }
                    else
                    {
                        _indexResource = Array.Find(index.Resources, x => string.Equals(x.Type, "PackageBaseAddress/3.0.0", StringComparison.Ordinal)) ??
                                         Array.Find(index.Resources, x => x.Type != null && x.Type.StartsWith("PackageBaseAddress", StringComparison.Ordinal));
                        if (getIndexRequest.GetResponseHeader("cache-control") is { } cacheControlHeader)
                        {
                            int startInd = cacheControlHeader.IndexOf("max-age=", StringComparison.OrdinalIgnoreCase);
                            float t = Time.realtimeSinceStartup;
                            _nuGetIndexExpiration = t + 21600f;
                            if (startInd != -1)
                            {
                                startInd += 8;
                                int endInd = cacheControlHeader.Length <= startInd ? -1 : cacheControlHeader.IndexOf(',', startInd);
                                if (endInd != -1 && float.TryParse(cacheControlHeader.Substring(startInd, endInd - startInd), NumberStyles.Any, CultureInfo.InvariantCulture, out float cacheDelay))
                                {
                                    _nuGetIndexExpiration = t + cacheDelay;
                                    _logger.LogDebug($"  Got cache delay: {cacheDelay}.");
                                }
                            }
                        }

                        if (_indexResource is not { Id.Length: > 0 })
                        {
                            _logger.LogError("Unable to find NuGet resource: PackageBaseAddress.");
                            continue;
                        }
                    }
                }
                else
                {
                    _logger.LogError($"Unable to get NuGet index from: {DevkitServerLauncherModule.NugetAPIIndex}.");
                    if (_indexResource == null)
                        continue;
                    _nuGetIndexExpiration = Time.realtimeSinceStartup + CheckTimer;
                }
            }
            

            const string rscPackageId = DevkitServerLauncherModule.ResourcePackageId;
            string mainPackageId = DevkitServerLauncherModule.MainPackageId;
            Uri getVersionsUri = new Uri(new Uri(_indexResource.Id), rscPackageId.ToLowerInvariant() + "/index.json");
            NuGetVersion? highestResourceVersion = null;

            _logger.LogDebug($"Getting versions for NuGet package: {rscPackageId} ({getVersionsUri}).");

            UnityWebRequest getResxVersionsRequest = UnityWebRequest.Get(getVersionsUri);
            UnityWebRequestAsyncOperation op = getResxVersionsRequest.SendWebRequest();

            getVersionsUri = new Uri(new Uri(_indexResource.Id), mainPackageId.ToLowerInvariant() + "/index.json");
            NuGetVersion? highestMainVersion = null;

            _logger.LogDebug($"Getting versions for NuGet package: {mainPackageId} ({getVersionsUri}).");

            UnityWebRequest getMainVersionsRequest = UnityWebRequest.Get(getVersionsUri);
            yield return getMainVersionsRequest.SendWebRequest();
            yield return op;

            if (getResxVersionsRequest.responseCode == 404L)
            {
                _logger.LogError($"Package {rscPackageId} not found.");
                goto main;
            }

            NuGetVersionsResponse? versions = IOUtility.jsonDeserializer.deserialize<NuGetVersionsResponse>(getResxVersionsRequest.downloadHandler.data, 0);

            if (versions?.Versions == null)
            {
                _logger.LogError($"Invalid versions response receieved from: {getVersionsUri}.");
                goto main;
            }

            getResxVersionsRequest.Dispose();
            NuGetVersion[] versionsRtn = new NuGetVersion[versions.Versions.Length];
            for (int i = 0; i < versions.Versions.Length; ++i)
                versionsRtn[i] = NuGetVersion.Parse(versions.Versions[i]);
            highestResourceVersion = versionsRtn.Max();
            
            _logger.LogDebug($"Highest available version for package {rscPackageId}: {highestResourceVersion}.");

            main:


            if (getMainVersionsRequest.responseCode == 404L)
            {
                _logger.LogError($"Package {mainPackageId} not found.");
                goto check;
            }

            versions = IOUtility.jsonDeserializer.deserialize<NuGetVersionsResponse>(getMainVersionsRequest.downloadHandler.data, 0);

            if (versions?.Versions == null)
            {
                _logger.LogError($"Invalid versions response receieved from: {getVersionsUri}.");
                goto check;
            }

            getMainVersionsRequest.Dispose();
            versionsRtn = new NuGetVersion[versions.Versions.Length];
            for (int i = 0; i < versions.Versions.Length; ++i)
                versionsRtn[i] = NuGetVersion.Parse(versions.Versions[i]);
            highestMainVersion = versionsRtn.Max();
            
            _logger.LogDebug($"Highest available version for package {mainPackageId}: {highestMainVersion}.");

            check:
            List<PackageIdentity>? updatesAvailable = null;

            DirectoryInfo dir = Directory.CreateDirectory(packagePath);
            if ((dir.Attributes & FileAttributes.Hidden) == 0)
                dir.Attributes |= FileAttributes.Hidden;

            if (highestResourceVersion != null)
            {
                string rscPath = Path.Combine(packagePath, rscPackageId);
                PackageIdentity? resourcesPackage = DevkitServerLauncherModule.ReadPackage(rscPath, _logger);
                SemanticVersion? oldVersion = resourcesPackage?.Version;
                if (oldVersion == null || oldVersion < highestResourceVersion)
                {
                    yield return DownloadPackage(highestResourceVersion, rscPackageId, rscPath);

                    resourcesPackage = DevkitServerLauncherModule.ReadPackage(rscPath, _logger);
                    if (resourcesPackage == null || resourcesPackage.Version < highestResourceVersion)
                    {
                        _logger.LogError($"Unable to update {rscPackageId}.");
                        goto main2;
                    }

                    (updatesAvailable = new List<PackageIdentity>(2)).Add(resourcesPackage);
                }
                else
                {
                    _logger.LogDebug($"{rscPackageId} is already up to date ({oldVersion}).");
                }
            }

            main2:
            if (highestMainVersion != null)
            {
                string mainPath = Path.Combine(packagePath, mainPackageId);
                PackageIdentity? mainPackage = DevkitServerLauncherModule.ReadPackage(mainPath, _logger);
                SemanticVersion? oldVersion = mainPackage?.Version;
                if (oldVersion == null || oldVersion < highestMainVersion)
                {
                    yield return DownloadPackage(highestMainVersion, mainPackageId, mainPath);

                    mainPackage = DevkitServerLauncherModule.ReadPackage(mainPath, _logger);
                    if (mainPackage == null || mainPackage.Version < highestMainVersion)
                    {
                        _logger.LogError($"Unable to update {mainPackageId}.");
                        goto apply;
                    }

                    (updatesAvailable ??= new List<PackageIdentity>(2)).Add(mainPackage);
                }
                else
                {
                    _logger.LogDebug($"{mainPackageId} is already up to date ({oldVersion}).");
                }
            }

            apply:
            if (updatesAvailable is { Count: > 0 })
            {
                string msg;
                if (updatesAvailable.Count == 1)
                    msg = $"Update available: {updatesAvailable[0].Id} -> {updatesAvailable[0].Version.ToNormalizedString()}.";
                else
                    msg = $"Updates available: {string.Join(" | ", updatesAvailable.Select(x => x.Id + " -> " + x.Version.ToNormalizedString()))}";

                _logger.LogInformation(msg);

                IReadOnlyList<PackageIdentity> packages = updatesAvailable.AsReadOnly();
                bool restart = _autoRestartFlag.value;
                OnUpdateReady?.Invoke(packages, ref restart);

                if (restart)
                {
                    float restartTimer = _shutdownTimerCmd.hasValue ? _shutdownTimerCmd.value : CheckTimer;
                    if (restartTimer is >= -1 and < 0)
                        restartTimer = 0f;
                    else if (restartTimer < 0f)
                        restartTimer = ShutdownTimer < 0f ? 120f : ShutdownTimer;

                    if (restartTimer == 0f)
                    {
                        if (Dedicator.isStandaloneDedicatedServer)
                            Provider.shutdown(0, msg);
                        else
                            Provider.QuitGame(msg);
                    }
                    else
                    {
                        _logger.LogWarning($"Shutting down in {restartTimer:0.#} second(s).");

                        if (Dedicator.isStandaloneDedicatedServer)
                        {
                            Provider.shutdown(Mathf.CeilToInt(restartTimer), msg);
                        }
                        else
                        {
                            yield return new WaitForSecondsRealtime(restartTimer);
                            Provider.QuitGame(msg);
                        }
                    }
                }

                Destroy(this);
                yield break;
            }
        }
    }
    private IEnumerator DownloadPackage(SemanticVersion version, string packageId, string path)
    {
        string v = version.ToNormalizedString();
        string id = packageId.ToLowerInvariant();
        Uri getNupkgUri = new Uri(new Uri(_indexResource!.Id), $"{id}/{v}/{id}.{v}.nupkg");

        _logger.LogInformation($"Downloading: {packageId} package (Version: {v})...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        using UnityWebRequest getNupkgRequest = UnityWebRequest.Get(getNupkgUri);
        yield return getNupkgRequest.SendWebRequest();
        stopwatch.Stop();
        _logger.LogInformation($"  Done ({stopwatch.ElapsedMilliseconds} ms).");

        if (getNupkgRequest.responseCode == 404L)
        {
            _logger.LogError($"Package {packageId} not found.");
            yield break;
        }

        byte[] bytes = getNupkgRequest.downloadHandler.data;

        using (FileStream stream = new FileStream(path + ".nupkg", FileMode.Create, FileAccess.Write, FileShare.Read))
            stream.Write(bytes, 0, bytes.Length);

        _logger.LogInformation($"Downloading: {packageId} symbols (Version: {v})...");
        getNupkgUri = new Uri($"https://globalcdn.nuget.org/symbol-packages/{id}.{v}.snupkg");
        stopwatch.Restart();
        using UnityWebRequest getSymNupkgRequest = UnityWebRequest.Get(getNupkgUri);
        yield return getSymNupkgRequest.SendWebRequest();
        stopwatch.Stop();

        try
        {
            _logger.LogInformation($"  Done ({stopwatch.ElapsedMilliseconds} ms).");

            if (getSymNupkgRequest.responseCode == 404L)
            {
                _logger.LogDebug($"Package {packageId} symbols not found.");
                string p = path + ".snupkg";
                if (File.Exists(p))
                    File.Delete(p);
                yield break;
            }

            bytes = getSymNupkgRequest.downloadHandler.data;

            using FileStream stream = new FileStream(path + ".snupkg", FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Unable to download symbol package for {packageId} from {getNupkgUri}. This is not a huge issue.");
            _logger.LogDebug(ex.ToString());
            try
            {
                string p = path + ".snupkg";
                if (File.Exists(p))
                    File.Delete(p);
            }
            catch
            {
                // ignored
            }
        }
    }
}

public delegate void UpdateReady(IReadOnlyList<PackageIdentity> packages, ref bool shouldAutoRestart);