#if CLIENT
using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.API.Extensions.Members;
using DevkitServer.API.Devkit.Spawns;

namespace DevkitServer.Core.UI.Extensions;

[UIExtension(typeof(EditorLevelPlayersUI))]
internal class EditorLevelPlayersUIExtension : BaseEditorSpawnsUIExtension<PlayerSpawnpoint>
{
    [ExistingMember("altToggle", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected ISleekToggle? AlternateToggle;
    protected override bool IsVisible
    {
        get => LevelVisibility.playersVisible;
        set => LevelVisibility.playersVisible = value;
    }
    public EditorLevelPlayersUIExtension() : base(new Vector3(0f, 3f, 0f), 20f, 120f, SpawnType.Player)
    {
        if (AlternateToggle != null)
            AlternateToggle.PositionOffset_Y = -40;
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion) { }
    protected override Vector3 GetPosition(PlayerSpawnpoint spawn) => spawn.node.position;
    protected override bool CheckLabelAlive(PlayerSpawnpoint spawn) => spawn.node != null;
}
#endif