using System;
using System.Collections.Generic;
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

    public List<IDevkitServerResource> Resources = new List<IDevkitServerResource>
    {
        // Directories
        new DevkitServerDirectoryResource(@"Defaults", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource(@"Schemas", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource(@"Bundles", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource(@"Data", new Version(0, 0, 1, 0)),
        new DevkitServerDirectoryResource(@"Libraries", new Version(0, 0, 1, 0)),

        // Defaults
        new DevkitServerFileResource(@"Defaults\client_config.json", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Defaults\server_config.json", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Defaults\backup_config.json", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Defaults\permission_groups.json", new Version(0, 0, 1, 0)),

        // Schemas
        new DevkitServerFileResource(@"Schemas\client_config_schema.json", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Schemas\server_config_schema.json", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Schemas\backup_schema.json", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Schemas\permission_groups_schema.json", new Version(0, 0, 1, 0)),

        // Root
        new DevkitServerFileResource(@"DevkitServer.module", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"English.dat", new Version(0, 0, 1, 0)),
        
        // Bundles
        new DevkitServerFileResource(@"Bundles\MasterBundle.dat", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource("devkitserver.masterbundle", @"Bundles\devkitserver.masterbundle", new Version(0, 0, 6, 0)),
        new DevkitServerFileResource("devkitserver.masterbundle.hash", @"Bundles\devkitserver.masterbundle.hash", new Version(0, 0, 6, 0)),
        new DevkitServerFileResource("devkitserver.masterbundle.manifest", @"Bundles\devkitserver.masterbundle.manifest", new Version(0, 0, 6, 0)),
        new DevkitServerFileResource("devkitserver_linux.masterbundle", @"Bundles\devkitserver_linux.masterbundle", new Version(0, 0, 6, 0)),
        new DevkitServerFileResource("devkitserver_linux.masterbundle.manifest", @"Bundles\devkitserver_linux.masterbundle.manifest", new Version(0, 0, 6, 0)),
        new DevkitServerFileResource("devkitserver_mac.masterbundle", @"Bundles\devkitserver_mac.masterbundle", new Version(0, 0, 6, 0)),
        new DevkitServerFileResource("devkitserver_mac.masterbundle.manifest", @"Bundles\devkitserver_mac.masterbundle.manifest", new Version(0, 0, 6, 0)),

        // Data
        new DevkitServerFileResource(@"Data\Object Icon Presets (Vanilla).json", new Version(0, 0, 1, 0)),

        // Libraries
        new DevkitServerFileResource(@"Libraries\Library Info.txt", new Version(0, 0, 1, 0)),

        new DevkitServerFileResource(@"Libraries\0Harmony.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\0Harmony.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\Microsoft.Bcl.AsyncInterfaces.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\Microsoft.Bcl.AsyncInterfaces.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\StackCleaner.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource("StackCleaner.pdb", @"Libraries\StackCleaner.pdb", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource("StackCleaner.xml", @"Libraries\StackCleaner.xml", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\StackCleaner.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Buffers.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Buffers.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.IO.Compression.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.IO.Compression.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.IO.Compression.FileSystem.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.IO.Compression.FileSystem.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Memory.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Memory.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Numerics.Vectors.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Numerics.Vectors.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Runtime.CompilerServices.Unsafe.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Runtime.CompilerServices.Unsafe.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Text.Encodings.Web.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Text.Encodings.Web.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Text.Json.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Text.Json.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Threading.Tasks.Extensions.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.Threading.Tasks.Extensions.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.ValueTuple.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\System.ValueTuple.dll license.txt", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\UniTask.dll", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource("UniTask.pdb", @"Libraries\UniTask.pdb", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource("UniTask.xml", @"Libraries\UniTask.xml", new Version(0, 0, 1, 0)),
        new DevkitServerFileResource(@"Libraries\UniTask.dll license.txt", new Version(0, 0, 1, 0))
    };
}
