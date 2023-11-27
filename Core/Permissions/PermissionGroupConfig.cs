#if SERVER
using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Configuration;
using System.Text.Json.Serialization;

namespace DevkitServer.Core.Permissions;

public class PermissionGroupsConfig : SchemaConfiguration
{
    public override string SchemaURI => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/permission_groups_schema.json", true);

    private static PermissionGroupsConfig? _default;

    [JsonPropertyName("groups")]
    public List<PermissionGroup> Groups { get; set; } = null!;

    public static PermissionGroupsConfig Default => _default ??= new PermissionGroupsConfig
    {
        Groups = new List<PermissionGroup>(4)
        {
            new PermissionGroup("viewer", "Viewer", new Color32(255, 204, 102, 255), 0, Array.Empty<PermissionBranch>()),
            new PermissionGroup("terrain_editor", "Terrain Editor", new Color32(51, 204, 51, 255), 1, new PermissionBranch[]
            {
                new PermissionBranch("+unturned::level.terrain.*"),
                new PermissionBranch("+unturned::level.roads.*"),
                new PermissionBranch("+unturned::level.cartography.bake.*")
            }),
            new PermissionGroup("location_builder", "Location Builder", new Color32(255, 255, 153, 255), 1, new PermissionBranch[]
            {
                new PermissionBranch("+unturned::level.terrain.*"),
                new PermissionBranch("+unturned::level.roads.*"),
                new PermissionBranch("+unturned::level.cartography.bake.*"),
                new PermissionBranch("+unturned::level.objects.*"),
                new PermissionBranch("+unturned::level.volumes.*"),
                new PermissionBranch("-unturned::level.volumes.arena_compactor.*"),
                new PermissionBranch("-unturned::level.volumes.cartography.*"),
                new PermissionBranch("-unturned::level.volumes.foliage.*"),
                new PermissionBranch("-unturned::level.volumes.landscape_hole.*"),
                new PermissionBranch("+unturned::level.nodes.*")
            }),
            new PermissionGroup("director", "Director", new Color32(51, 204, 255, 255), 2, new PermissionBranch[]
            {
                PermissionBranch.Superuser
            })
        }
    };
    internal sealed class ConfigHost : JsonConfigurationFile<PermissionGroupsConfig>
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public override PermissionGroupsConfig Default => PermissionGroupsConfig.Default;
        public ConfigHost() : base(DevkitServerConfig.PermissionGroupsPath) { }

        protected override void OnReload()
        {
            Configuration.Groups?.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }
}

#endif