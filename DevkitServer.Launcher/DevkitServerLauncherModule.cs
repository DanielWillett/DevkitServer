using Cysharp.Threading.Tasks;
using DevkitServer.Launcher.Models;
using DevkitServer.Resources;
using NuGet.Packaging;
using NuGet.Versioning;
using SDG.Framework.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using UnityEngine.Networking;
using ILogger = NuGet.Common.ILogger;
using Module = SDG.Framework.Modules.Module;
using NuGetVersionConverter = DevkitServer.Launcher.Models.NuGetVersionConverter;
using Version = System.Version;

namespace DevkitServer.Launcher;

public class DevkitServerLauncherModule : IModuleNexus
{
    public const string ResourcePackageId = "DevkitServer.Resources";
    public const string PackageId = "DevkitServer";
    private const string NugetAPIIndex = "https://api.nuget.org/v3/index.json";
    private readonly CommandLineFlag _forceReinstall = new CommandLineFlag(false, "ForceDevkitServerReinstall");
    void IModuleNexus.initialize()
    {
        try
        {
            Load(_forceReinstall.value);
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("DevkitServer.Launcher threw an error.");
            CommandWindow.LogError(ex.ToString());
            throw new Exception();
        }
    }
    void IModuleNexus.shutdown()
    {

    }
    private static void Load(bool forceReinstall)
    {
        ILogger logger = new DevkitServerLauncherLogger();
        logger.LogInformation("Checking for updates...");
        Assembly thisAssembly = Assembly.GetExecutingAssembly();

        Module thisModule = ModuleHook.modules.Find(x => Array.IndexOf(x.assemblies, thisAssembly) != -1) ??
                            throw new Exception("Unable to find module: DevkitServer.Launcher");

        string modulePath = thisModule.config.DirectoryPath;
        string packagePath = Path.Combine(modulePath, "Packages");

        DirectoryInfo dir = Directory.CreateDirectory(packagePath);
        if ((dir.Attributes & FileAttributes.Hidden) == 0)
            dir.Attributes |= FileAttributes.Hidden;

        Module? devkitServerModule = ModuleHook.modules.Find(x => Path.GetFileName(x.config.DirectoryPath).Equals(PackageId, StringComparison.Ordinal));
        AssemblyName? devkitServerAssembly = null;
        Version? devkitServerVersion = null;

        string mainModuleFolder = devkitServerModule != null ? devkitServerModule.config.DirectoryPath : Path.Combine(Path.GetDirectoryName(thisModule.config.DirectoryPath)!, PackageId);

        Directory.CreateDirectory(mainModuleFolder);

        logger.LogDebug($"Modules folder: \"{mainModuleFolder}\".");

        if (devkitServerModule == null)
        {
            logger.LogInformation("Did not find DevkitServer module.");
        }
        else
        {
            EModuleRole role = Dedicator.isStandaloneDedicatedServer ? EModuleRole.Server : EModuleRole.Client;
            for (int i = devkitServerModule.config.Assemblies.Count - 1; i >= 0; i--)
            {
                ModuleAssembly assembly = devkitServerModule.config.Assemblies[i];
                if (assembly.Role != role)
                    continue;
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(assembly.Path);
                    if (name.Name.Equals(PackageId, StringComparison.Ordinal))
                    {
                        devkitServerAssembly = name;
                        devkitServerVersion = name.Version;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Invalid assembly, skipping check: \"{assembly.Path}\".");
                    logger.LogWarning(ex.ToString());
                }
            }

            if (devkitServerAssembly == null)
            {
                logger.LogInformation("Found existing DevkitServer module, but not the expected assembly.");
            }
            else
            {
                logger.LogInformation($"Found existing DevkitServer module: v {devkitServerVersion}.");
            }
        }

        try
        {
            JsonSerializerOptions jsonOptions = new JsonSerializerOptions
            {
                Converters = { new NuGetVersionConverter() },
                WriteIndented = true
            };

            logger.LogInformation($"Getting NuGet index from: {NugetAPIIndex}.");
            UnityWebRequest getIndexRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(NugetAPIIndex));
            Utf8JsonReader jsonReader = new Utf8JsonReader(getIndexRequest.downloadHandler.data);
            NuGetIndex? index = JsonSerializer.Deserialize<NuGetIndex>(ref jsonReader, jsonOptions);

