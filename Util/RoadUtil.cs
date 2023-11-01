using DanielWillett.ReflectionTools;
using DevkitServer.Models;

namespace DevkitServer.Util;

public delegate void RoadIndexUpdated(Road road, int fromIndex, int toIndex);
public delegate void VertexIndexUpdated(Road road, RoadVertexIdentifier from, RoadVertexIdentifier to);
public delegate void RoadArgs(Road road, int index);
public delegate void VertexArgs(Road road, RoadVertexIdentifier vertex);

/// <summary>
/// Contains utilities for working with <see cref="Road"/>s and the road editor.
/// </summary>
public static class RoadUtil
{
    internal static readonly CachedMulticastEvent<RoadIndexUpdated> EventOnRoadIndexUpdated = new CachedMulticastEvent<RoadIndexUpdated>(typeof(RoadUtil), nameof(OnRoadIndexUpdated));
    internal static readonly CachedMulticastEvent<VertexIndexUpdated> EventOnVertexIndexUpdated = new CachedMulticastEvent<VertexIndexUpdated>(typeof(RoadUtil), nameof(OnVertexIndexUpdated));
    
    internal static readonly CachedMulticastEvent<RoadArgs> EventOnRoadRemoved = new CachedMulticastEvent<RoadArgs>(typeof(RoadUtil), nameof(OnRoadRemoved));
    internal static readonly CachedMulticastEvent<VertexArgs> EventOnVertexRemoved = new CachedMulticastEvent<VertexArgs>(typeof(RoadUtil), nameof(OnVertexRemoved));

    // todo implement these
    /// <summary>
    /// Called when the index of a <see cref="Road"/> is updated.
    /// </summary>
    public static event RoadIndexUpdated OnRoadIndexUpdated
    {
        add => EventOnRoadIndexUpdated.Add(value);
        remove => EventOnRoadIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Called when the index of a vertex is updated, or after it's <see cref="Road"/>'s index is updated.
    /// </summary>
    public static event VertexIndexUpdated OnVertexIndexUpdated
    {
        add => EventOnVertexIndexUpdated.Add(value);
        remove => EventOnVertexIndexUpdated.Remove(value);
    }


    /// <summary>
    /// Called when a <see cref="Road"/> is removed.
    /// </summary>
    public static event RoadArgs OnRoadRemoved
    {
        add => EventOnRoadRemoved.Add(value);
        remove => EventOnRoadRemoved.Remove(value);
    }

    /// <summary>
    /// Called when a vertex is removed, or before it's <see cref="Road"/> is removed.
    /// </summary>
    public static event VertexArgs OnVertexRemoved
    {
        add => EventOnVertexRemoved.Add(value);
        remove => EventOnVertexRemoved.Remove(value);
    }

#if CLIENT
    private static readonly StaticGetter<int>? GetVertexIndex = Accessor.GenerateStaticGetter<EditorRoads, int>("vertexIndex", false);
    private static readonly StaticGetter<int>? GetTangentIndex = Accessor.GenerateStaticGetter<EditorRoads, int>("tangentIndex", false);
    private static readonly StaticGetter<Transform?>? GetSelection = Accessor.GenerateStaticGetter<EditorRoads, Transform?>("selection", false);
    private static readonly Action<Transform?>? CallSelect = Accessor.GenerateStaticCaller<EditorRoads, Action<Transform?>>("select", parameters: new Type[] { typeof(Transform) });

    /// <summary>
    /// The index of the selected road vertex in <see cref="EditorRoads.road"/>, or -1 if none are selected (or in the case of a reflection failure).
    /// </summary>
    public static int SelectedVertexIndex => GetVertexIndex != null ? GetVertexIndex() : -1;

    /// <summary>
    /// The index of the selected tangent handle (0 or 1) in <see cref="EditorRoads.road"/> at vertex <see cref="SelectedVertexIndex"/>, or -1 if none are selected (or in the case of a reflection failure).
    /// </summary>
    public static int SelectedTangentIndex => GetTangentIndex != null ? GetTangentIndex() : -1;

    /// <summary>
    /// The <see cref="Transform"/> of the element selected, or <see langref="null"/> if nothing is selected (or in the case of a reflection failure).
    /// </summary>
    /// <remarks>This could be a vertex or tangent handle.</remarks>
    public static Transform? SelectedRoadElement => GetSelection?.Invoke();

    /// <summary>
    /// Deselect the road and vertex or tangent handle currently selected.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    public static bool Deselect() => Select(null);

    /// <summary>
    /// Select the road and vertex or tangent handle belonging to the element <paramref name="roadElement"/>. This can be a vertex or tangent handle editor object.
    /// </summary>
    /// <remarks>Will do nothing if <paramref name="roadElement"/> is already selected. Passing <see langword="null"/> is the same as calling <see cref="Deselect"/>.</remarks>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    public static bool Select(Transform? roadElement)
    {
        if (CallSelect == null)
            return false;

        if (roadElement != null && SelectedRoadElement != roadElement)
            CallSelect.Invoke(roadElement);

        return true;
    }
#endif

    public static int GetRoadIndex(this Road road)
    {
        return !Level.isEditor ? road.roadIndex : LevelRoads.getRoadIndex(road);
    }
}

/// <summary>
/// Represents the two tangent handles for roads.
/// </summary>
public enum TangentHandle : byte
{
    /// <summary>
    /// Faces towards the joint one index below the owning joint.
    /// </summary>
    Negative = 0,
    /// <summary>
    /// Faces towards the joint one index above the owning joint.
    /// </summary>
    Positive = 1
}