#if CLIENT
using DevkitServer.API.UI;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorLevelPlayersUI))]
internal class EditorLevelPlayersUIExtension : BaseEditorSpawnsUIExtension<PlayerSpawnpoint>
{
    [ExistingUIMember("altToggle", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected ISleekToggle? AlternateToggle;

    public EditorLevelPlayersUIExtension() : base(new Vector3(0f, 3f, 0f), 20f, 120f)
    {
        if (AlternateToggle != null)
            AlternateToggle.positionOffset_Y = -40;
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion) { }
    protected override Vector3 GetPosition(PlayerSpawnpoint spawn) => spawn.point;
}
#endif