            if (index?.Resources == null || index.Version is not { Major: 3 })
            {
                logger.LogError($"Invalid NuGet index receieved from: {NugetAPIIndex}.");
                if (devkitServerModule != null)
                    goto main2;
                Break();
                return;
            }

            NuGetResource? packageBaseAddress = Array.Find(index.Resources, x => string.Equals(x.Type, "PackageBaseAddress/3.0.0", StringComparison.Ordinal)) ??
                                      Array.Find(index.Resources, x => x.Type != null && x.Type.StartsWith("PackageBaseAddress", StringComparison.Ordinal));

            if (packageBaseAddress == null)
            {
                logger.LogError("Unable to find NuGet resource: PackageBaseAddress.");
                if (devkitServerModule != null)
                    goto main2;
                Break();
                return;
            }

            NuGetVersion[]? resourcesVersions = GetVersions(logger, packageBaseAddress, ResourcePackageId, jsonOptions);
            if (resourcesVersions == null)
            {
                if (devkitServerModule != null)
                    goto main2;
                Break();
                return;
            }

            NuGetVersion highestResourceVersion = resourcesVersions.Max();

            NuGetVersion[]? mainVersions = GetVersions(logger, packageBaseAddress, PackageId, jsonOptions);
            if (mainVersions == null)
            {
                if (devkitServerModule != null)
                    goto main2;
                Break();
                return;
            }

            NuGetVersion highestMainVersion = mainVersions.Max();

            string rscPath = Path.Combine(packagePath, ResourcePackageId + ".nupkg");
            NuspecReader? resourcesPackage = ReadPackage(rscPath);
            SemanticVersion? oldVersion = resourcesPackage?.GetVersion();
            string resourcesDllPath = Path.Combine(modulePath, "Bin", "DevkitServer.Resources.dll");
            bool unpack = false;
            if (forceReinstall || oldVersion == null || oldVersion < highestResourceVersion)
            {
                if (!DownloadPackage(logger, packageBaseAddress, highestResourceVersion, ResourcePackageId, rscPath))
                {
                    Break();
                    return;
                }
                resourcesPackage = ReadPackage(rscPath);
                if (resourcesPackage == null || resourcesPackage.GetVersion() < highestResourceVersion)
                {
                    logger.LogError($"Unable to update {ResourcePackageId}.");
                    if (resourcesPackage != null)
                        logger.LogInformation($"  It's still installed at version {oldVersion}.");
                    else
                    {
                        Break();
                        return;
                    }
                    
                    goto main;
                }

                unpack = true;
            }
            else
            {
                logger.LogInformation($"{ResourcePackageId} is already up to date ({oldVersion}).");
                if (!File.Exists(resourcesDllPath))
                    unpack = true;
            }

            if (unpack && !UnpackResourceAssembly(logger, rscPath, resourcesDllPath))
                goto main;

            Assembly.Load(File.ReadAllBytes(resourcesDllPath));

            WriteResources(logger, mainModuleFolder, oldVersion);

            main:;
            string mainPath = Path.Combine(packagePath, PackageId + ".nupkg");
            NuspecReader? mainPackage = ReadPackage(mainPath);
            oldVersion = mainPackage?.GetVersion();
            string mainDllPath = Path.Combine(mainModuleFolder, "Bin", Dedicator.isStandaloneDedicatedServer ? "DevkitServer_Server.dll" : "DevkitServer_Client.dll");
            unpack = false;
            if (forceReinstall || oldVersion == null || oldVersion < highestMainVersion)
            {
                if (!DownloadPackage(logger, packageBaseAddress, highestMainVersion, PackageId, mainPath))
                {
                    Break();
                    return;
                }
                mainPackage = ReadPackage(mainPath);
                if (mainPackage == null || mainPackage.GetVersion() < highestMainVersion)
                {
                    logger.LogError($"Unable to update {PackageId}.");
                    if (resourcesPackage != null)
                        logger.LogInformation($"  It's still installed at version {oldVersion}.");
                    else
                    {
                        Break();
                        return;
                    }

                    goto main2;
                }

                unpack = true;
            }
            else
            {
                logger.LogInformation($"{PackageId} is already up to date ({oldVersion}).");
                if (!File.Exists(mainDllPath))
                    unpack = true;
            }

