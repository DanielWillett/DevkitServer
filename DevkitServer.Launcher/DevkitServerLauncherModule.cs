using DevkitServer.Launcher.Models;
using DevkitServer.Resources;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using SDG.Framework.IO;
using SDG.Framework.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine.Networking;
using ILogger = NuGet.Common.ILogger;
using Module = SDG.Framework.Modules.Module;
using Version = System.Version;

namespace DevkitServer.Launcher;

public class DevkitServerLauncherModule : IModuleNexus
{
    public const string ResourcePackageId = "DevkitServer.Resources";
    public const string NugetAPIIndex = "https://api.nuget.org/v3/index.json";
    private static GameObject? _dsLaunchHost;
    public static string MainPackageId => Dedicator.isStandaloneDedicatedServer ? "DevkitServer.Server" : "DevkitServer.Client";

    void IModuleNexus.initialize()
    {
        try
        {
            Load(new CommandLineFlag(false, "-ForceDevkitServerReinstall").value,
                new CommandLineFlag(false, "-DontUpdateDevkitServer").value,
                new CommandLineFlag(false, "-DontCheckForDevkitServerUpdates").value);
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("DevkitServer.Launcher threw an error.");
            CommandWindow.LogError(ex.ToString());
            throw new Exception("^ Unable to launch DevkitServer.Launcher. See above ^");
        }
    }
    void IModuleNexus.shutdown()
    {
        if (_dsLaunchHost != null)
        {
            Object.Destroy(_dsLaunchHost);
        }
    }
    private static void Load(bool forceReinstall, bool dontUpdate, bool dontCheckForUpdates)
    {
        string mainPackageId = MainPackageId;

        DevkitServerLauncherLogger logger = new DevkitServerLauncherLogger();
        Assembly thisAssembly = Assembly.GetExecutingAssembly();

        Module thisModule = ModuleHook.modules.Find(x => Array.IndexOf(x.assemblies, thisAssembly) != -1) ??
                            throw new Exception("Unable to find module: DevkitServer.Launcher");

        string modulePath = thisModule.config.DirectoryPath;
        string packagePath = Path.Combine(modulePath, "Packages");

        DirectoryInfo dir = Directory.CreateDirectory(packagePath);
        if ((dir.Attributes & FileAttributes.Hidden) == 0)
            dir.Attributes |= FileAttributes.Hidden;

        // discover existing modules

        Module? devkitServerModule = ModuleHook.modules.Find(x => x.config.Name.Equals("DevkitServer", StringComparison.Ordinal));
        AssemblyName? devkitServerAssembly = null;
        Version? devkitServerVersion = null;

        string mainModuleFolder = devkitServerModule != null ? devkitServerModule.config.DirectoryPath : Path.Combine(Path.GetDirectoryName(thisModule.config.DirectoryPath)!, "DevkitServer");

        Directory.CreateDirectory(mainModuleFolder);
        
        if (devkitServerModule == null)
        {
            logger.LogInformation("Did not find DevkitServer module, installing...");
        }
        else
        {
            // try to find the existing assembly
            EModuleRole role = Dedicator.isStandaloneDedicatedServer ? EModuleRole.Server : EModuleRole.Client;
            for (int i = devkitServerModule.config.Assemblies.Count - 1; i >= 0; i--)
            {
                ModuleAssembly assembly = devkitServerModule.config.Assemblies[i];
                string path = GetAbsolutePath(devkitServerModule.config, assembly);
                if (assembly.Role != role || !File.Exists(path))
                    continue;
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(path);
                    if (name.Name.Equals("DevkitServer", StringComparison.Ordinal))
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
                logger.LogInformation("Found existing DevkitServer module, but not the expected assembly. Installing...");
            }
            else
            {
                logger.LogInformation($"Found existing DevkitServer module: v {devkitServerVersion!.ToString(3)}. Checking for updates...");
            }
        }

        try
        {
            // query the v3 API index
            
            UnityWebRequest getIndexRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(NugetAPIIndex));
            NuGetIndex? index = IOUtility.jsonDeserializer.deserialize<NuGetIndex>(getIndexRequest.downloadHandler.data, 0);

            if (index?.Resources == null || (index.Version == null ? null : NuGetVersion.Parse(index.Version)) is not { Major: 3 })
            {
                logger.LogError($"Invalid NuGet index receieved from: {NugetAPIIndex}.");
                if (devkitServerAssembly != null)
                    goto main2;
                Break();
                return;
            }

            // get the PackageBaseAddress resource URL

            NuGetResource? packageBaseAddress = Array.Find(index.Resources, x => string.Equals(x.Type, "PackageBaseAddress/3.0.0", StringComparison.Ordinal)) ??
                                      Array.Find(index.Resources, x => x.Type != null && x.Type.StartsWith("PackageBaseAddress", StringComparison.Ordinal));

            if (packageBaseAddress == null)
            {
                logger.LogError("Unable to find NuGet resource: PackageBaseAddress.");
                if (devkitServerAssembly != null)
                    goto main2;
                Break();
                return;
            }

            // get the available versions for all the packages and calculate the highest available version

            NuGetVersion[]? resourcesVersions = GetVersions(logger, packageBaseAddress, ResourcePackageId);
            if (resourcesVersions is not { Length: > 0 })
            {
                if (devkitServerAssembly != null)
                    goto main2;
                Break();
                return;
            }

            NuGetVersion[]? mainVersions = GetVersions(logger, packageBaseAddress, mainPackageId);
            if (mainVersions is not { Length: > 0 })
            {
                if (devkitServerAssembly != null)
                    goto main2;
                Break();
                return;
            }

            NuGetVersion highestResourceVersion = resourcesVersions.Max();
            NuGetVersion highestMainVersion = mainVersions.Max();

            logger.LogInformation($"Found highest versions of packages: {mainPackageId}: {highestMainVersion}, {ResourcePackageId}: {highestResourceVersion}.");

            // Resources

            string rscPath = Path.Combine(packagePath, ResourcePackageId);
            PackageIdentity? resourcesPackage = ReadPackage(rscPath, logger);
            SemanticVersion? oldVersion = resourcesPackage?.Version;
            string resourcesDllPath = Path.Combine(modulePath, "Bin", "DevkitServer.Resources.dll");
            bool unpack = false;
            if (forceReinstall || oldVersion == null || (!dontUpdate && oldVersion < highestResourceVersion))
            {
                if (!DownloadPackage(logger, packageBaseAddress, highestResourceVersion, ResourcePackageId, rscPath))
                {
                    Break();
                    return;
                }
                resourcesPackage = ReadPackage(rscPath, logger);
                if (resourcesPackage == null || resourcesPackage.Version < highestResourceVersion)
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
                if (!CheckAtLeastIsVersion(resourcesDllPath, highestResourceVersion))
                    unpack = true;
            }
            
            if (unpack && !UnpackResourceAssembly(logger, rscPath, resourcesDllPath))
                goto main;

            Assembly.Load(File.ReadAllBytes(resourcesDllPath));

            WriteResources(logger, mainModuleFolder, oldVersion, forceReinstall);

            main:;

            // Main assembly

            string mainPath = Path.Combine(packagePath, mainPackageId);
            PackageIdentity? mainPackage = ReadPackage(mainPath, logger);
            oldVersion = mainPackage?.Version;
            string mainDllPath = Path.Combine(mainModuleFolder, "Bin", Dedicator.isStandaloneDedicatedServer ? "DevkitServer_Server.dll" : "DevkitServer_Client.dll");
            unpack = false;
            if (forceReinstall || oldVersion == null || (!dontUpdate && oldVersion < highestMainVersion))
            {
                if (!DownloadPackage(logger, packageBaseAddress, highestMainVersion, mainPackageId, mainPath))
                {
                    Break();
                    return;
                }
                mainPackage = ReadPackage(mainPath, logger);
                if (mainPackage == null || mainPackage.Version < highestMainVersion)
                {
                    logger.LogError($"Unable to update {mainPackageId}.");
                    if (mainPackage != null)
                        logger.LogInformation($"  It's still installed at version {oldVersion}.");
                    else
                    {
                        Break();
                        return;
                    }
                }
                else
                    unpack = true;
            }
            else
            {
                logger.LogInformation($"{mainPackageId} is already up to date ({oldVersion}).");
                if (!CheckAtLeastIsVersion(mainDllPath, highestMainVersion))
                    unpack = true;
            }

            if (unpack && !UnpackMainAssembly(logger, mainPath, mainDllPath))
            {
                Break();
                return;
            }

            if (devkitServerModule != null)
            {
                try
                {
                    string moduleConfigFile = devkitServerModule.config.FilePath;

                    AssemblyName asn = AssemblyName.GetAssemblyName(mainDllPath);
                    string v = Math.Min(asn.Version.Major, 255) + "." +
                               Math.Min(asn.Version.Minor, 255) + "." +
                               Math.Min(asn.Version.Build, 255) + ".0";
                    uint intl = Parser.getUInt32FromIP(v);
                    logger.LogDebug("Checking module file version.");
                    if (!File.Exists(moduleConfigFile) || intl != devkitServerModule.config.Version_Internal)
                    {
                        logger.LogDebug($"  Updating ({devkitServerModule.config.Version} -> {v}).");
                        devkitServerModule.config.Version = v;
                        devkitServerModule.config.Version_Internal = intl;
                        IOUtility.jsonSerializer.serialize(devkitServerModule.config, moduleConfigFile, true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Unable to update module config version.");
                    logger.LogWarning(ex.ToString());
                }
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
                    logger.LogError($"Unable to automatically install {mainPackageId}. Restart your {(Dedicator.isStandaloneDedicatedServer ? "server" : "game")} to load it. Closing in 10 seconds.");
                    Break();
                    return;
                }

                method.Invoke(moduleHook, new object[] { mainModuleFolder, modules });

                if (modules.Count < 1)
                {
                    logger.LogError($"Unable to discover {mainPackageId} module file. You may have to do a manual installation if restarting doesn't fix it.");
                    Break();
                    return;
                }

                int moduleIndex = ModuleHook.modules.IndexOf(thisModule);
                foreach (ModuleConfig config in modules)
                {
                    try
                    {
                        string configFile = config.FilePath;
                        if (File.Exists(configFile))
                        {
                            AssemblyName asn = AssemblyName.GetAssemblyName(mainDllPath);
                            string v = Math.Min(asn.Version.Major, 255) + "." +
                                       Math.Min(asn.Version.Minor, 255) + "." +
                                       Math.Min(asn.Version.Build, 255) + ".0";
                            uint intl = Parser.getUInt32FromIP(v);
                            logger.LogDebug("Checking module file version.");
                            if (intl != config.Version_Internal)
                            {
                                logger.LogDebug($"  Updating ({config.Version} -> {v}).");
                                config.Version = v;
                                config.Version_Internal = intl;
                                IOUtility.jsonSerializer.serialize(config, configFile, true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Unable to update module config version.");
                        logger.LogWarning(ex.ToString());
                    }

                    logger.LogInformation($"Registering new module: {config.Name} ({config.Version}).");
                    for (int i = config.Assemblies.Count - 1; i >= 0; i--)
                    {
                        ModuleAssembly assembly = config.Assemblies[i];
                        string path = GetAbsolutePath(config, assembly);
                        if (File.Exists(path))
                            continue;

                        config.Assemblies.RemoveAt(i);

                        if (!Path.GetFileName(path).Equals(Dedicator.isStandaloneDedicatedServer ? "DevkitServer_Client.dll" : "DevkitServer_Server.dll", StringComparison.Ordinal))
                        {
                            logger.LogError($"Missing assembly: \"{path}\".");
                            Break();
                            return;
                        }
                    }

                    ModuleHook.modules.Insert(++moduleIndex, new Module(config));
                }
            }

            if (unpack)
            {
                if (oldVersion != null)
                    logger.LogInformation($"DevkitServer updated from version {oldVersion} to {highestMainVersion} (with resources version {highestResourceVersion}).");
                else
                    logger.LogInformation($"DevkitServer version {highestMainVersion} installed (with resources version {highestResourceVersion}).");
            }

            main2:
            logger.LogInformation("Finished checking for updates.");
        }
        catch (Exception ex)
        {
            if (ModuleHook.getModuleByName("DevkitServer") is { } module)
            {
                logger.LogWarning($"Unable to check for updates for {module.config.Name} ({module.config.DirectoryPath}). An error occured ({ex.GetType().Name}).");
                logger.LogError(ex.ToString());
            }
            else
            {
                logger.LogWarning($"Unable to install {mainPackageId} from NuGet servers. An error occured ({ex.GetType().Name}).");
                logger.LogError(ex.ToString());
                Break();
                return;
            }
        }

        if (!dontCheckForUpdates)
        {
            _dsLaunchHost = new GameObject("DevkitServerLauncher", typeof(DevkitServerAutoUpdateComponent));
            Object.DontDestroyOnLoad(_dsLaunchHost);
        }
    }
    public static string GetAbsolutePath(ModuleConfig config, ModuleAssembly assembly)
    {
        string path = assembly.Path;
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        if (path[0] == Path.DirectorySeparatorChar || path[0] == Path.AltDirectorySeparatorChar)
            path = path.Substring(1);
        path = Path.Combine(config.DirectoryPath, path);
        return path;
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
        string? dir = Path.GetDirectoryName(dllPath);
        if (dir != null)
            Directory.CreateDirectory(dir);
        string basePath = Path.Combine(dir ?? string.Empty, Path.GetFileNameWithoutExtension(dllPath));
        using (FileStream stream = new FileStream(path + ".nupkg", FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using PackageArchiveReader reader = new PackageArchiveReader(stream, true);

            PackageIdentity id = reader.NuspecReader.GetIdentity();
            string? asmFile = null;
            string? xmlFile = null;
            foreach (string file in reader.GetLibItems().SelectMany(x => x.Items))
            {
                string fn = Path.GetFileName(file);
                if (fn.Equals("DevkitServer.Resources.dll", StringComparison.Ordinal))
                    asmFile = file;
                else if (fn.Equals("DevkitServer.Resources.xml", StringComparison.Ordinal))
                    xmlFile = file;
            }

            if (asmFile == null)
            {
                logger.LogError("Unable to find DevkitServer.Resources assembly in package.");
                return false;
            }

            logger.LogInformation($"Unpacking assembly: \"{id}://{asmFile}\" -> \"{dllPath}\".");
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

            if (xmlFile != null)
            {
                string p = basePath + ".xml";
                logger.LogInformation($"Unpacking xml documentation: \"{id}://{xmlFile}\" -> \"{p}\".");
                reader.ExtractFile(xmlFile, p, logger);

                try
                {
                    FileAttributes resxFileAttr = File.GetAttributes(p);
                    if ((resxFileAttr & FileAttributes.Hidden) == 0)
                        File.SetAttributes(p, resxFileAttr | FileAttributes.Hidden);
                }
                catch
                {
                    // ignored
                }
            }
        }

        if (!File.Exists(path + ".snupkg"))
            return true;

        using (FileStream stream = new FileStream(path + ".snupkg", FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using PackageArchiveReader reader = new PackageArchiveReader(stream, true);

            PackageIdentity id = reader.NuspecReader.GetIdentity();

            string? symFile = reader.GetLibItems().SelectMany(x => x.Items).FirstOrDefault(x => Path.GetFileName(x)
                .Equals("DevkitServer.Resources.pdb", StringComparison.Ordinal));

            if (symFile == null)
            {
                logger.LogWarning("Unable to find DevkitServer.Resources symbols file in package.");
                return true;
            }

            string p = basePath + ".pdb";
            logger.LogInformation($"Unpacking symbols: \"{id}://{symFile}\" -> \"{p}\".");
            reader.ExtractFile(symFile, p, logger);

            try
            {
                FileAttributes resxFileAttr = File.GetAttributes(p);
                if ((resxFileAttr & FileAttributes.Hidden) == 0)
                    File.SetAttributes(p, resxFileAttr | FileAttributes.Hidden);
            }
            catch
            {
                // ignored
            }
        }

        return true;
    }
    private static bool UnpackMainAssembly(ILogger logger, string path, string dllPath)
    {
        string? dir = Path.GetDirectoryName(dllPath);
        if (dir != null)
            Directory.CreateDirectory(dir);
        string basePath = Path.Combine(dir ?? string.Empty, Path.GetFileNameWithoutExtension(dllPath));
        using (FileStream stream = new FileStream(path + ".nupkg", FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using PackageArchiveReader reader = new PackageArchiveReader(stream, true);

            PackageIdentity id = reader.NuspecReader.GetIdentity();
            string? asmFile = null;
            string? xmlFile = null;
            foreach (string file in reader.GetLibItems().SelectMany(x => x.Items))
            {
                string fn = Path.GetFileName(file);
                if (fn.Equals("DevkitServer.dll", StringComparison.Ordinal))
                    asmFile = file;
                else if (fn.Equals("DevkitServer.xml", StringComparison.Ordinal))
                    xmlFile = file;
            }

            if (asmFile == null)
            {
                logger.LogError("Unable to find DevkitServer assembly in package.");
                return false;
            }

            logger.LogInformation($"Unpacking assembly: \"{id}://{asmFile}\" -> \"{dllPath}\".");
            reader.ExtractFile(asmFile, dllPath, logger);

            if (xmlFile != null)
            {
                string p = basePath + ".xml";
                logger.LogInformation($"Unpacking xml documentation: \"{id}://{xmlFile}\" -> \"{p}\".");
                reader.ExtractFile(xmlFile, p, logger);
            }
        }

        if (!File.Exists(path + ".snupkg"))
            return true;

        using (FileStream stream = new FileStream(path + ".snupkg", FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using PackageArchiveReader reader = new PackageArchiveReader(stream, true);

            PackageIdentity id = reader.NuspecReader.GetIdentity();

            string? symFile = reader.GetLibItems().SelectMany(x => x.Items).FirstOrDefault(x => Path.GetFileName(x)
                .Equals("DevkitServer.pdb", StringComparison.Ordinal));

            if (symFile == null)
            {
                logger.LogWarning("Unable to find DevkitServer symbols file in package.");
                return true;
            }

            string p = basePath + ".pdb";
            logger.LogInformation($"Unpacking symbols: \"{id}://{symFile}\" -> \"{p}\".");
            reader.ExtractFile(symFile, p, logger);
        }

        return true;
    }
    private static void WriteResources(ILogger logger, string modulesFolder, SemanticVersion? lastVersion, bool forceReinstall)
    {
        DevkitServerResources resources = new DevkitServerResources();

        Version oldVersion = lastVersion == null ? new Version(0, 0, 0, 0) : new Version(lastVersion.Major, lastVersion.Minor, lastVersion.Patch, 0);

        if (!forceReinstall)
            logger.LogInformation($"Checking for resources that have been updated since version {oldVersion}.");
        else
            logger.LogInformation($"Re-installing {resources.Resources.Count} resources.");
        foreach (IDevkitServerResource resource in resources.Resources)
        {
            bool applyingAnyways = false;
            if (!forceReinstall && resource.LastUpdated <= oldVersion && !(applyingAnyways = resource.ShouldApplyAnyways(modulesFolder)))
            {
                logger.LogDebug($"Resource up to date: {resource}.");
                continue;
            }

            if (forceReinstall)
                logger.LogInformation($"Installing resource {resource}.");
            else if (applyingAnyways)
                logger.LogInformation($"Installing missing resource {resource}.");
            else    
                logger.LogInformation($"Installing out of date resource {resource}.");

            resource.Apply(modulesFolder);
        }

        resources.ReleaseAll();
    }
    internal static PackageIdentity? ReadPackage(string path, ILogger logger)
    {
        path += ".nupkg";
        if (!File.Exists(path))
            return null;
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PackageArchiveReader reader = new PackageArchiveReader(stream, true);
        //try
        //{
        //    PrimarySignature? primarySignature = reader.GetPrimarySignatureAsync(CancellationToken.None).Result;

        //    if (primarySignature != null)
        //    {
        //        logger.LogInformation($"Validating signature of {Path.GetFileNameWithoutExtension(path)}...");
        //        try
        //        {
        //            reader.ValidateIntegrityAsync(primarySignature.SignatureContent, CancellationToken.None).Wait();
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.LogError($"  Unable to verify package signature at: \"{path}\".");
        //            logger.LogError(ex.ToString());
        //            return null;
        //        }
        //        logger.LogInformation("  Done.");
        //    }
        //    else
        //    {
        //        logger.LogInformation($"Skipping signature validation, {Path.GetFileNameWithoutExtension(path)} is not signed.");
        //    }
        //}
        //catch (Exception ex)
        //{
        //    logger.LogDebug(ex.InnerException?.Message ?? ex.Message);
        //}


        return reader.GetIdentity();
    }
    private static bool DownloadPackage(ILogger logger, NuGetResource packageBaseAddress, SemanticVersion version, string packageId, string path)
    {
        string v = version.ToNormalizedString();
        string id = packageId.ToLowerInvariant();
        Uri getNupkgUri = new Uri(new Uri(packageBaseAddress.Id), $"{id}/{v}/{id}.{v}.nupkg");

        logger.LogInformation($"Downloading: {packageId} package (Version: {v})...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        using UnityWebRequest getNupkgRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(getNupkgUri));
        stopwatch.Stop();
        logger.LogInformation($"  Done ({stopwatch.ElapsedMilliseconds} ms).");

        if (getNupkgRequest.responseCode == 404L)
        {
            logger.LogError($"Package {packageId} not found.");
            return false;
        }

        byte[] bytes = getNupkgRequest.downloadHandler.data;

        using (FileStream stream = new FileStream(path + ".nupkg", FileMode.Create, FileAccess.Write, FileShare.Read))
            stream.Write(bytes, 0, bytes.Length);

        try
        {
            logger.LogInformation($"Downloading: {packageId} symbols (Version: {v})...");
            getNupkgUri = new Uri($"https://globalcdn.nuget.org/symbol-packages/{id}.{v}.snupkg");
            stopwatch.Restart();
            using UnityWebRequest getSymNupkgRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(getNupkgUri));
            stopwatch.Stop();
            logger.LogInformation($"  Done ({stopwatch.ElapsedMilliseconds} ms).");

            if (getSymNupkgRequest.responseCode == 404L)
            {
                logger.LogDebug($"Package {packageId} symbols not found.");
                string p = path + ".snupkg";
                if (File.Exists(p))
                    File.Delete(p);
                return true;
            }

            bytes = getSymNupkgRequest.downloadHandler.data;

            using FileStream stream = new FileStream(path + ".snupkg", FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogDebug($"Unable to download symbol package for {packageId} from {getNupkgUri}. This is not a huge issue.");
            logger.LogDebug(ex.ToString());
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

        return true;
    }
    private static NuGetVersion[]? GetVersions(ILogger logger, NuGetResource packageBaseAddress, string packageId)
    {
        Uri getVersionsUri = new Uri(new Uri(packageBaseAddress.Id), packageId.ToLowerInvariant() + "/index.json");

        logger.LogInformation($"Getting versions for NuGet package: {packageId} ({getVersionsUri}).");
        UnityWebRequest getVersionsRequest = ExecuteUnityWebRequestSync(logger, UnityWebRequest.Get(getVersionsUri));
        if (getVersionsRequest.responseCode == 404L)
        {
            logger.LogError($"Package {packageId} not found.");
            return null;
        }
        
        NuGetVersionsResponse? versions = IOUtility.jsonDeserializer.deserialize<NuGetVersionsResponse>(getVersionsRequest.downloadHandler.data, 0);

        if (versions?.Versions == null)
        {
            logger.LogError($"Invalid versions response receieved from: {getVersionsUri}.");
            logger.LogInformationSummary(getVersionsRequest.downloadHandler.text);
            return null;
        }
        
        getVersionsRequest.Dispose();
        NuGetVersion[] versionsRtn = new NuGetVersion[versions.Versions.Length];
        for (int i = 0; i < versions.Versions.Length; ++i)
            versionsRtn[i] = NuGetVersion.Parse(versions.Versions[i]);
        return versionsRtn;
    }
    private static UnityWebRequest ExecuteUnityWebRequestSync(ILogger logger, UnityWebRequest request, bool retry = true)
    {
        request.SetRequestHeader("User-Agent", "DevkitServer.Launcher/" + typeof(DevkitServerLauncherModule).Assembly.GetName().Version.ToString(3));
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
        request.SetRequestHeader("User-Agent", "DevkitServer.Launcher/" + typeof(DevkitServerLauncherModule).Assembly.GetName().Version.ToString(3));
        operation = request.SendWebRequest();

        while (!operation.isDone)
            Thread.Sleep(50);

        err = request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.DataProcessingError or UnityWebRequest.Result.ProtocolError;

        rtn:
        return !err ? request : throw new Exception(request.error);
    }
    private static bool CheckAtLeastIsVersion(string filePath, SemanticVersion version)
    {
        if (!File.Exists(filePath))
            return false;
        try
        {
            AssemblyName name = AssemblyName.GetAssemblyName(filePath);
            if (name == null)
                return false;
            Version v = name.Version;
            if (v.Major > version.Major)
                return true;
            if (v.Major < version.Major)
                return false;
            if (v.Minor > version.Minor)
                return true;
            if (v.Minor < version.Minor)
                return false;
            if (v.Build > version.Patch)
                return true;
            
            return v.Build >= version.Patch;
        }
        catch
        {
            return false;
        }
    }
}
