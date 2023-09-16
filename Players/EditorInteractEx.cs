#if CLIENT
namespace DevkitServer.Players;

[EarlyTypeInit]
public class EditorInteractEx
{
    public static Type? EditorInteractType { get; } = Accessor.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract");

    private static readonly StaticGetter<RaycastHit>? GetWorldHit = Accessor.GenerateStaticPropertyGetter<RaycastHit>(EditorInteractType, "worldHit");
    private static readonly StaticGetter<RaycastHit>? GetObjectHit = Accessor.GenerateStaticPropertyGetter<RaycastHit>(EditorInteractType, "objectHit");
    private static readonly StaticGetter<RaycastHit>? GetLogicHit = Accessor.GenerateStaticPropertyGetter<RaycastHit>(EditorInteractType, "logicHit");
    private static readonly StaticGetter<Ray>? GetScreenRay = Accessor.GenerateStaticPropertyGetter<Ray>(EditorInteractType, "ray");
    private static readonly StaticGetter<bool>? GetIsFlying = Accessor.GenerateStaticPropertyGetter<bool>(EditorInteractType, "isFlying");
#if CLIENT
    static EditorInteractEx()
    {

    }
#endif
    public static bool IsFlying => GetIsFlying != null && GetIsFlying();
    public static Ray Ray => GetScreenRay == null ? UserInput.GetLocalLookRay() : GetScreenRay();
    public static bool TryGetWorldHit(out RaycastHit hit)
    {
        if (GetWorldHit == null)
        {
            hit = default;
            return false;
        }

        hit = GetWorldHit();
        return hit.transform != null;
    }
    public static bool TryGetObjectHit(out RaycastHit hit)
    {
        if (GetObjectHit == null)
        {
            hit = default;
            return false;
        }

        hit = GetObjectHit();
        return hit.transform != null;
    }
    public static bool TryGetLogicHit(out RaycastHit hit)
    {
        if (GetLogicHit == null)
        {
            hit = default;
            return false;
        }

        hit = GetLogicHit();
        return hit.transform != null;
    }
}
#endif