            if (unpack && !UnpackMainAssembly(logger, mainPath, mainDllPath))
            {
                Break();
                return;
            }

            if (devkitServerModule == null)
            {
                List<ModuleConfig> modules = new List<ModuleConfig>();
                MethodInfo? method = typeof(ModuleHook).GetMethod("findModules",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                    new Type[] { typeof(string), typeof(List<ModuleConfig>) }, null);

                FieldInfo? providerInstance = typeof(Provider).GetField("steam", BindingFlags.NonPublic | BindingFlags.Static);

                if (method == null || providerInstance == null || providerInstance.GetValue(null) is not Provider provider || provider == null || !provider.gameObject.TryGetComponent(out ModuleHook moduleHook))
                {
                    logger.LogError($"Unable to automatically install {PackageId}. Restart your {(Dedicator.isStandaloneDedicatedServer ? "server" : "game")} to load it. Closing in 10 seconds.");
                    Break();
                    return;
                }

                method.Invoke(moduleHook, new object[] { mainModuleFolder, modules });

                if (modules.Count < 1)
                {
                    logger.LogError($"Unable to discover {PackageId} module file. You may have to do a manual installation if restarting doesn't fix it.");
                    Break();
                    return;
                }

                int moduleIndex = ModuleHook.modules.IndexOf(thisModule);
                foreach (ModuleConfig config in modules)
                {
                    logger.LogInformation($"Registering new module: {config.Name} ({config.Version}).");
                    ModuleHook.modules.Insert(++moduleIndex, new Module(config));
                }
            }

            if (oldVersion != null)
                logger.LogInformation($"DevkitServer updated from version {oldVersion} to {highestMainVersion} (with resources version {highestResourceVersion}).");
            else
                logger.LogInformation($"DevkitServer version {highestMainVersion} installed (with resources version {highestResourceVersion}).");

