using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Util;
internal static class VanillaEditorObjectIconPresets
{
    public static Dictionary<Guid, AssetIconPreset> Presets = new Dictionary<Guid, AssetIconPreset>(128)
    {
        /* Billboard 0-16 */
        {
            new Guid("205a8cc33c9849c9bd65790403d0753d"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("a227d89ce4f34339a0124c146c1f8218"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("a8681fcb59d44b1588caec49f30a9c8f"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("70a1a85305a54009890ad292cd31b4ba"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("8631144673b34a89af1e6f7160591892"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("4925ee9047d846eea13fe2ffd91d34a3"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("61b260059a16486da1cd434cf7f2a88f"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("026f1c9406c34b8786517e1a6a10db4b"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("ebfb3476f7f0406f88d0cba7054afc3f"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("4182d6f8e54044f08c30d1961bb1dbec"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("7f48479a84a94592932e61684af5ecc6"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("b6f9ed46e7a04072834094ba45bf7bc2"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("74a98d29022343a0904bd6641c96f8c1"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("60b06f3164e24fcf8025fdabbad01841"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("7d8cee8f7ce143d5819629c761fbe861"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("5ba775bc88fe4171ab457e724179086a"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        },
        {
            new Guid("fe5a47b6ef0f4e3087028d57180e0b71"),
            new AssetIconPreset
            {
                IconPosition = new Vector3(-3f, -10f, 9f),
                IconRotation = Quaternion.Euler(-107f, -80f, 260f)
            }
        }
    };
}

public class AssetIconPreset
{
    public Vector3 IconPosition { get; set; }
    public Quaternion IconRotation { get; set; }
}