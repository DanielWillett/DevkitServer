using System;
using System.Collections.Generic;
using System.IO;
using Version = System.Version;

namespace DevkitServer.Resources;
internal class DevkitServerResources
{
    public void ReleaseAll()
    {
        Properties.Resources.ResourceManager.ReleaseAllResources();
        Properties.Resources.resourceMan = null;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
    }

    private static readonly Version LastBundleUpdate = new Version(0, 0, 40, 0);

    /*
     * Defines all resource files and the last time (DevkitServer.Resources version) they were updated.
     */

    public List<IDevkitServerResource> Resources = new List<IDevkitServerResource>
    {
        // Directories
        new DevkitServerDirectoryResource("Bin", new Version(0, 0, 15, 0)),
        new DevkitServerDirectoryResource("Defaults", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Schemas", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Bundles", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Data", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Libraries", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource(Path.Combine("Libraries", "ForwardingTargets"), new Version(0, 0, 18, 0)),

        // Defaults
        new DevkitServerFileResource(Path.Combine("Defaults", "client_config.json"), new Version(0, 0, 37, 0)) { Side = Side.Client },
        new DevkitServerFileResource(Path.Combine("Defaults", "server_config.json"), new Version(0, 0, 38, 0)) { Side = Side.Server },
        new DevkitServerFileResource(Path.Combine("Defaults", "backup_config.json"), new Version(0, 0, 28, 0)),
        new DevkitServerFileResource(Path.Combine("Defaults", "permission_groups.json"), new Version(0, 0, 15, 0)) { Side = Side.Server },
        new DevkitServerFileResource(Path.Combine("Defaults", "chart_colors.json"), new Version(0, 0, 27, 0)),
        new DevkitServerFileResource(Path.Combine("Defaults", "cartography_config.json"), new Version(0, 0, 39, 0)),

        // Schemas
        new DevkitServerFileResource(Path.Combine("Schemas", "client_config_schema.json"), new Version(0, 0, 41, 0)) { Side = Side.Client },
        new DevkitServerFileResource(Path.Combine("Schemas", "server_config_schema.json"), new Version(0, 0, 41, 0)) { Side = Side.Server },
        new DevkitServerFileResource(Path.Combine("Schemas", "backup_schema.json"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource(Path.Combine("Schemas", "permission_groups_schema.json"), new Version(0, 0, 41, 0)) { Side = Side.Server },
        new DevkitServerFileResource(Path.Combine("Schemas", "chart_colors_schema.json"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource(Path.Combine("Schemas", "cartography_config_schema.json"), new Version(0, 0, 42, 0)),
        new DevkitServerFileResource(Path.Combine("Schemas", "cartography_compositor_pipeline_schema.json"), new Version(0, 0, 42, 0)),

        // Root
        new DevkitServerFileResource(@"DevkitServer.module", new Version(0, 0, 17, 0)),
        new DevkitServerFileResource(@"English.dat", new Version(0, 0, 1, 0)),
        
        // Bundles
        new DevkitServerFileResource(Path.Combine("Bundles", "MasterBundle.dat"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource("devkitserver.masterbundle", Path.Combine("Bundles", "devkitserver.masterbundle"), LastBundleUpdate),
        new DevkitServerFileResource("devkitserver.masterbundle.hash", Path.Combine("Bundles", "devkitserver.masterbundle.hash"), LastBundleUpdate),
        new DevkitServerFileResource("devkitserver.masterbundle.manifest", Path.Combine("Bundles", "devkitserver.masterbundle.manifest"), LastBundleUpdate),
        new DevkitServerFileResource("devkitserver_linux.masterbundle", Path.Combine("Bundles", "devkitserver_linux.masterbundle"), LastBundleUpdate),
        new DevkitServerFileResource("devkitserver_linux.masterbundle.manifest", Path.Combine("Bundles", "devkitserver_linux.masterbundle.manifest"), LastBundleUpdate),
        new DevkitServerFileResource("devkitserver_mac.masterbundle", Path.Combine("Bundles", "devkitserver_mac.masterbundle"), LastBundleUpdate),
        new DevkitServerFileResource("devkitserver_mac.masterbundle.manifest", Path.Combine("Bundles", "devkitserver_mac.masterbundle.manifest"), LastBundleUpdate),

        // Data
        new DevkitServerFileResource(Path.Combine("Data", "Object Icon Presets (Vanilla).json"), new Version(0, 0, 15, 0)),
        new DevkitServerFileResource("DevkitServer.LICENSE", Path.Combine("Bin", "LICENSE.txt"), new Version(0, 0, 16, 0)),

        // Legacy Libraries (Deleted)
        new DevkitServerFileResource(Lib("Library Info.txt"), new Version(0, 0, 15, 0)) { Delete = true },

        new DevkitServerFileResource(Lib("0Harmony.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("0Harmony.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("Microsoft.Bcl.AsyncInterfaces.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("Microsoft.Bcl.AsyncInterfaces.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("StackCleaner.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource("StackCleaner.pdb", Lib("StackCleaner.pdb"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource("StackCleaner.xml", Lib("StackCleaner.xml"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("StackCleaner.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Buffers.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Buffers.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.IO.Compression.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.IO.Compression.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.IO.Compression.FileSystem.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.IO.Compression.FileSystem.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Memory.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Memory.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Numerics.Vectors.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Numerics.Vectors.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Runtime.CompilerServices.Unsafe.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Runtime.CompilerServices.Unsafe.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Text.Encodings.Web.dll"), new Version(0, 0, 1, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Text.Encodings.Web.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Text.Json.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Text.Json.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Threading.Tasks.Extensions.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.Threading.Tasks.Extensions.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.ValueTuple.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("System.ValueTuple.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("UniTask.dll"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource("UniTask.pdb", Lib("UniTask.pdb"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource("UniTask.xml", Lib("UniTask.xml"), new Version(0, 0, 15, 0)) { Delete = true },
        new DevkitServerFileResource(Lib("UniTask.dll license.txt"), new Version(0, 0, 15, 0)) { Delete = true },

        // New Libraries

        new DevkitServerFileResource("Lib.Harmony",
            Lib("Lib.Harmony", "0Harmony.dll"), new Version(0, 0, 29, 0)),
        new DevkitServerFileResource("Lib.Harmony.LICENSE",
            Lib("Lib.Harmony", "LICENSE.txt"), new Version(0, 0, 29, 0)),

        // legacy harmony files
        new DevkitServerFileResource("Lib.Harmony.README",
            Lib("Lib.Harmony", "README.md"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("Lib.Harmony.XmlDocs",
            Lib("Lib.Harmony", "0Harmony.xml"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("Lib.Harmony.Dependencies",
            Lib("Lib.Harmony", "0Harmony.deps.json"), new Version(0, 0, 29, 0)) { Delete = true },

        new DevkitServerFileResource("Mono.Cecil",
            Lib("Lib.Harmony", "Mono.Cecil", "Mono.Cecil.dll"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("Mono.Cecil.LICENSE",
            Lib("Lib.Harmony", "Mono.Cecil", "LICENSE.txt"), new Version(0, 0, 29, 0)) { Delete = true },

        new DevkitServerFileResource("MonoMod.Common",
            Lib("Lib.Harmony", "MonoMod.Common", "MonoMod.Common.dll"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("MonoMod.Common.LICENSE",
            Lib("Lib.Harmony", "MonoMod.Common", "LICENSE.txt"), new Version(0, 0, 29, 0)) { Delete = true },

        new DevkitServerFileResource("System.Reflection.Emit.ILGeneration",
            Lib("Lib.Harmony", "System.Reflection.Emit.ILGeneration", "System.Reflection.Emit.ILGeneration.dll"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("System.Reflection.Emit.ILGeneration.LICENSE",
            Lib("Lib.Harmony", "System.Reflection.Emit.ILGeneration", "LICENSE.txt"), new Version(0, 0, 29, 0)) { Delete = true },

        new DevkitServerFileResource("System.Reflection.Emit.Lightweight",
            Lib("Lib.Harmony", "System.Reflection.Emit.Lightweight", "System.Reflection.Emit.Lightweight.dll"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("System.Reflection.Emit.Lightweight.LICENSE",
            Lib("Lib.Harmony", "System.Reflection.Emit.Lightweight", "LICENSE.txt"), new Version(0, 0, 29, 0)) { Delete = true },

        new DevkitServerFileResource("netstandard",
            Lib(".NET Standard 2.1", "netstandard.dll"), new Version(0, 0, 23, 0)),
        new DevkitServerFileResource("netstandard.LICENSE",
            Lib(".NET Standard 2.1", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("DanielWillett.StackCleaner",
            Lib("DanielWillett.StackCleaner", "DanielWillett.StackCleaner.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("DanielWillett.StackCleaner.LICENSE",
            Lib("DanielWillett.StackCleaner", "LICENSE.txt"), new Version(0, 0, 15, 0)),
        new DevkitServerFileResource("DanielWillett.StackCleaner.XmlDocs",
            Lib("DanielWillett.StackCleaner", "DanielWillett.StackCleaner.xml"), new Version(0, 0, 15, 0)),
        new DevkitServerFileResource("DanielWillett.StackCleaner.Symbols",
            Lib("DanielWillett.StackCleaner", "DanielWillett.StackCleaner.pdb"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("DanielWillett.ReflectionTools",
            Lib("DanielWillett.ReflectionTools", "DanielWillett.ReflectionTools.dll"), new Version(0, 0, 40, 0)),
        new DevkitServerFileResource("DanielWillett.ReflectionTools.Harmony",
            Lib("DanielWillett.ReflectionTools", "DanielWillett.ReflectionTools.Harmony.dll"), new Version(0, 0, 40, 0)),
        new DevkitServerFileResource("DanielWillett.ReflectionTools.LICENSE",
            Lib("DanielWillett.ReflectionTools", "LICENSE.txt"), new Version(0, 0, 29, 0)),

        new DevkitServerFileResource("DanielWillett.SpeedBytes",
            Lib("DanielWillett.SpeedBytes", "DanielWillett.SpeedBytes.dll"), new Version(0, 0, 36, 0)),
        new DevkitServerFileResource("DanielWillett.SpeedBytes.Unity",
            Lib("DanielWillett.SpeedBytes", "DanielWillett.SpeedBytes.Unity.dll"), new Version(0, 0, 36, 0)),
        new DevkitServerFileResource("DanielWillett.SpeedBytes.LICENSE",
            Lib("DanielWillett.SpeedBytes", "LICENSE.txt"), new Version(0, 0, 29, 0)),

        new DevkitServerFileResource("UnturnedUITools",
            Lib("UnturnedUITools", "UnturnedUITools.dll"), new Version(0, 0, 43, 0)) { Side = Side.Client },
        new DevkitServerFileResource("UnturnedUITools.XmlDocs",
            Lib("UnturnedUITools", "UnturnedUITools.xml"), new Version(0, 0, 43, 0)) { Side = Side.Client },
        new DevkitServerFileResource("UnturnedUITools.Symbols",
            Lib("UnturnedUITools", "UnturnedUITools.pdb"), new Version(0, 0, 43, 0)) { Side = Side.Client },
        new DevkitServerFileResource("UnturnedUITools.LICENSE",
            Lib("UnturnedUITools", "LICENSE.txt"), new Version(0, 0, 43, 0)) { Side = Side.Client },

        // legacy reflection tools forwarding
        new DevkitServerFileResource("FWD.DanielWillett.ReflectionTools",
            LibFwd("DanielWillett.ReflectionTools.dll"), new Version(0, 0, 29, 0)) { Delete = true },
        new DevkitServerFileResource("FWD.DanielWillett.UnturnedUITools",
            LibFwd("DanielWillett.UnturnedUITools.dll"), new Version(0, 0, 43, 0)) { Delete = true },
        new DevkitServerFileResource("FWD.DanielWillett.LevelObjectIcons",
            LibFwd("DanielWillett.LevelObjectIcons.dll"), new Version(0, 0, 24, 0)) { Side = Side.Client },

        new DevkitServerFileResource("Microsoft.Bcl.AsyncInterfaces",
            Lib("Microsoft.Bcl.AsyncInterfaces", "Microsoft.Bcl.AsyncInterfaces.dll"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource("Microsoft.Bcl.AsyncInterfaces.LICENSE",
            Lib("Microsoft.Bcl.AsyncInterfaces", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("Microsoft.Bcl.HashCode",
            Lib("Microsoft.Bcl.HashCode", "Microsoft.Bcl.HashCode.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("Microsoft.Bcl.HashCode.LICENSE",
            Lib("Microsoft.Bcl.HashCode", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Buffers",
            Lib("System.Buffers", "System.Buffers.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("System.Buffers.LICENSE",
            Lib("System.Buffers", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.IO.Compression",
            Lib("System.IO.Compression", "System.IO.Compression.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("System.IO.Compression.LICENSE",
            Lib("System.IO.Compression", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.IO.Compression.FileSystem",
            Lib("System.IO.Compression.FileSystem", "System.IO.Compression.FileSystem.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("System.IO.Compression.FileSystem.LICENSE",
            Lib("System.IO.Compression.FileSystem", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Memory",
            Lib("System.Memory", "System.Memory.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("System.Memory.LICENSE",
            Lib("System.Memory", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Numerics.Vectors",
            Lib("System.Numerics.Vectors", "System.Numerics.Vectors.dll"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource("System.Numerics.Vectors.LICENSE",
            Lib("System.Numerics.Vectors", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Runtime.CompilerServices.Unsafe",
            Lib("System.Runtime.CompilerServices.Unsafe", "System.Runtime.CompilerServices.Unsafe.dll"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource("System.Runtime.CompilerServices.Unsafe.LICENSE",
            Lib("System.Runtime.CompilerServices.Unsafe", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Text.Encodings.Web",
            Lib("System.Text.Encodings.Web", "System.Text.Encodings.Web.dll"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource("System.Text.Encodings.Web.LICENSE",
            Lib("System.Text.Encodings.Web", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Text.Json",
            Lib("System.Text.Json", "System.Text.Json.dll"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource("System.Text.Json.LICENSE",
            Lib("System.Text.Json", "LICENSE.txt"), new Version(0, 0, 15, 0)),

        new DevkitServerFileResource("System.Threading.Tasks.Extensions",
            Lib("System.Threading.Tasks.Extensions", "System.Threading.Tasks.Extensions.dll"), new Version(0, 0, 41, 0)),
        new DevkitServerFileResource("System.Threading.Tasks.Extensions.LICENSE",
            Lib("System.Threading.Tasks.Extensions", "LICENSE.txt"), new Version(0, 0, 41, 0)) { Delete = true },

        new DevkitServerFileResource("System.ValueTuple",
            Lib("System.ValueTuple", "System.ValueTuple.dll"), new Version(0, 0, 24, 0)),
        new DevkitServerFileResource("System.ValueTuple.LICENSE",
            Lib("System.ValueTuple", "LICENSE.txt"), new Version(0, 0, 24, 0)),

        new DevkitServerFileResource("UniTask",
            Lib("UniTask", "UniTask.dll"), new Version(0, 0, 22, 0)),
        new DevkitServerFileResource("UniTask.LICENSE",
            Lib("UniTask", "LICENSE.txt"), new Version(0, 0, 15, 0)),
        new DevkitServerFileResource("UniTask.XmlDocs",
            Lib("UniTask", "UniTask.xml"), new Version(0, 0, 15, 0)),
        new DevkitServerFileResource("UniTask.Symbols",
            Lib("UniTask", "UniTask.pdb"), new Version(0, 0, 15, 0)),
        new DevkitServerFileResource("UniTask.Dependencies",
            Lib("UniTask", "UniTask.deps.json"), new Version(0, 0, 15, 0))
    };

    private static string Lib(params string[] args)
    {
        string[] newArgs = new string[args.Length + 1];
        Array.Copy(args, 0, newArgs, 1, args.Length);
        newArgs[0] = "Libraries";
        return Path.Combine(newArgs);
    }
    private static string LibFwd(params string[] args)
    {
        string[] newArgs = new string[args.Length + 2];
        Array.Copy(args, 0, newArgs, 2, args.Length);
        newArgs[0] = "Libraries";
        newArgs[1] = "ForwardingTargets";
        return Path.Combine(newArgs);
    }
}