            main2:
            logger.LogInformation("Finished checking for updates.");
        }
        catch (Exception ex)
        {
            if (ModuleHook.getModuleByName(PackageId) is { } module)
            {
                logger.LogWarning($"Unable to check for updates for {module.config.Name} ({module.config.DirectoryPath}). An error occured ({ex.GetType().Name}).");
                logger.LogError(ex.ToString());
            }
            else
            {
                logger.LogWarning($"Unable to install {PackageId} from NuGet servers. An error occured ({ex.GetType().Name}).");
                logger.LogError(ex.ToString());
                Break();
            }
        }
    }
    private static void Break()
    {
        Thread.Sleep(10000);
        Application.Quit();
        for (int i = 0; i < ModuleHook.modules.Count; ++i)
        {
            ModuleHook.modules[i].config.IsEnabled = false;
        }
    }
    private static bool UnpackResourceAssembly(ILogger logger, string path, string dllPath)
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PackageArchiveReader reader = new PackageArchiveReader(stream, true);
        
        string? asmFile = reader.GetLibItems().SelectMany(x => x.Items).FirstOrDefault(x => Path.GetFileName(x).Equals("DevkitServer.Resources.dll", StringComparison.Ordinal));
        if (asmFile == null)
        {
            logger.LogError("Unable to find resources file in package.");
            return false;
        }

        reader.ExtractFile(asmFile, dllPath, logger);

        try
        {
            FileAttributes resxFileAttr = File.GetAttributes(dllPath);
            if ((resxFileAttr & FileAttributes.Hidden) == 0)
                File.SetAttributes(dllPath, resxFileAttr | FileAttributes.Hidden);
        }
        catch
        {
            // ignored
        }

        return true;
    }
    private static bool UnpackMainAssembly(ILogger logger, string path, string dllPath)
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PackageArchiveReader reader = new PackageArchiveReader(stream, true);
        
        string? asmFile = reader.GetLibItems().SelectMany(x => x.Items).FirstOrDefault(x => Path.GetFileName(x)
            .Equals(Dedicator.isStandaloneDedicatedServer ? "DevkitServer_Server.dll" : "DevkitServer_Client.dll", StringComparison.Ordinal));

        if (asmFile == null)
        {
            logger.LogError("Unable to find resources file in package.");
            return false;
        }

        reader.ExtractFile(asmFile, dllPath, logger);

        return true;
    }
    private static void WriteResources(ILogger logger, string modulesFolder, SemanticVersion? lastVersion)
    {
        DevkitServerResources resources = new DevkitServerResources();

        Version oldVersion = lastVersion == null ? new Version(0, 0, 0, 0) : new Version(lastVersion.Major, lastVersion.Minor, lastVersion.Patch, 0);

        logger.LogDebug($"Checking for resources that have been updated since version {oldVersion}.");
        foreach (IDevkitServerResource resource in resources.Resources)
        {
            if (resource.LastUpdated <= oldVersion)
            {
                logger.LogDebug($"Resource up to date: {resource} (Last updated in version {resource.LastUpdated.ToString(3)}).");
                continue;
            }

            resource.Apply(modulesFolder);
        }

        resources.ReleaseAll();
    }
    private static NuspecReader? ReadPackage(string path)
    {
        if (!File.Exists(path))
            return null;
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PackageArchiveReader reader = new PackageArchiveReader(stream, true);
        NuspecReader nuspec = reader.NuspecReader;
        _ = nuspec.GetVersion(); // cache stuff
        return nuspec;
    }
    private static bool DownloadPackage(ILogger logger, NuGetResource packageBaseAddress, SemanticVersion version, string packageId, string path)
    {
        string v = version.ToNormalizedString();
        string id = packageId.ToLowerInvariant();
        Uri getNupkgUri = new Uri(new Uri(packageBaseAddress.Id), $"{id}/{v}/{id}.{v}.nupkg");

        logger.LogInformation($"Downloading: {packageId} (Version: {v})...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        UnityWebRequest getNupkgRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(getNupkgUri));
        stopwatch.Stop();
        logger.LogInformation($"  Done ({stopwatch.ElapsedMilliseconds} ms).");

        if (getNupkgRequest.responseCode == 404L)
        {
            logger.LogError($"Package {packageId} not found.");
            return false;
        }

        byte[] bytes = getNupkgRequest.downloadHandler.data;

        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            stream.Write(bytes, 0, bytes.Length);

        getNupkgRequest.Dispose();
        return true;
    }
    private static NuGetVersion[]? GetVersions(ILogger logger, NuGetResource packageBaseAddress, string packageId, JsonSerializerOptions jsonOptions)
    {
        Uri getVersionsUri = new Uri(new Uri(packageBaseAddress.Id), packageId.ToLowerInvariant() + "/index.json");

        logger.LogInformation($"Getting versions for NuGet package: {packageId} ({getVersionsUri}).");
        UnityWebRequest getVersionsRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(getVersionsUri));
        if (getVersionsRequest.responseCode == 404L)
        {
            logger.LogError($"Package {packageId} not found.");
            return null;
        }

        Utf8JsonReader jsonReader = new Utf8JsonReader(getVersionsRequest.downloadHandler.data);
        NuGetVersionsResponse? versions = JsonSerializer.Deserialize<NuGetVersionsResponse>(ref jsonReader, jsonOptions);

        if (versions?.Versions == null)
        {
            logger.LogError($"Invalid versions response receieved from: {getVersionsUri}.");
            logger.LogInformationSummary(getVersionsRequest.downloadHandler.text);
            return null;
        }

        logger.LogInformation($"Found versions of {packageId}: {string.Join<NuGetVersion>(", ", versions.Versions)}.");
        getVersionsRequest.Dispose();
        return versions.Versions;
    }
    private static UnityWebRequest ExecuteUnityWebRequestSync(ILogger logger, UnityWebRequest request, bool retry = true)
    {
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        while (!operation.isDone)
            Thread.Sleep(50);
        bool err = request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.DataProcessingError or UnityWebRequest.Result.ProtocolError && request.responseCode != 404L;

        if (!err)
            return request;
        logger.LogWarning($"Error with request {request.method} {request.uri}... Resp: {request.responseCode}, \"{request.downloadHandler?.text}\".");

        if (!retry)
            goto rtn;

        logger.LogInformation("Retrying in 5 seconds...");
        Thread.Sleep(5000);

        request = new UnityWebRequest(request.uri, request.method, request.downloadHandler, request.uploadHandler);
        operation = request.SendWebRequest();

        while (!operation.isDone)
            Thread.Sleep(50);

        err = request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.DataProcessingError or UnityWebRequest.Result.ProtocolError;

        rtn:
        return !err ? request : throw new UnityWebRequestException(request);
    }
}
