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

    private static readonly Version LastBundleUpdate = new Version(0, 0, 10, 0);

    /*
     * Defines all resource files and the last time (DevkitServer.Resources version) they were updated.
     */

    public List<IDevkitServerResource> Resources = new List<IDevkitServerResource>
    {
        // Directories
        new DevkitServerDirectoryResource("Defaults", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Schemas", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Bundles", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Data", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource("Libraries", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource(Path.Combine("Libraries", "ForwardingTargets"), new Version(0, 0, 13, 0)),

        // Defaults
        new DevkitServerFileResource(Path.Combine("Defaults", "client_config.json"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Defaults", "server_config.json"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Defaults", "backup_config.json"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Defaults", "permission_groups.json"), new Version(0, 0, 10, 0)),

        // Schemas
        new DevkitServerFileResource(Path.Combine("Schemas", "client_config_schema.json"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Schemas", "server_config_schema.json"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Schemas", "backup_schema.json"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Schemas", "permission_groups_schema.json"), new Version(0, 0, 1, 0)),

        // Root
        new DevkitServerFileResource(@"DevkitServer.module", new Version(0, 0, 10, 0)),
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
        new DevkitServerFileResource(Path.Combine("Data", "Object Icon Presets (Vanilla).json"), new Version(0, 0, 1, 0)),

        // Libraries
        new DevkitServerFileResource(Path.Combine("Libraries", "Library Info.txt"), new Version(0, 0, 10, 0)),

        new DevkitServerFileResource(Path.Combine("Libraries", "0Harmony.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "0Harmony.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "Microsoft.Bcl.AsyncInterfaces.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "Microsoft.Bcl.AsyncInterfaces.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "StackCleaner.dll"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource("StackCleaner.pdb", Path.Combine("Libraries", "StackCleaner.pdb"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource("StackCleaner.xml", Path.Combine("Libraries", "StackCleaner.xml"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "StackCleaner.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Buffers.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Buffers.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.IO.Compression.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.IO.Compression.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.IO.Compression.FileSystem.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.IO.Compression.FileSystem.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Memory.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Memory.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Numerics.Vectors.dll"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Numerics.Vectors.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Runtime.CompilerServices.Unsafe.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Runtime.CompilerServices.Unsafe.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Text.Encodings.Web.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Text.Encodings.Web.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Text.Json.dll"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Text.Json.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Threading.Tasks.Extensions.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.Threading.Tasks.Extensions.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.ValueTuple.dll"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "System.ValueTuple.dll license.txt"), new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "UniTask.dll"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource("UniTask.pdb", Path.Combine("Libraries", "UniTask.pdb"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource("UniTask.xml", Path.Combine("Libraries", "UniTask.xml"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "UniTask.dll license.txt"), new Version(0, 0, 10, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "ForwardingTargets", "DanielWillett.ReflectionTools.dll"), new Version(0, 0, 13, 0)),
        new DevkitServerFileResource(Path.Combine("Libraries", "ForwardingTargets", "DanielWillett.UnturnedUITools.dll"), new Version(0, 0, 13, 0))
    };
}
