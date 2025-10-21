using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Configuration;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Players;
using System.Globalization;
#if SERVER
using DevkitServer.API.UI;
#endif
#if CLIENT
using DanielWillett.ReflectionTools;
#endif

namespace DevkitServer.Util;

public delegate void RoadIndexUpdated(Road road, int fromIndex, int toIndex);
public delegate void RoadVertexIndexUpdated(Road road, RoadVertexIdentifier from, RoadVertexIdentifier to);
public delegate void RoadArgs(Road road, int index);
public delegate void RoadVertexArgs(Road road, RoadVertexIdentifier vertex);
public delegate void RoadVertexMoved(Road road, RoadVertexIdentifier vertex, Vector3 fromWorldPosition, Vector3 toWorldPosition);

/// <param name="toRelativePosition">New position of the handle relative to it's vertex.</param>
/// <param name="fromRelativePosition">Previous position of the handle relative to it's vertex.</param>
/// <param name="isReflectionMovement">Is the movement caused by the other handle moving, like when <see cref="ERoadMode"/> not <see cref="ERoadMode.FREE"/>?</param>
public delegate void RoadTangentHandleMoved(Road road, RoadTangentHandleIdentifier handle, Vector3 fromRelativePosition, Vector3 toRelativePosition, bool isReflectionMovement);

public delegate void RoadVertexTangentHandleModeUpdated(Road road, RoadVertexIdentifier handle, ERoadMode fromHandleMode, ERoadMode toHandleMode);
public delegate void RoadIsLoopUpdated(Road road, int index, bool isLoop);

[Obsolete("Use RoadMaterialOrAssetUpdated instead.")]
public delegate void RoadMaterialUpdated(Road road, int index, byte fromMaterialIndex, byte toMaterialIndex, RoadMaterial fromMaterial, RoadMaterial toMaterial);
public delegate void RoadMaterialOrAssetUpdated(Road road, int index, RoadMaterialOrAsset fromAsset, RoadMaterialOrAsset toAsset);
public delegate void RoadVertexIgnoreTerrainUpdated(Road road, RoadVertexIdentifier handle, bool ignoreTerrain);
public delegate void RoadVertexVerticalOffsetUpdated(Road road, RoadVertexIdentifier handle, float fromOffset, float toOffset);

public delegate void RoadMaterialDimensionUpdated(RoadMaterial material, int materialIndex, float fromValue, float toValue);
public delegate void RoadMaterialIsConcreteUpdated(RoadMaterial material, int materialIndex, bool isConcrete);

/// <summary>
/// Contains utilities for working with <see cref="Road"/>s and the road editor.
/// </summary>
public static class RoadUtil
{
    private const string Source = "ROAD UTIL";

    /// <summary>
    /// Stops auto-baking in most cases.
    /// </summary>
    internal static bool HoldBake;

    private static readonly CachedMulticastEvent<RoadIndexUpdated> EventOnRoadIndexUpdated = new CachedMulticastEvent<RoadIndexUpdated>(typeof(RoadUtil), nameof(OnRoadIndexUpdated));
    private static readonly CachedMulticastEvent<RoadVertexIndexUpdated> EventOnVertexIndexUpdated = new CachedMulticastEvent<RoadVertexIndexUpdated>(typeof(RoadUtil), nameof(OnVertexIndexUpdated));

    private static readonly CachedMulticastEvent<RoadArgs> EventOnRoadRemoved = new CachedMulticastEvent<RoadArgs>(typeof(RoadUtil), nameof(OnRoadRemoved));
    private static readonly CachedMulticastEvent<RoadVertexArgs> EventOnVertexRemoved = new CachedMulticastEvent<RoadVertexArgs>(typeof(RoadUtil), nameof(OnVertexRemoved));

    private static readonly CachedMulticastEvent<RoadArgs> EventOnRoadAdded = new CachedMulticastEvent<RoadArgs>(typeof(RoadUtil), nameof(OnRoadAdded));
    private static readonly CachedMulticastEvent<RoadVertexArgs> EventOnVertexAdded = new CachedMulticastEvent<RoadVertexArgs>(typeof(RoadUtil), nameof(OnVertexAdded));

    private static readonly CachedMulticastEvent<RoadVertexMoved> EventOnVertexMoved = new CachedMulticastEvent<RoadVertexMoved>(typeof(RoadUtil), nameof(OnVertexMoved));
    private static readonly CachedMulticastEvent<RoadTangentHandleMoved> EventOnTangentHandleMoved = new CachedMulticastEvent<RoadTangentHandleMoved>(typeof(RoadUtil), nameof(OnTangentHandleMoved));
    
    private static readonly CachedMulticastEvent<RoadVertexTangentHandleModeUpdated> EventOnVertexTangentHandleModeUpdated = new CachedMulticastEvent<RoadVertexTangentHandleModeUpdated>(typeof(RoadUtil), nameof(OnVertexTangentHandleModeUpdated));
    private static readonly CachedMulticastEvent<RoadIsLoopUpdated> EventOnIsLoopUpdated = new CachedMulticastEvent<RoadIsLoopUpdated>(typeof(RoadUtil), nameof(OnIsLoopUpdated));
    [Obsolete("Use EventOnMaterialOrAssetUpdated instead.")]
    private static readonly CachedMulticastEvent<RoadMaterialUpdated> EventOnMaterialUpdated = new CachedMulticastEvent<RoadMaterialUpdated>(typeof(RoadUtil), nameof(OnMaterialUpdated));
    private static readonly CachedMulticastEvent<RoadMaterialOrAssetUpdated> EventOnMaterialOrAssetUpdated = new CachedMulticastEvent<RoadMaterialOrAssetUpdated>(typeof(RoadUtil), nameof(OnMaterialOrAssetUpdated));
    private static readonly CachedMulticastEvent<RoadVertexIgnoreTerrainUpdated> EventOnVertexIgnoreTerrainUpdated = new CachedMulticastEvent<RoadVertexIgnoreTerrainUpdated>(typeof(RoadUtil), nameof(OnVertexIgnoreTerrainUpdated));
    private static readonly CachedMulticastEvent<RoadVertexVerticalOffsetUpdated> EventOnVertexVerticalOffsetUpdated = new CachedMulticastEvent<RoadVertexVerticalOffsetUpdated>(typeof(RoadUtil), nameof(OnVertexVerticalOffsetUpdated));
    
    private static readonly CachedMulticastEvent<RoadMaterialDimensionUpdated> EventOnMaterialWidthUpdated = new CachedMulticastEvent<RoadMaterialDimensionUpdated>(typeof(RoadUtil), nameof(OnMaterialWidthUpdated));
    private static readonly CachedMulticastEvent<RoadMaterialDimensionUpdated> EventOnMaterialHeightUpdated = new CachedMulticastEvent<RoadMaterialDimensionUpdated>(typeof(RoadUtil), nameof(OnMaterialHeightUpdated));
    private static readonly CachedMulticastEvent<RoadMaterialDimensionUpdated> EventOnMaterialDepthUpdated = new CachedMulticastEvent<RoadMaterialDimensionUpdated>(typeof(RoadUtil), nameof(OnMaterialDepthUpdated));
    private static readonly CachedMulticastEvent<RoadMaterialDimensionUpdated> EventOnMaterialVerticalOffsetUpdated = new CachedMulticastEvent<RoadMaterialDimensionUpdated>(typeof(RoadUtil), nameof(OnMaterialVerticalOffsetUpdated));
    private static readonly CachedMulticastEvent<RoadMaterialIsConcreteUpdated> EventOnMaterialIsConcreteUpdated = new CachedMulticastEvent<RoadMaterialIsConcreteUpdated>(typeof(RoadUtil), nameof(OnMaterialIsConcreteUpdated));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, byte, Guid> SendRequestInstantiation = new NetCall<Vector3, byte, Guid>(DevkitServerNetCall.RequestRoadInstantiation);
    [UsedImplicitly]
    private static readonly NetCall<NetId, Vector3, int> SendRequestVertexInstantiation = new NetCall<NetId, Vector3, int>(DevkitServerNetCall.RequestRoadVertexInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Vector3, ushort, Guid, Vector3, Vector3, float, long, ulong> SendInstantiation = new NetCall<Vector3, ushort, Guid, Vector3, Vector3, float, long, ulong>(DevkitServerNetCall.SendRoadInstantiation);
    [UsedImplicitly]
    private static readonly NetCall<NetId, Vector3, Vector3, Vector3, int, ERoadMode, float, bool, NetId, ulong> SendVertexInstantiation = new NetCall<NetId, Vector3, Vector3, Vector3, int, ERoadMode, float, bool, NetId, ulong>(DevkitServerNetCall.SendRoadVertexInstantiation);
    
    /// <summary>
    /// Called when the index of a <see cref="Road"/> is updated locally.
    /// </summary>
    public static event RoadIndexUpdated OnRoadIndexUpdated
    {
        add => EventOnRoadIndexUpdated.Add(value);
        remove => EventOnRoadIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Called when the index of a vertex is updated, or after it's <see cref="Road"/>'s index is updated locally.
    /// </summary>
    public static event RoadVertexIndexUpdated OnVertexIndexUpdated
    {
        add => EventOnVertexIndexUpdated.Add(value);
        remove => EventOnVertexIndexUpdated.Remove(value);
    }


    /// <summary>
    /// Called when a <see cref="Road"/> is removed locally.
    /// </summary>
    public static event RoadArgs OnRoadRemoved
    {
        add => EventOnRoadRemoved.Add(value);
        remove => EventOnRoadRemoved.Remove(value);
    }

    /// <summary>
    /// Called when a vertex is removed, or before it's <see cref="Road"/> is removed locally.
    /// </summary>
    public static event RoadVertexArgs OnVertexRemoved
    {
        add => EventOnVertexRemoved.Add(value);
        remove => EventOnVertexRemoved.Remove(value);
    }


    /// <summary>
    /// Called when a <see cref="Road"/> is added locally.
    /// </summary>
    public static event RoadArgs OnRoadAdded
    {
        add => EventOnRoadAdded.Add(value);
        remove => EventOnRoadAdded.Remove(value);
    }

    /// <summary>
    /// Called when a vertex is added, or after it's <see cref="Road"/> is added locally.
    /// </summary>
    public static event RoadVertexArgs OnVertexAdded
    {
        add => EventOnVertexAdded.Add(value);
        remove => EventOnVertexAdded.Remove(value);
    }


    /// <summary>
    /// Called when a vertex is translated locally.
    /// </summary>
    public static event RoadVertexMoved OnVertexMoved
    {
        add => EventOnVertexMoved.Add(value);
        remove => EventOnVertexMoved.Remove(value);
    }

    /// <summary>
    /// Called when a vertex's tangent handle is translated locally.
    /// </summary>
    public static event RoadTangentHandleMoved OnTangentHandleMoved
    {
        add => EventOnTangentHandleMoved.Add(value);
        remove => EventOnTangentHandleMoved.Remove(value);
    }


    /// <summary>
    /// Called when a road's <see cref="Road.isLoop"/> is updated locally.
    /// </summary>
    public static event RoadIsLoopUpdated OnIsLoopUpdated
    {
        add => EventOnIsLoopUpdated.Add(value);
        remove => EventOnIsLoopUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a road's <see cref="Road.material"/> is updated locally.
    /// </summary>
    [Obsolete("Use OnMaterialOrAssetUpdated instead.")]
    public static event RoadMaterialUpdated OnMaterialUpdated
    {
        add => EventOnMaterialUpdated.Add(value);
        remove => EventOnMaterialUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a road's <see cref="Road.material"/> or <see cref="Road.RoadAssetRef"/> is updated locally.
    /// </summary>
    public static event RoadMaterialOrAssetUpdated OnMaterialOrAssetUpdated
    {
        add => EventOnMaterialOrAssetUpdated.Add(value);
        remove => EventOnMaterialOrAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a vertex's <see cref="RoadJoint.mode"/> is updated locally.
    /// </summary>
    public static event RoadVertexTangentHandleModeUpdated OnVertexTangentHandleModeUpdated
    {
        add => EventOnVertexTangentHandleModeUpdated.Add(value);
        remove => EventOnVertexTangentHandleModeUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a vertex's <see cref="RoadJoint.ignoreTerrain"/> is updated locally.
    /// </summary>
    public static event RoadVertexIgnoreTerrainUpdated OnVertexIgnoreTerrainUpdated
    {
        add => EventOnVertexIgnoreTerrainUpdated.Add(value);
        remove => EventOnVertexIgnoreTerrainUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a vertex's <see cref="RoadJoint.offset"/> is updated locally.
    /// </summary>
    public static event RoadVertexVerticalOffsetUpdated OnVertexVerticalOffsetUpdated
    {
        add => EventOnVertexVerticalOffsetUpdated.Add(value);
        remove => EventOnVertexVerticalOffsetUpdated.Remove(value);
    }


    /// <summary>
    /// Called when a material's <see cref="RoadMaterial.width"/> is updated locally.
    /// </summary>
    public static event RoadMaterialDimensionUpdated OnMaterialWidthUpdated
    {
        add => EventOnMaterialWidthUpdated.Add(value);
        remove => EventOnMaterialWidthUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a material's <see cref="RoadMaterial.height"/> is updated locally.
    /// </summary>
    public static event RoadMaterialDimensionUpdated OnMaterialHeightUpdated
    {
        add => EventOnMaterialHeightUpdated.Add(value);
        remove => EventOnMaterialHeightUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a material's <see cref="RoadMaterial.depth"/> is updated locally.
    /// </summary>
    public static event RoadMaterialDimensionUpdated OnMaterialDepthUpdated
    {
        add => EventOnMaterialDepthUpdated.Add(value);
        remove => EventOnMaterialDepthUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a material's <see cref="RoadMaterial.offset"/> is updated locally.
    /// </summary>
    public static event RoadMaterialDimensionUpdated OnMaterialVerticalOffsetUpdated
    {
        add => EventOnMaterialVerticalOffsetUpdated.Add(value);
        remove => EventOnMaterialVerticalOffsetUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a material's <see cref="RoadMaterial.isConcrete"/> is updated locally.
    /// </summary>
    public static event RoadMaterialIsConcreteUpdated OnMaterialIsConcreteUpdated
    {
        add => EventOnMaterialIsConcreteUpdated.Add(value);
        remove => EventOnMaterialIsConcreteUpdated.Remove(value);
    }

#if CLIENT
    private static readonly StaticGetter<int>? GetVertexIndex = Accessor.GenerateStaticGetter<EditorRoads, int>("vertexIndex", false);
    private static readonly StaticGetter<int>? GetTangentIndex = Accessor.GenerateStaticGetter<EditorRoads, int>("tangentIndex", false);
    private static readonly StaticGetter<Transform?>? GetSelection = Accessor.GenerateStaticGetter<EditorRoads, Transform?>("selection", false);
    private static readonly Action<Transform?>? CallSelect = Accessor.GenerateStaticCaller<EditorRoads, Action<Transform?>>("select", parameters: new Type[] { typeof(Transform) });
    private static readonly Action? CallDeselect = Accessor.GenerateStaticCaller<EditorRoads, Action>("deselect", parameters: Type.EmptyTypes);

    /// <summary>
    /// The index of the selected road vertex in <see cref="EditorRoads.road"/>, or -1 if none are selected (or in the case of a reflection failure).
    /// </summary>
    public static int SelectedVertexIndex => GetVertexIndex?.Invoke() ?? -1;

    /// <summary>
    /// The index of the selected tangent handle (0 or 1) in <see cref="EditorRoads.road"/> at vertex <see cref="SelectedVertexIndex"/>, or -1 if none are selected (or in the case of a reflection failure).
    /// </summary>
    public static int SelectedTangentIndex => GetTangentIndex?.Invoke() ?? -1;

    /// <summary>
    /// The <see cref="Transform"/> of the element selected, or <see langref="null"/> if nothing is selected (or in the case of a reflection failure).
    /// </summary>
    /// <remarks>This could be a vertex or tangent handle.</remarks>
    public static Transform? SelectedRoadElement => GetSelection?.Invoke();

    internal static bool ClearSelection()
    {
        if (CallDeselect == null)
            return false;

        CallDeselect();
        return true;
    }

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
        ThreadUtil.assertIsGameThread();

        if (CallSelect == null)
            return false;

        if (roadElement != null && (GetSelection == null || SelectedRoadElement != roadElement))
            CallSelect(roadElement);

        return true;
    }

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static void RequestRoadInstantiation(Vector3 firstVertexWorldPosition, int materialIndex)
    {
        if (materialIndex is > byte.MaxValue or < 0)
            throw new ArgumentOutOfRangeException(nameof(materialIndex), "Material index must be a byte (0-255).");

        RequestRoadInstantiation(firstVertexWorldPosition, new RoadMaterialOrAsset((byte)materialIndex));
    }
    /// <summary>
    /// Sends a request to the server to instantiate a road and a first vertex.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Material index was out of range of a byte.</exception>
    public static void RequestRoadInstantiation(Vector3 firstVertexWorldPosition, RoadMaterialOrAsset material)
    {
        DevkitServerModule.AssertIsDevkitServerClient();
        SendRequestInstantiation.Invoke(firstVertexWorldPosition, material.LegacyIndex, material.Guid);
    }

    /// <summary>
    /// Sends a request to the server to instantiate a vertex on <paramref name="road"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    /// <returns><see langword="false"/> if the <see cref="NetId"/> of <paramref name="road"/> can not be found, otherwise <see langword="true"/>.</returns>
    public static bool RequestVertexInstantiation(Road road, Vector3 firstVertexWorldPosition, int vertexIndex)
    {
        if (!RoadNetIdDatabase.TryGetRoadNetId(road, out NetId netId))
            return false;

        DevkitServerModule.AssertIsDevkitServerClient();
        SendRequestVertexInstantiation.Invoke(netId, firstVertexWorldPosition, vertexIndex);
        return true;
    }

    /// <summary>
    /// Sends a request to the server to instantiate a vertex on the road at index <paramref name="roadIndex"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    /// <returns><see langword="false"/> if the <see cref="NetId"/> of <paramref name="roadIndex"/> can not be found, otherwise <see langword="true"/>.</returns>
    public static bool RequestVertexInstantiation(int roadIndex, Vector3 firstVertexWorldPosition, int vertexIndex)
    {
        if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId netId))
            return false;

        DevkitServerModule.AssertIsDevkitServerClient();
        SendRequestVertexInstantiation.Invoke(netId, firstVertexWorldPosition, vertexIndex);
        return true;
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendRoadInstantiation)]
    internal static StandardErrorCode ReceiveInstantiation(MessageContext ctx, Vector3 firstVertexWorldPosition, ushort flags, Guid asset, Vector3 tangent1RelativePosition, Vector3 tangent2RelativePosition, float verticalOffset, long netIdsPacked, ulong owner)
    {
        if (!EditorActions.HasProcessedPendingRoads)
        {
            EditorActions.TemporaryEditorActions?.QueueRoadInstantiation(netIdsPacked, flags, asset, firstVertexWorldPosition, tangent1RelativePosition, tangent2RelativePosition, verticalOffset, owner);
            return StandardErrorCode.Success;
        }

        NetId netId = new NetId((uint)netIdsPacked), vertexNetId = new NetId((uint)(netIdsPacked >> 32));
        byte materialIndex = (byte)flags;
        bool ignoreTerrain = (flags & (1 << 9)) != 0;
        bool isLoop = (flags & (1 << 8)) != 0;
        ERoadMode handleMode = (ERoadMode)((flags >> 10) & 0b00111111);

        HoldBake = true;

        try
        {
            Transform vertexTransform = AddRoadLocal(firstVertexWorldPosition, materialIndex == byte.MaxValue ? new RoadMaterialOrAsset(asset) : new RoadMaterialOrAsset(materialIndex));

            if (vertexTransform == null)
                return StandardErrorCode.GenericError;

            InitializeRoad(vertexTransform, out Road? road, netId, vertexNetId);

            if (road == null)
                return StandardErrorCode.GenericError;
            
            int roadIndex = road.GetRoadIndex();
            if (road.isLoop != isLoop)
            {
                SetIsLoopLocal(road, roadIndex, isLoop);
            }

            RoadJoint joint = road.joints[0];
            if (joint.getTangent(0) != tangent1RelativePosition || joint.getTangent(1) != tangent2RelativePosition)
            {
                ERoadMode old = joint.mode;
                joint.mode = ERoadMode.FREE;
                SetTangentHandlePositionLocal(road, roadIndex, 0, TangentHandle.Negative, tangent1RelativePosition);
                SetTangentHandlePositionLocal(road, roadIndex, 0, TangentHandle.Positive, tangent2RelativePosition);
                joint.mode = old;
            }

            if (joint.mode != handleMode)
            {
                SetVertexTangentHandleModeLocal(road, roadIndex, joint, 0, handleMode);
            }

            if (joint.ignoreTerrain != ignoreTerrain)
            {
                SetVertexIgnoreTerrainLocal(road, roadIndex, joint, 0, ignoreTerrain);
            }

            if (joint.offset != verticalOffset)
            {
                SetVertexVerticalOffsetLocal(road, roadIndex, joint, 0, verticalOffset);
            }

            if (owner == Provider.client.m_SteamID && EditorRoads.isPaving)
                Select(vertexTransform);

            SyncIfAuthority(roadIndex);

            if (!DevkitServerConfig.RemoveCosmeticImprovements)
                road.buildMesh();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Failed to initialize road: {netId.Format()}.");
            return StandardErrorCode.GenericError;
        }
        finally
        {
            HoldBake = false;
        }

        return StandardErrorCode.Success;
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendRoadVertexInstantiation)]
    internal static StandardErrorCode ReceiveVertexInstantiation(MessageContext ctx, NetId roadNetId, Vector3 vertexWorldPosition, Vector3 tangent1RelativePosition, Vector3 tangent2RelativePosition, int vertexIndex, ERoadMode handleMode, float verticalOffset, bool ignoreTerrain, NetId vertexNetId, ulong owner)
    {
        if (!EditorActions.HasProcessedPendingRoads)
        {
            EditorActions.TemporaryEditorActions?.QueueRoadVertexInstantiation(roadNetId, vertexWorldPosition, tangent1RelativePosition, tangent2RelativePosition, ignoreTerrain, verticalOffset, vertexIndex, owner, vertexNetId, handleMode);
            return StandardErrorCode.Success;
        }

        HoldBake = true;
        Road? road = null;
        try
        {
            if (!RoadNetIdDatabase.TryGetRoad(roadNetId, out road, out int roadIndex))
            {
                Logger.DevkitServer.LogWarning(nameof(ReceiveVertexInstantiation), $"Failed to find road of net id {roadNetId.Format()}.");
                return StandardErrorCode.NotFound;
            }
            

            Transform vertexTransform = AddVertexLocal(road, roadIndex, vertexIndex, vertexWorldPosition);
            if (vertexTransform == null)
                return StandardErrorCode.GenericError;

            InitializeVertex(road, roadIndex, vertexIndex, vertexNetId);

            RoadJoint joint = road.joints[vertexIndex];
            if (joint.getTangent(0) != tangent1RelativePosition || joint.getTangent(1) != tangent2RelativePosition)
            {
                ERoadMode old = joint.mode;
                joint.mode = ERoadMode.FREE;
                SetTangentHandlePositionLocal(road, roadIndex, vertexIndex, TangentHandle.Negative, tangent1RelativePosition);
                SetTangentHandlePositionLocal(road, roadIndex, vertexIndex, TangentHandle.Positive, tangent2RelativePosition);
                joint.mode = old;
            }

            if (joint.mode != handleMode)
            {
                SetVertexTangentHandleModeLocal(road, roadIndex, joint, vertexIndex, handleMode);
            }

            if (joint.ignoreTerrain != ignoreTerrain)
            {
                SetVertexIgnoreTerrainLocal(road, roadIndex, joint, vertexIndex, ignoreTerrain);
            }

            if (joint.offset != verticalOffset)
            {
                SetVertexVerticalOffsetLocal(road, roadIndex, joint, vertexIndex, verticalOffset);
            }

            if (owner == Provider.client.m_SteamID && EditorRoads.isPaving)
                Select(vertexTransform);

            SyncIfAuthority(roadIndex);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Failed to initialize vertex # {vertexIndex.Format()} on road {roadNetId.Format()}: {vertexNetId.Format()}.");
            return StandardErrorCode.GenericError;
        }
        finally
        {
            HoldBake = false;
            if (road != null && !DevkitServerConfig.RemoveCosmeticImprovements)
                road.buildMesh();
        }

        return StandardErrorCode.Success;
    }
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestRoadInstantiation)]
    internal static void ReceiveRoadInstantiationRequest(MessageContext ctx, Vector3 firstVertexPosition, byte materialIndex, Guid roadAssetGuid)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveRoadInstantiationRequest), "Unable to get user from road instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.EditRoads.Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.EditRoads);
            return;
        }

        RoadMaterialOrAsset material = materialIndex == byte.MaxValue
            ? new RoadMaterialOrAsset(roadAssetGuid)
            : new RoadMaterialOrAsset(materialIndex);

        AddRoad(firstVertexPosition, material, ctx);
        
        Logger.DevkitServer.LogDebug(Source, $"Granted request for instantiation of road at {firstVertexPosition.Format()}, material: {material.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestRoadVertexInstantiation)]
    internal static void ReceiveRoadVertexInstantiationRequest(MessageContext ctx, NetId roadNetId, Vector3 worldPosition, int vertexIndex)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveRoadVertexInstantiationRequest), "Unable to get user from road instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.EditRoads.Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.EditRoads);
            return;
        }

        if (!RoadNetIdDatabase.TryGetRoad(roadNetId, out Road road, out int roadIndex))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.EditRoads);
            return;
        }

        AddVertex(roadNetId, road, roadIndex, vertexIndex, worldPosition, ctx);
        
        Logger.DevkitServer.LogDebug(Source, $"Granted request for instantiation of road vertex at {worldPosition.Format()}, index: {vertexIndex.Format()} to road #{roadIndex.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }
#endif
    /// <summary>
    /// Gets the opposite handle to <paramref name="handle"/>.
    /// </summary>
    public static TangentHandle OtherHandle(this TangentHandle handle) => (TangentHandle)(1 - (int)handle);

    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static int GetRoadIndex(this Road road)
    {
        ThreadUtil.assertIsGameThread();

        return !Level.isEditor ? road.roadIndex : LevelRoads.getRoadIndex(road);
    }

    /// <summary>
    /// Get a text-based identifier for a <see cref="RoadMaterial"/>, which is the texture name if available, otherwie the material index.
    /// </summary>
    public static string MaterialToString(int materialIndex)
    {
        // material textures aren't filled on the server.
#if SERVER
        return "Material #" + materialIndex.ToString(CultureInfo.InvariantCulture);
#else
        return LevelRoads.materials[materialIndex].material?.mainTexture?.name ?? ("Material #" + materialIndex.ToString(CultureInfo.InvariantCulture));
#endif
    }

    /// <summary>
    /// Get a text-based identifier for a <see cref="RoadMaterial"/>, which is the texture name if available, otherwie the material index.
    /// </summary>
    public static string MaterialToString(this RoadMaterial material)
    {
#if SERVER
        return "Material #" + Array.IndexOf(LevelRoads.materials, material).ToString(CultureInfo.InvariantCulture);
#else
        return material.material?.mainTexture?.name ?? ("Material #" + Array.IndexOf(LevelRoads.materials, material).ToString(CultureInfo.InvariantCulture));
#endif
    }

    /// <summary>
    /// Get a text-based identifier for a <see cref="RoadMaterial"/>, which is the texture name if available, otherwie the material index.
    /// </summary>
    public static string MaterialToString(Road road)
    {
        if (road.GetRoadAsset() is { } asset)
        {
            return asset.FriendlyName;
        }

        return MaterialToString(road.material);
    }

    /// <summary>
    /// Remesh all roads with the material <paramref name="materialIndex"/>.
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public static void BakeRoadsWithMaterial(int materialIndex)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = 0; i < ushort.MaxValue; ++i)
        {
            Road? road = LevelRoads.getRoad(i);
            if (road == null)
                break;

            if (road.material == materialIndex)
                road.buildMesh();
        }
    }

    /// <summary>
    /// Locally set <see cref="RoadJoint.mode"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="mode"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexTangentHandleModeLocal(Road road, int vertexIndex, ERoadMode mode)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RoadJoint joint = road.joints[vertexIndex];
        if (joint.mode == mode)
            return false;

        SetVertexTangentHandleModeLocal(road, roadIndex, joint, vertexIndex, mode);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="RoadJoint.mode"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="mode"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexTangentHandleModeLocal(int roadIndex, int vertexIndex, ERoadMode mode)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RoadJoint joint = road.joints[vertexIndex];
        if (joint.mode == mode)
            return false;

        SetVertexTangentHandleModeLocal(road, roadIndex, joint, vertexIndex, mode);
        return true;
    }

    internal static void SetVertexTangentHandleModeLocal(Road road, int roadIndex, RoadJoint joint, int vertexIndex, ERoadMode mode)
    {
        ERoadMode oldMode = joint.mode;
        joint.mode = mode;
        EventOnVertexTangentHandleModeUpdated.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex), oldMode, mode);

        Logger.DevkitServer.LogDebug(nameof(SetVertexTangentHandleModeLocal), $"Vertex mode updated: {roadIndex.Format()}/{vertexIndex.Format()} {oldMode.Format()} -> {mode.Format()}.");

        SyncIfAuthority(roadIndex);

        // todo update UI
    }

    /// <summary>
    /// Locally set <see cref="Road.isLoop"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="isLoop"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetIsLoopLocal(Road road, bool isLoop)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (road.isLoop == isLoop)
            return false;

        SetIsLoopLocal(road, roadIndex, isLoop);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="Road.isLoop"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="isLoop"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetIsLoopLocal(int roadIndex, bool isLoop)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (road.isLoop == isLoop)
            return false;

        SetIsLoopLocal(road, roadIndex, isLoop);
        return true;
    }
    internal static void SetIsLoopLocal(Road road, int roadIndex, bool isLoop)
    {
        road.isLoop = isLoop;

        EventOnIsLoopUpdated.TryInvoke(road, roadIndex, isLoop);

        Logger.DevkitServer.LogDebug(nameof(SetIsLoopLocal), $"Road is loop updated: {roadIndex.Format()} {(!isLoop).Format()} -> {isLoop.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        // todo update UI
    }

    /// <summary>
    /// Gets a road's material as a <see cref="RoadMaterialOrAsset"/>.
    /// </summary>
    public static RoadMaterialOrAsset GetMaterial(this Road road)
    {
        RoadAsset? roadAsset = road.GetRoadAsset();
        return roadAsset != null ? new RoadMaterialOrAsset(roadAsset.GUID) : new RoadMaterialOrAsset(road.material);
    }

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static bool SetMaterialLocal(Road road, int materialIndex)
    {
        return SetMaterialLocal(road, new RoadMaterialOrAsset(checked ( (byte)materialIndex )));
    }

    /// <summary>
    /// Locally set <see cref="Road.material"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="materialIndex"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialLocal(Road road, RoadMaterialOrAsset material)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (material.IsLegacyMaterial)
        {
            if (!material.TryGetMaterial(out RoadMaterial? mat))
                throw new ArgumentOutOfRangeException(nameof(material), $"Material #{material.LegacyIndex} does not exist.");

            if (road.material == material.LegacyIndex && road.RoadAssetRef.IsEmpty)
                return false;

            SetMaterialLocal(road, roadIndex, mat, material.LegacyIndex);
            return true;
        }

        if (!material.TryGetAsset(out RoadAsset? asset))
        {
            if (material.Guid != Guid.Empty)
                throw new ArgumentOutOfRangeException(nameof(material), $"RoadAsset {material.Guid:N} does not exist.");
        }

        if (road.RoadAssetRef.Guid == material.Guid)
            return false;

        SetMaterialLocal(road, roadIndex, asset, material.Guid);
        return true;
    }

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static bool SetMaterialLocal(int roadIndex, int materialIndex)
    {
        return SetMaterialLocal(roadIndex, new RoadMaterialOrAsset(checked((byte)materialIndex)));
    }

    /// <summary>
    /// Locally set <see cref="Road.material"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="materialIndex"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialLocal(int roadIndex, RoadMaterialOrAsset material)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (material.IsLegacyMaterial)
        {
            if (!material.TryGetMaterial(out RoadMaterial? mat))
                throw new ArgumentOutOfRangeException(nameof(material), $"Material #{material.LegacyIndex} does not exist.");

            if (road.material == material.LegacyIndex && road.RoadAssetRef.IsEmpty)
                return false;

            SetMaterialLocal(road, roadIndex, mat, material.LegacyIndex);
            return true;
        }

        if (!material.TryGetAsset(out RoadAsset? asset))
        {
            if (material.Guid != Guid.Empty)
                throw new ArgumentOutOfRangeException(nameof(material), $"RoadAsset {material.Guid:N} does not exist.");
        }

        if (road.RoadAssetRef.Guid == material.Guid)
            return false;

        SetMaterialLocal(road, roadIndex, asset, material.Guid);
        return true;
    }

    internal static void SetMaterialLocal(Road road, int roadIndex, RoadMaterial material, byte materialIndex)
    {
        RoadMaterialOrAsset oldMaterial = road.GetMaterial();
        road.material = materialIndex;
        road.RoadAssetRef = CachingAssetRef.Empty;

#pragma warning disable CS0618
        if (oldMaterial.IsLegacyMaterial && oldMaterial.LegacyIndex < LevelRoads.materials.Length)
        {
            EventOnMaterialUpdated.TryInvoke(road, roadIndex, oldMaterial.LegacyIndex, materialIndex, LevelRoads.materials[oldMaterial.LegacyIndex], LevelRoads.materials[materialIndex]);
        }
#pragma warning restore CS0618

        EventOnMaterialOrAssetUpdated.TryInvoke(road, roadIndex, oldMaterial, new RoadMaterialOrAsset(materialIndex));

        Logger.DevkitServer.LogDebug(nameof(SetMaterialLocal), $"Road material updated: {roadIndex.Format()} {oldMaterial.Format()} -> {materialIndex.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        // todo update UI
    }

    internal static void SetMaterialLocal(Road road, int roadIndex, RoadAsset? asset, Guid assetGuid)
    {
        RoadMaterialOrAsset oldMaterial = road.GetMaterial();
        road.material = byte.MaxValue;
        road.RoadAssetRef = new CachingAssetRef(asset);

        EventOnMaterialOrAssetUpdated.TryInvoke(road, roadIndex, oldMaterial, new RoadMaterialOrAsset(assetGuid));

        Logger.DevkitServer.LogDebug(nameof(SetMaterialLocal), $"Road material updated: {roadIndex.Format()} {oldMaterial.Format()} -> {assetGuid.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        // todo update UI
    }

    /// <summary>
    /// Locally set <see cref="RoadJoint.ignoreTerrain"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="ignoreTerrain"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexIgnoreTerrainLocal(Road road, int vertexIndex, bool ignoreTerrain)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RoadJoint joint = road.joints[vertexIndex];

        if (joint.ignoreTerrain == ignoreTerrain)
            return false;

        SetVertexIgnoreTerrainLocal(road, roadIndex, joint, vertexIndex, ignoreTerrain);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="RoadJoint.ignoreTerrain"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="ignoreTerrain"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexIgnoreTerrainLocal(int roadIndex, int vertexIndex, bool ignoreTerrain)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RoadJoint joint = road.joints[vertexIndex];

        if (joint.ignoreTerrain == ignoreTerrain)
            return false;

        SetVertexIgnoreTerrainLocal(road, roadIndex, joint, vertexIndex, ignoreTerrain);
        return true;
    }

    internal static void SetVertexIgnoreTerrainLocal(Road road, int roadIndex, RoadJoint joint, int vertexIndex, bool ignoreTerrain)
    {
        joint.ignoreTerrain = ignoreTerrain;
        EventOnVertexIgnoreTerrainUpdated.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex), ignoreTerrain);

        Logger.DevkitServer.LogDebug(nameof(SetVertexIgnoreTerrainLocal), $"Vertex ignore terrain updated: {roadIndex.Format()}/{vertexIndex.Format()} {(!joint.ignoreTerrain).Format()} -> {joint.ignoreTerrain.Format()}.");

        road.updatePoints();
        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        // todo update UI
    }

    /// <summary>
    /// Locally set <see cref="RoadJoint.offset"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="verticalOffset"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexVerticalOffsetLocal(Road road, int vertexIndex, float verticalOffset)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RoadJoint joint = road.joints[vertexIndex];

        float oldVerticalOffset = joint.offset;
        if (oldVerticalOffset == verticalOffset)
            return false;

        SetVertexVerticalOffsetLocal(road, roadIndex, joint, vertexIndex, verticalOffset);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="RoadJoint.offset"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="verticalOffset"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexVerticalOffsetLocal(int roadIndex, int vertexIndex, float verticalOffset)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RoadJoint joint = road.joints[vertexIndex];

        float oldVerticalOffset = joint.offset;
        if (oldVerticalOffset == verticalOffset)
            return false;

        SetVertexVerticalOffsetLocal(road, roadIndex, joint, vertexIndex, verticalOffset);
        return true;
    }

    internal static void SetVertexVerticalOffsetLocal(Road road, int roadIndex, RoadJoint joint, int vertexIndex, float verticalOffset)
    {
        float oldVerticalOffset = joint.offset;
        joint.offset = verticalOffset;

        EventOnVertexVerticalOffsetUpdated.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex), oldVerticalOffset, verticalOffset);

        Logger.DevkitServer.LogDebug(nameof(SetVertexVerticalOffsetLocal), $"Vertex vertical offset updated: {roadIndex.Format()}/{vertexIndex.Format()} {oldVerticalOffset.Format()} -> {verticalOffset.Format()}.");

        road.updatePoints();
        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        // todo update UI
    }

    /// <summary>
    /// Locally call <see cref="Road.moveVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetVertexPositionLocal(Road road, int vertexIndex, Vector3 worldPosition)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetVertexPositionLocal(road, roadIndex, vertexIndex, worldPosition);
    }

    /// <summary>
    /// Locally call <see cref="Road.moveVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetVertexPositionLocal(int roadIndex, int vertexIndex, Vector3 worldPosition)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetVertexPositionLocal(road, roadIndex, vertexIndex, worldPosition);
    }
    internal static void SetVertexPositionLocal(Road road, int roadIndex, int vertexIndex, Vector3 worldPosition)
    {
        Vector3 oldPos = road.joints[vertexIndex].vertex;
        road.moveVertex(vertexIndex, worldPosition);

        EventOnVertexMoved.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex), oldPos, worldPosition);

        Logger.DevkitServer.LogDebug(nameof(SetVertexPositionLocal), $"Vertex moved: {roadIndex.Format()}/{vertexIndex.Format()} {oldPos.Format("F1")} -> {worldPosition.Format("F1")}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);
    }

    /// <summary>
    /// Locally call <see cref="Road.moveTangent"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetTangentHandlePositionLocal(Road road, int vertexIndex, TangentHandle handle, Vector3 relativePosition)
    {
        ThreadUtil.assertIsGameThread();

        if (handle is not TangentHandle.Negative and not TangentHandle.Positive)
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle must be negative (0) or positive (1).");

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetTangentHandlePositionLocal(road, roadIndex, vertexIndex, handle, relativePosition);
    }

    /// <summary>
    /// Locally call <see cref="Road.moveTangent"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetTangentHandlePositionLocal(int roadIndex, int vertexIndex, TangentHandle handle, Vector3 relativePosition)
    {
        ThreadUtil.assertIsGameThread();

        if (handle is not TangentHandle.Negative and not TangentHandle.Positive)
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle must be negative (0) or positive (1).");

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetTangentHandlePositionLocal(road, roadIndex, vertexIndex, handle, relativePosition);
    }
    internal static void SetTangentHandlePositionLocal(Road road, int roadIndex, int vertexIndex, TangentHandle handle, Vector3 relativePosition)
    {
        RoadJoint joint = road.joints[vertexIndex];
        Vector3 oldPos = joint.getTangent((int)handle);
        Vector3 oldReflectionPos = joint.getTangent(1 - (int)handle);
        road.moveTangent(vertexIndex, (int)handle, relativePosition);
        EventOnTangentHandleMoved.TryInvoke(road, new RoadTangentHandleIdentifier(roadIndex, vertexIndex, handle), oldPos, relativePosition, false);

        Vector3 reflectionPos = joint.getTangent(1 - (int)handle);
        if (reflectionPos != oldReflectionPos)
            EventOnTangentHandleMoved.TryInvoke(road, new RoadTangentHandleIdentifier(roadIndex, vertexIndex, (TangentHandle)(1 - (int)handle)), oldReflectionPos, reflectionPos, true);

        Logger.DevkitServer.LogDebug(nameof(SetTangentHandlePositionLocal), $"Tangent handle moved: {roadIndex.Format()}/{vertexIndex.Format()}/{handle.Format()} {oldPos.Format("F1")} -> {relativePosition.Format("F1")}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);
    }

    /// <summary>
    /// Locally call <see cref="Road.addVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns>The <see cref="Transform"/> of the new vertex.</returns>
    public static Transform AddVertexLocal(Road road, int vertexIndex, Vector3 worldPosition)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex > road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Index {vertexIndex} is not available for a new joint.");

        Transform transform = AddVertexLocal(road, roadIndex, vertexIndex, worldPosition);
        return transform;
    }

    /// <summary>
    /// Locally call <see cref="Road.addVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns>The <see cref="Transform"/> of the new vertex.</returns>
    public static Transform AddVertexLocal(int roadIndex, int vertexIndex, Vector3 worldPosition)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex > road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Index {vertexIndex} is not available for a new joint.");

        Transform transform = AddVertexLocal(road, roadIndex, vertexIndex, worldPosition);
        return transform;
    }
    internal static Transform AddVertexLocal(Road road, int roadIndex, int vertexIndex, Vector3 worldPosition)
    {
        Transform transform = road.addVertex(vertexIndex, worldPosition);

        for (int i = vertexIndex + 1; i < road.joints.Count; ++i)
            EventOnVertexIndexUpdated.TryInvoke(road, new RoadVertexIdentifier(roadIndex, i - 1), new RoadVertexIdentifier(roadIndex, i));

        EventOnVertexAdded.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex));

        Logger.DevkitServer.LogDebug(nameof(AddVertexLocal), $"Vertex added: {roadIndex.Format()}/{vertexIndex.Format()} {worldPosition.Format("F1")}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        return transform;
    }

    /// <summary>
    /// Locally call <see cref="Road.removeVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RemoveVertexLocal(Road road, int vertexIndex)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RemoveVertexLocal(road, roadIndex, vertexIndex);
    }

    /// <summary>
    /// Locally call <see cref="Road.removeVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RemoveVertexLocal(int roadIndex, int vertexIndex)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RemoveVertexLocal(road, roadIndex, vertexIndex);
    }
    internal static void RemoveVertexLocal(Road road, int roadIndex, int vertexIndex)
    {
        road.removeVertex(vertexIndex);

        EventOnVertexRemoved.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex));

        for (int i = vertexIndex; i < road.joints.Count; ++i)
            EventOnVertexIndexUpdated.TryInvoke(road, new RoadVertexIdentifier(roadIndex, i + 1), new RoadVertexIdentifier(roadIndex, i));

        Logger.DevkitServer.LogDebug(nameof(RemoveVertexLocal), $"Vertex removed: {roadIndex.Format()}/{vertexIndex.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);
    }

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static Transform AddRoadLocal(Vector3 firstVertexWorldPosition, byte materialIndex)
    {
        return AddRoadLocal(firstVertexWorldPosition, new RoadMaterialOrAsset(materialIndex));
    }

    /// <summary>
    /// Locally call <see cref="LevelRoads.addRoad"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Invalid asset or material index.</exception>
    /// <returns>The <see cref="Transform"/> of the first vertex of the road.</returns>
    public static Transform AddRoadLocal(Vector3 firstVertexWorldPosition, RoadMaterialOrAsset material)
    {
        ThreadUtil.assertIsGameThread();

        if (!material.CheckValid())
        {
            throw new ArgumentOutOfRangeException(nameof(material), $"Material {material} doesn't represent a valid asset or material index.");
        }

        byte selectedOld = EditorRoads.selected;
        CachingAssetRef selectedAssetRefOld = EditorRoads.selectedAssetRef;
        EditorRoads.selected = material.LegacyIndex;
        EditorRoads.selectedAssetRef = material.IsLegacyMaterial ? default : new CachingAssetRef(material.Guid);

        Transform transform = LevelRoads.addRoad(firstVertexWorldPosition);

        EditorRoads.selected = selectedOld;
        EditorRoads.selectedAssetRef = selectedAssetRefOld;

        Road road = LevelRoads.getRoad(transform, out int vertexIndex, out _);
        int roadIndex = road.GetRoadIndex();
        EventOnRoadAdded.TryInvoke(road, roadIndex);
        EventOnVertexAdded.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex));

        Logger.DevkitServer.LogDebug(nameof(AddRoadLocal), $"Road added: {roadIndex.Format()}/{vertexIndex.Format()} {firstVertexWorldPosition.Format("F1")}.");
        
        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        SyncIfAuthority(roadIndex);

        return transform;
    }

    /// <summary>
    /// Locally call <see cref="LevelRoads.removeRoad(Road)"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RemoveRoadLocal(Road road)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        RemoveRoadLocal(road, roadIndex);
    }

    /// <summary>
    /// Locally call <see cref="LevelRoads.removeRoad(Road)"/> and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RemoveRoadLocal(int roadIndex)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        RemoveRoadLocal(road, roadIndex);
    }

    internal static void RemoveRoadLocal(Road road, int roadIndex)
    {
        SyncIfAuthority(roadIndex);
#if CLIENT
        if (EditorRoads.road == road)
            ClearSelection();
#endif
        LevelRoads.removeRoad(road);


        for (int i = 0; i < road.joints.Count; ++i)
            EventOnVertexRemoved.TryInvoke(road, new RoadVertexIdentifier(roadIndex, i));
        EventOnRoadRemoved.TryInvoke(road, roadIndex);

        for (int i = roadIndex; i < ushort.MaxValue; ++i)
        {
            Road? replRoad = LevelRoads.getRoad(i);
            if (replRoad == null)
                break;

            int fromIndex = i + 1;
            EventOnRoadIndexUpdated.TryInvoke(road, fromIndex, i);
            for (int j = 0; j < road.joints.Count; ++j)
                EventOnVertexIndexUpdated.TryInvoke(road, new RoadVertexIdentifier(fromIndex, j), new RoadVertexIdentifier(i, j));
        }

        Logger.DevkitServer.LogDebug(nameof(RemoveRoadLocal), $"Road removed: {roadIndex.Format()}.");
    }

    /// <summary>
    /// Locally set the <see cref="RoadMaterial.width"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="width"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialWidthLocal(int materialIndex, float width)
    {
        ThreadUtil.assertIsGameThread();

        if (materialIndex < 0 || materialIndex >= LevelRoads.materials.Length || materialIndex > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(materialIndex), $"Material #{materialIndex} does not exist.");

        RoadMaterial material = LevelRoads.materials[materialIndex];

        float oldWidth = material.width;
        if (oldWidth == width)
            return false;

        material.width = width;

        EventOnMaterialWidthUpdated.TryInvoke(material, materialIndex, oldWidth, width);

        Logger.DevkitServer.LogDebug(nameof(SetMaterialWidthLocal), $"Material width updated: {material.MaterialToString().Format()} ({materialIndex.Format()}) {oldWidth.Format()} -> {width.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            BakeRoadsWithMaterial(materialIndex);

        SyncMaterialIfAuthority(materialIndex);

        // todo update UI
        return true;
    }

    /// <summary>
    /// Locally set the <see cref="RoadMaterial.height"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="height"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialHeightLocal(int materialIndex, float height)
    {
        ThreadUtil.assertIsGameThread();

        if (materialIndex < 0 || materialIndex >= LevelRoads.materials.Length || materialIndex > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(materialIndex), $"Material #{materialIndex} does not exist.");

        RoadMaterial material = LevelRoads.materials[materialIndex];

        float oldHeight = material.height;
        if (oldHeight == height)
            return false;

        material.height = height;

        EventOnMaterialHeightUpdated.TryInvoke(material, materialIndex, oldHeight, height);

        Logger.DevkitServer.LogDebug(nameof(SetMaterialHeightLocal), $"Material height updated: {material.MaterialToString().Format()} ({materialIndex.Format()}) {oldHeight.Format()} -> {height.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            BakeRoadsWithMaterial(materialIndex);

        SyncMaterialIfAuthority(materialIndex);

        // todo update UI
        return true;
    }

    /// <summary>
    /// Locally set the <see cref="RoadMaterial.depth"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="depth"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialDepthLocal(int materialIndex, float depth)
    {
        ThreadUtil.assertIsGameThread();

        if (materialIndex < 0 || materialIndex >= LevelRoads.materials.Length || materialIndex > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(materialIndex), $"Material #{materialIndex} does not exist.");

        RoadMaterial material = LevelRoads.materials[materialIndex];

        float oldDepth = material.depth;
        if (oldDepth == depth)
            return false;

        material.depth = depth;

        EventOnMaterialDepthUpdated.TryInvoke(material, materialIndex, oldDepth, depth);

        Logger.DevkitServer.LogDebug(nameof(SetMaterialDepthLocal), $"Material depth updated: {material.MaterialToString().Format()} ({materialIndex.Format()}) {oldDepth.Format()} -> {depth.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            BakeRoadsWithMaterial(materialIndex);

        SyncMaterialIfAuthority(materialIndex);

        // todo update UI
        return true;
    }

    /// <summary>
    /// Locally set the <see cref="RoadMaterial.offset"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="verticalOffset"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialVerticalOffsetLocal(int materialIndex, float verticalOffset)
    {
        ThreadUtil.assertIsGameThread();

        if (materialIndex < 0 || materialIndex >= LevelRoads.materials.Length || materialIndex > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(materialIndex), $"Material #{materialIndex} does not exist.");

        RoadMaterial material = LevelRoads.materials[materialIndex];

        float oldVerticalOffset = material.offset;
        if (oldVerticalOffset == verticalOffset)
            return false;

        material.offset = verticalOffset;

        EventOnMaterialVerticalOffsetUpdated.TryInvoke(material, materialIndex, oldVerticalOffset, verticalOffset);

        Logger.DevkitServer.LogDebug(nameof(SetMaterialVerticalOffsetLocal), $"Material vertical offset updated: {material.MaterialToString().Format()} ({materialIndex.Format()}) {oldVerticalOffset.Format()} -> {verticalOffset.Format()}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            BakeRoadsWithMaterial(materialIndex);

        SyncMaterialIfAuthority(materialIndex);

        // todo update UI
        return true;
    }

    /// <summary>
    /// Locally set the <see cref="RoadMaterial.isConcrete"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns><see langword="true"/> if <paramref name="isConcrete"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialIsConcreteLocal(int materialIndex, bool isConcrete)
    {
        ThreadUtil.assertIsGameThread();

        if (materialIndex < 0 || materialIndex >= LevelRoads.materials.Length || materialIndex > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(materialIndex), $"Material #{materialIndex} does not exist.");

        RoadMaterial material = LevelRoads.materials[materialIndex];
        
        if (material.isConcrete == isConcrete)
            return false;

        material.isConcrete = isConcrete;

        EventOnMaterialIsConcreteUpdated.TryInvoke(material, materialIndex, isConcrete);

        Logger.DevkitServer.LogDebug(nameof(SetMaterialIsConcreteLocal), $"Material is concrete updated: {material.MaterialToString().Format()} ({materialIndex.Format()}) {(!isConcrete).Format()} -> {isConcrete.Format()}.");
        
        // need to bake since chart colors rely on this
        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            BakeRoadsWithMaterial(materialIndex);

        SyncMaterialIfAuthority(materialIndex);

        // todo update UI
        return true;
    }

#if SERVER

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static Transform AddRoad(Vector3 firstVertexWorldPosition, byte materialIndex, EditorUser? owner = null)
    {
        return AddRoad(firstVertexWorldPosition, new RoadMaterialOrAsset(materialIndex), owner);
    }

    /// <summary>
    /// Call <see cref="LevelRoads.addRoad"/> and call the necessary events.
    /// </summary
    /// <remarks>Replicates to clients.</remarks>
    /// <param name="owner">Optional owner/placer for the road.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Road asset or material not found.</exception>
    /// <returns>The <see cref="Transform"/> of the first vertex of the road.</returns>
    public static Transform AddRoad(Vector3 firstVertexWorldPosition, RoadMaterialOrAsset material, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddRoad(firstVertexWorldPosition, material, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }

    internal static Transform AddRoad(Vector3 firstVertexWorldPosition, RoadMaterialOrAsset material, MessageContext ctx)
    {
        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        byte oldSelected = EditorRoads.selected;
        CachingAssetRef oldSelectedAssetRef = EditorRoads.selectedAssetRef;
        EditorRoads.selected = material.LegacyIndex;
        EditorRoads.selectedAssetRef = material.IsLegacyMaterial ? default : new CachingAssetRef(material.Guid);

        if (!material.CheckValid())
            throw new ArgumentOutOfRangeException(nameof(material), "Invalid material or road asset.");

        Transform transform = LevelRoads.addRoad(firstVertexWorldPosition);

        EditorRoads.selected = oldSelected;
        EditorRoads.selectedAssetRef = oldSelectedAssetRef;

        InitializeRoad(transform, out Road road, out NetId roadNetId, out NetId vertexNetId);
        int roadIndex = road.GetRoadIndex();
        EventOnRoadAdded.TryInvoke(road, roadIndex);
        EventOnVertexAdded.TryInvoke(road, new RoadVertexIdentifier(roadIndex, 0));

        Logger.DevkitServer.LogDebug(nameof(AddRoad), $"Road added: {roadIndex.Format()}/{0.Format()} {firstVertexWorldPosition.Format("F1")}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        firstVertexWorldPosition = transform.position;

        RoadJoint joint = road.joints[0];

        long netIdsPacked = ((long)vertexNetId.id << 32) | roadNetId.id;
        ushort flags = (ushort)(material.LegacyIndex | (road.isLoop ? 1 << 8 : 0) | (joint.ignoreTerrain ? 1 << 9 : 0) | ((int)joint.mode << 10));

        PooledTransportConnectionList list;
        if (!ctx.IsRequest)
            list = DevkitServerUtility.GetAllConnections();
        else
        {
            ctx.ReplyLayered(SendInstantiation, firstVertexWorldPosition, flags, material.Guid, joint.getTangent(0), joint.getTangent(1), joint.offset, netIdsPacked, owner);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendInstantiation.Invoke(list, firstVertexWorldPosition, flags, material.Guid, joint.getTangent(0), joint.getTangent(1), joint.offset, netIdsPacked, owner);

        SyncIfAuthority(roadIndex);

        return transform;
    }

    /// <summary>
    /// Call <see cref="Road.addVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns>The <see cref="Transform"/> of the new vertex.</returns>
    public static Transform AddVertex(Road road, int vertexIndex, Vector3 worldPosition, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex > road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Index {vertexIndex} is not available for a new joint.");

        if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            throw new ArgumentException(nameof(roadIndex), $"Unable to find NetId for road: #{roadIndex}.");

        Transform transform = AddVertex(roadNetId, road, roadIndex, vertexIndex, worldPosition, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
        return transform;
    }

    /// <summary>
    /// Call <see cref="Road.addVertex"/> and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <returns>The <see cref="Transform"/> of the new vertex.</returns>
    public static Transform AddVertex(int roadIndex, int vertexIndex, Vector3 worldPosition, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex > road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Index {vertexIndex} is not available for a new joint.");

        if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            throw new ArgumentException(nameof(roadIndex), $"Unable to find NetId for road: #{roadIndex}.");

        Transform transform = AddVertex(roadNetId, road, roadIndex, vertexIndex, worldPosition, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
        return transform;
    }
    internal static Transform AddVertex(NetId roadNetId, Road road, int roadIndex, int vertexIndex, Vector3 worldPosition, MessageContext ctx)
    {
        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;
        
        Transform transform = road.addVertex(vertexIndex, worldPosition);

        for (int i = vertexIndex + 1; i < road.joints.Count; ++i)
            EventOnVertexIndexUpdated.TryInvoke(road, new RoadVertexIdentifier(roadIndex, i - 1), new RoadVertexIdentifier(roadIndex, i));

        InitializeVertex(road, roadIndex, vertexIndex, out NetId netId);

        EventOnVertexAdded.TryInvoke(road, new RoadVertexIdentifier(roadIndex, vertexIndex));

        Logger.DevkitServer.LogDebug(nameof(AddVertex), $"Vertex added: {roadIndex.Format()}/{vertexIndex.Format()} {worldPosition.Format("F1")}.");

        if (!HoldBake && !DevkitServerConfig.RemoveCosmeticImprovements)
            road.buildMesh();

        worldPosition = transform.position;

        RoadJoint joint = road.joints[vertexIndex];

        PooledTransportConnectionList list;
        if (!ctx.IsRequest)
            list = DevkitServerUtility.GetAllConnections();
        else
        {
            ctx.ReplyLayered(SendVertexInstantiation, roadNetId, worldPosition, joint.getTangent(0), joint.getTangent(1), vertexIndex, joint.mode, joint.offset, joint.ignoreTerrain, netId, owner);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendVertexInstantiation.Invoke(list, roadNetId, worldPosition, joint.getTangent(0), joint.getTangent(1), vertexIndex, joint.mode, joint.offset, joint.ignoreTerrain, netId, owner);

        SyncIfAuthority(roadIndex);

        return transform;
    }
#endif

    /// <summary>
    /// Set the <see cref="RoadMaterial.isConcrete"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoadMaterials"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="isConcrete"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialIsConcrete(int materialIndex, bool isConcrete)
    {
        ThreadUtil.assertIsGameThread();

        if (!SetMaterialIsConcreteLocal(materialIndex, isConcrete))
            return false;
        if (!DevkitServerModule.IsEditing)
            return true;
#if CLIENT
        CheckEditMaterial();
        ClientEvents.InvokeOnSetRoadMaterialIsConcrete(new SetRoadMaterialIsConcreteProperties((byte)materialIndex, isConcrete, CachedTime.DeltaTime));
#else
        EditorActions.QueueServerAction(new SetRoadMaterialIsConcreteAction
        {
            DeltaTime = CachedTime.DeltaTime,
            InstanceId = (uint)materialIndex,
            IsConcrete = isConcrete
        });
#endif
        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadMaterial.width"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoadMaterials"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="width"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialWidth(int materialIndex, float width)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        CheckEditMaterial();
        float old = materialIndex < 0 || materialIndex >= LevelRoads.materials.Length ? 0 : LevelRoads.materials[materialIndex].width;
#endif
        if (!SetMaterialWidthLocal(materialIndex, width))
            return false;
        if (!DevkitServerModule.IsEditing)
            return true;
#if CLIENT
        ClientEvents.InvokeOnSetRoadMaterialWidth(new SetRoadMaterialWidthProperties((byte)materialIndex, width, old, CachedTime.DeltaTime));
#else
        EditorActions.QueueServerAction(new SetRoadMaterialWidthAction
        {
            DeltaTime = CachedTime.DeltaTime,
            InstanceId = (uint)materialIndex,
            Width = width
        });
#endif
        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadMaterial.height"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoadMaterials"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="height"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialHeight(int materialIndex, float height)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        CheckEditMaterial();
        float old = materialIndex < 0 || materialIndex >= LevelRoads.materials.Length ? 0 : LevelRoads.materials[materialIndex].height;
#endif
        if (!SetMaterialHeightLocal(materialIndex, height))
            return false;
        if (!DevkitServerModule.IsEditing)
            return true;
#if CLIENT
        ClientEvents.InvokeOnSetRoadMaterialHeight(new SetRoadMaterialHeightProperties((byte)materialIndex, height, old, CachedTime.DeltaTime));
#else
        EditorActions.QueueServerAction(new SetRoadMaterialHeightAction
        {
            DeltaTime = CachedTime.DeltaTime,
            InstanceId = (uint)materialIndex,
            Height = height
        });
#endif
        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadMaterial.depth"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoadMaterials"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="depth"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialDepth(int materialIndex, float depth)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        CheckEditMaterial();
        float old = materialIndex < 0 || materialIndex >= LevelRoads.materials.Length ? 0 : LevelRoads.materials[materialIndex].depth;
#endif
        if (!SetMaterialDepthLocal(materialIndex, depth))
            return false;
        if (!DevkitServerModule.IsEditing)
            return true;
#if CLIENT
        ClientEvents.InvokeOnSetRoadMaterialDepth(new SetRoadMaterialDepthProperties((byte)materialIndex, depth, old, CachedTime.DeltaTime));
#else
        EditorActions.QueueServerAction(new SetRoadMaterialDepthAction
        {
            DeltaTime = CachedTime.DeltaTime,
            InstanceId = (uint)materialIndex,
            Depth = depth
        });
#endif
        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadMaterial.offset"/> property on a material and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoadMaterials"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="verticalOffset"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterialVerticalOffset(int materialIndex, float verticalOffset)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        CheckEditMaterial();
        float old = materialIndex < 0 || materialIndex >= LevelRoads.materials.Length ? 0 : LevelRoads.materials[materialIndex].offset;
#endif
        if (!SetMaterialVerticalOffsetLocal(materialIndex, verticalOffset))
            return false;
        if (!DevkitServerModule.IsEditing)
            return true;
#if CLIENT
        ClientEvents.InvokeOnSetRoadMaterialVerticalOffset(new SetRoadMaterialVerticalOffsetProperties((byte)materialIndex, verticalOffset, old, CachedTime.DeltaTime));
#else
        EditorActions.QueueServerAction(new SetRoadMaterialVerticalOffsetAction
        {
            DeltaTime = CachedTime.DeltaTime,
            InstanceId = (uint)materialIndex,
            VerticalOffset = verticalOffset
        });
#endif
        return true;
    }

    /// <summary>
    /// Set the <see cref="Road.isLoop"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="isLoop"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetIsLoop(Road road, bool isLoop)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        return SetIsLoop(road, roadIndex, isLoop);
    }

    /// <summary>
    /// Set the <see cref="Road.isLoop"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="isLoop"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetIsLoop(int roadIndex, bool isLoop)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        return SetIsLoop(road, roadIndex, isLoop);
    }
    private static bool SetIsLoop(Road road, int roadIndex, bool isLoop)
    {
#if CLIENT
        CheckEditRoad();
#endif
        if (road.isLoop == isLoop)
            return false;

        SetIsLoopLocal(road, roadIndex, isLoop);

        if (DevkitServerModule.IsEditing)
        {
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId netId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetIsLoop), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetIsLoop({roadIndex.Format()}, {isLoop.Format()}).");
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetRoadIsLoop(new SetRoadIsLoopProperties(netId, isLoop, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetRoadIsLoopAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                IsLoop = isLoop
            });
#endif
        }

        return true;
    }

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static bool SetMaterial(Road road, int materialIndex)
    {
        return SetMaterial(road, new RoadMaterialOrAsset(checked((byte)materialIndex)));
    }

    /// <summary>
    /// Set the <see cref="Road.material"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="materialIndex"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterial(Road road, RoadMaterialOrAsset material)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        return SetMaterial(road, roadIndex, material);
    }

    [Obsolete("Use the overload with RoadMaterialOrAsset instead.")]
    public static bool SetMaterial(int roadIndex, int materialIndex)
    {
        return SetMaterial(roadIndex, new RoadMaterialOrAsset(checked((byte)materialIndex)));
    }

    /// <summary>
    /// Set the <see cref="Road.isLoop"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="materialIndex"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetMaterial(int roadIndex, RoadMaterialOrAsset material)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        return SetMaterial(road, roadIndex, material);
    }

    private static bool SetMaterial(Road road, int roadIndex, RoadMaterialOrAsset material)
    {
#if CLIENT
        CheckEditRoad();
#endif
        if (material.IsSameAsMaterialOf(road))
            return false;

        SetMaterialLocal(roadIndex, material);

        if (DevkitServerModule.IsEditing)
        {
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId netId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetMaterial), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetMaterial({roadIndex.Format()}, {material.Format()}).");
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetRoadMaterial(new SetRoadMaterialProperties(netId, material, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetRoadMaterialAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                Material = material
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadJoint.ignoreTerrain"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="ignoreTerrain"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexIgnoreTerrain(Road road, int vertexIndex, bool ignoreTerrain)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        return SetVertexIgnoreTerrain(road, roadIndex, vertexIndex, ignoreTerrain);
    }

    /// <summary>
    /// Set the <see cref="RoadJoint.ignoreTerrain"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="ignoreTerrain"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexIgnoreTerrain(int roadIndex, int vertexIndex, bool ignoreTerrain)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        return SetVertexIgnoreTerrain(road, roadIndex, vertexIndex, ignoreTerrain);
    }
    private static bool SetVertexIgnoreTerrain(Road road, int roadIndex, int vertexIndex, bool ignoreTerrain)
    {
#if CLIENT
        CheckEditRoad();
#endif
        RoadJoint joint = road.joints[vertexIndex];

        if (joint.ignoreTerrain == ignoreTerrain)
            return false;

        SetVertexIgnoreTerrainLocal(road, roadIndex, joint, vertexIndex, ignoreTerrain);

        if (DevkitServerModule.IsEditing)
        {
#if CLIENT
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexIgnoreTerrain), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetVertexIgnoreTerrain({roadIndex.Format()}, {vertexIndex.Format()}, {ignoreTerrain.Format()}).");
                return true;
            }
#endif
            if (!RoadNetIdDatabase.TryGetVertexNetId(roadIndex, vertexIndex, out NetId vertexMetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexIgnoreTerrain), $"Failed to find NetId for vertex {roadIndex.Format()}/{roadIndex.Format()}. Did not replicate in SetVertexIgnoreTerrain({roadIndex.Format()}, {vertexIndex.Format()}, {ignoreTerrain.Format()}).");
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetRoadVertexIgnoreTerrain(new SetRoadVertexIgnoreTerrainProperties(roadNetId, vertexMetId, ignoreTerrain, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetRoadVertexIgnoreTerrainAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = vertexMetId.id,
                IgnoreTerrain = ignoreTerrain
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadJoint.offset"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="verticalOffset"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexVerticalOffset(Road road, int vertexIndex, float verticalOffset)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        return SetVertexVerticalOffset(road, roadIndex, vertexIndex, verticalOffset);
    }

    /// <summary>
    /// Set the <see cref="RoadJoint.offset"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="verticalOffset"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexVerticalOffset(int roadIndex, int vertexIndex, float verticalOffset)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        return SetVertexVerticalOffset(road, roadIndex, vertexIndex, verticalOffset);
    }
    
    private static bool SetVertexVerticalOffset(Road road, int roadIndex, int vertexIndex, float verticalOffset)
    {
        RoadJoint joint = road.joints[vertexIndex];
#if CLIENT
        CheckEditRoad();
        float oldVerticalOffset = joint.offset;
#endif

        if (joint.offset == verticalOffset)
            return false;

        SetVertexVerticalOffsetLocal(road, roadIndex, joint, vertexIndex, verticalOffset);

        if (DevkitServerModule.IsEditing)
        {
#if CLIENT
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexVerticalOffset), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetVertexVerticalOffset({roadIndex.Format()}, {vertexIndex.Format()}, {verticalOffset.Format()}).");
                return true;
            }
#endif
            if (!RoadNetIdDatabase.TryGetVertexNetId(roadIndex, vertexIndex, out NetId vertexMetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexVerticalOffset), $"Failed to find NetId for vertex {roadIndex.Format()}/{vertexIndex.Format()}. Did not replicate in SetVertexVerticalOffset({roadIndex.Format()}, {vertexIndex.Format()}, {verticalOffset.Format()}).");
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetRoadVertexVerticalOffset(new SetRoadVertexVerticalOffsetProperties(roadNetId, vertexMetId, verticalOffset, oldVerticalOffset, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetRoadVertexVerticalOffsetAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = vertexMetId.id,
                VerticalOffset = verticalOffset
            });
#endif
        }
        return true;
    }

    /// <summary>
    /// Set the <see cref="RoadJoint.mode"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="tangentHandleMode"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexTangentHandleMode(Road road, int vertexIndex, ERoadMode tangentHandleMode)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        return SetVertexTangentHandleMode(road, roadIndex, vertexIndex, tangentHandleMode);
    }

    /// <summary>
    /// Set the <see cref="RoadJoint.offset"/> property and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="tangentHandleMode"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetVertexTangentHandleMode(int roadIndex, int vertexIndex, ERoadMode tangentHandleMode)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        return SetVertexTangentHandleMode(road, roadIndex, vertexIndex, tangentHandleMode);
    }
    
    private static bool SetVertexTangentHandleMode(Road road, int roadIndex, int vertexIndex, ERoadMode tangentHandleMode)
    {
        RoadJoint joint = road.joints[vertexIndex];
#if CLIENT
        CheckEditRoad();
        ERoadMode oldTangentHandleMode = joint.mode;
#endif

        if (joint.mode == tangentHandleMode)
            return false;

        SetVertexTangentHandleModeLocal(road, roadIndex, joint, vertexIndex, tangentHandleMode);

        if (DevkitServerModule.IsEditing)
        {
#if CLIENT
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexTangentHandleMode), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetVertexTangentHandleMode({roadIndex.Format()}, {vertexIndex.Format()}, {tangentHandleMode.Format()}).");
                return true;
            }
#endif
            if (!RoadNetIdDatabase.TryGetVertexNetId(roadIndex, vertexIndex, out NetId vertexMetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexTangentHandleMode), $"Failed to find NetId for vertex {roadIndex.Format()}/{vertexIndex.Format()}. Did not replicate in SetVertexTangentHandleMode({roadIndex.Format()}, {vertexIndex.Format()}, {tangentHandleMode.Format()}).");
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetRoadVertexTangentHandleMode(new SetRoadVertexTangentHandleModeProperties(roadNetId, vertexMetId, tangentHandleMode, oldTangentHandleMode, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetRoadVertexTangentHandleModeAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = vertexMetId.id,
                TangentHandleMode = tangentHandleMode
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set the root position of a vertex and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void SetVertexPosition(Road road, int vertexIndex, Vector3 worldPosition)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetVertexPosition(road, roadIndex, vertexIndex, worldPosition);
    }

    /// <summary>
    /// Set the root position of a vertex and call the necessary events.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void SetVertexPosition(int roadIndex, int vertexIndex, Vector3 worldPosition)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetVertexPosition(road, roadIndex, vertexIndex, worldPosition);
    }

    internal static void SetVertexPosition(Road road, int roadIndex, int vertexIndex, Vector3 worldPosition)
    {
#if CLIENT
        CheckEditRoad();
        Vector3 oldWorldPosition = road.joints[vertexIndex].vertex;
#endif

        SetVertexPositionLocal(road, roadIndex, vertexIndex, worldPosition);

        if (DevkitServerModule.IsEditing)
        {
#if CLIENT
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexPosition), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetVertexPosition({roadIndex.Format()}, {vertexIndex.Format()}, {worldPosition.Format()}).");
                return;
            }
#endif
            if (!RoadNetIdDatabase.TryGetVertexNetId(roadIndex, vertexIndex, out NetId vertexMetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetVertexPosition), $"Failed to find NetId for vertex {roadIndex.Format()}/{vertexIndex.Format()}. Did not replicate in SetVertexPosition({roadIndex.Format()}, {vertexIndex.Format()}, {worldPosition.Format()}).");
                return;
            }

#if CLIENT
            ClientEvents.InvokeOnMoveRoadVertex(new MoveRoadVertexProperties(roadNetId, vertexMetId, oldWorldPosition, worldPosition, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new MoveRoadVertexAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = vertexMetId.id,
                Position = worldPosition
            });
#endif
        }
    }

    /// <summary>
    /// Set the relative position of a vertex's tangent handle and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void SetTangentHandlePosition(Road road, int vertexIndex, TangentHandle handle, Vector3 relativePosition)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetTangentHandlePosition(road, roadIndex, vertexIndex, handle, relativePosition);
    }

    /// <summary>
    /// Set the relative position of a vertex's tangent handle and call the necessary events.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void SetTangentHandlePosition(int roadIndex, int vertexIndex, TangentHandle handle, Vector3 relativePosition)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        SetTangentHandlePosition(road, roadIndex, vertexIndex, handle, relativePosition);
    }

    internal static void SetTangentHandlePosition(Road road, int roadIndex, int vertexIndex, TangentHandle handle, Vector3 relativePosition)
    {
#if CLIENT
        CheckEditRoad();
        Vector3 oldRelativePosition = road.joints[vertexIndex].getTangent((int)handle);
#endif

        SetTangentHandlePositionLocal(road, roadIndex, vertexIndex, handle, relativePosition);

        if (DevkitServerModule.IsEditing)
        {
#if CLIENT
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetTangentHandlePosition), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in SetTangentHandlePosition({roadIndex.Format()}, {vertexIndex.Format()}, {handle.Format()}, {relativePosition.Format()}).");
                return;
            }
#endif
            if (!RoadNetIdDatabase.TryGetVertexNetId(roadIndex, vertexIndex, out NetId vertexMetId))
            {
                Logger.DevkitServer.LogWarning(nameof(SetTangentHandlePosition), $"Failed to find NetId for vertex {roadIndex.Format()}/{vertexIndex.Format()}. Did not replicate in SetTangentHandlePosition({roadIndex.Format()}, {vertexIndex.Format()}, {handle.Format()}, {relativePosition.Format()}).");
                return;
            }

#if CLIENT
            ClientEvents.InvokeOnMoveRoadTangentHandle(new MoveRoadTangentHandleProperties(roadNetId, vertexMetId, oldRelativePosition, handle, relativePosition, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new MoveRoadTangentHandleAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = vertexMetId.id,
                Position = relativePosition,
                Handle = handle
            });
#endif
        }
    }

    /// <summary>
    /// Delete a vertex and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void RemoveVertex(Road road, int vertexIndex)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RemoveVertex(road, roadIndex, vertexIndex);
    }

    /// <summary>
    /// Delete a vertex and call the necessary events.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void RemoveVertex(int roadIndex, int vertexIndex)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        if (vertexIndex >= road.joints.Count || vertexIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Joint #{vertexIndex} does not exist.");

        RemoveVertex(road, roadIndex, vertexIndex);
    }

    internal static void RemoveVertex(Road road, int roadIndex, int vertexIndex)
    {
#if CLIENT
        CheckEditRoad();
        Vector3 oldPosition = road.joints[vertexIndex].vertex;
#endif

        RemoveVertexLocal(road, roadIndex, vertexIndex);

        if (DevkitServerModule.IsEditing)
        {
#if CLIENT
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(RemoveVertex), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in RemoveVertex({roadIndex.Format()}, {vertexIndex.Format()}).");
                return;
            }
#endif
            if (!RoadNetIdDatabase.TryGetVertexNetId(roadIndex, vertexIndex, out NetId vertexMetId))
            {
                Logger.DevkitServer.LogWarning(nameof(RemoveVertex), $"Failed to find NetId for vertex {roadIndex.Format()}/{vertexIndex.Format()}. Did not replicate in RemoveVertex({roadIndex.Format()}, {vertexIndex.Format()}).");
                return;
            }

#if CLIENT
            ClientEvents.InvokeOnDeleteRoadVertex(new DeleteRoadVertexProperties(roadNetId, vertexMetId, oldPosition, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new DeleteRoadVertexAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = vertexMetId.id
            });
#endif
        }
    }

    /// <summary>
    /// Delete a vertex and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void RemoveRoad(Road road)
    {
        ThreadUtil.assertIsGameThread();

        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));

        RemoveRoad(road, roadIndex);
    }

    /// <summary>
    /// Delete a vertex and call the necessary events.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditRoads"/>.</exception>
    public static void RemoveRoad(int roadIndex)
    {
        ThreadUtil.assertIsGameThread();

        Road? road = LevelRoads.getRoad(roadIndex);
        if (road == null)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Road #{roadIndex} does not exist.");

        RemoveRoad(road, roadIndex);
    }
    public static bool SyncIfAuthority(int roadIndex)
    {
        if (!CheckSync(out RoadSync sync))
            return false;
        sync.EnqueueSync(roadIndex);
        return true;
    }
    public static bool SyncMaterialIfAuthority(int materialIndex)
    {
        if (!CheckSync(out RoadSync sync) || materialIndex < 0 || materialIndex > byte.MaxValue || materialIndex >= LevelRoads.materials.Length)
            return false;
        sync.EnqueueMaterialSync((byte)materialIndex);
        return true;
    }
    public static bool SyncIfAuthority(Road road)
    {
        if (!CheckSync(out RoadSync sync))
            return false;
        sync.EnqueueSync(road);
        return true;
    }
    public static bool SyncIfAuthority(NetId netId)
    {
        if (!CheckSync(out RoadSync sync))
            return false;
        sync.EnqueueSync(netId);
        return true;
    }
    internal static bool CheckSync(out RoadSync sync)
    {
        sync = null!;
#if CLIENT
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.RoadSync == null || !EditorUser.User.RoadSync.HasAuthority)
            return false;
        sync = EditorUser.User.RoadSync;
#elif SERVER
        if (!DevkitServerModule.IsEditing || RoadSync.ServersideAuthority == null || !RoadSync.ServersideAuthority.HasAuthority)
            return false;
        sync = RoadSync.ServersideAuthority;
#endif
        return true;
    }
    internal static void RemoveRoad(Road road, int roadIndex)
    {
#if CLIENT
        CheckEditRoad();
        Vector3 oldPosition = road.joints.Count > 0 ? road.joints[0].vertex : Vector3.zero;
#endif

        RemoveRoadLocal(road, roadIndex);

        if (DevkitServerModule.IsEditing)
        {
            if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId roadNetId))
            {
                Logger.DevkitServer.LogWarning(nameof(RemoveRoad), $"Failed to find NetId for road {roadIndex.Format()}. Did not replicate in RemoveRoad({roadIndex.Format()}).");
                return;
            }

#if CLIENT
            ClientEvents.InvokeOnDeleteRoad(new DeleteRoadProperties(roadNetId, oldPosition, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new DeleteRoadAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = roadNetId.id
            });
#endif
        }
    }

#if CLIENT
    internal static void CheckEditMaterial()
    {
        if (DevkitServerModule.IsEditing && !VanillaPermissions.EditRoadMaterials.Has())
            throw new NoPermissionsException(VanillaPermissions.EditRoadMaterials);
    }
    internal static void CheckEditRoad()
    {
        if (DevkitServerModule.IsEditing && !VanillaPermissions.EditRoads.Has())
            throw new NoPermissionsException(VanillaPermissions.EditRoads);
    }
#endif
    internal static void InitializeRoad(Transform transform, out Road road,
#if SERVER
        out
#endif
        NetId roadNetId,
#if SERVER
        out
#endif
        NetId vertexNetId)
    {
#if SERVER
        roadNetId = NetId.INVALID;
        vertexNetId = NetId.INVALID;
#endif
        road = LevelRoads.getRoad(transform, out _, out _);
        int roadIndex = road?.GetRoadIndex() ?? -1;
        if (roadIndex != -1)
        {
#if SERVER
            roadNetId = RoadNetIdDatabase.AddRoad(roadIndex);
            vertexNetId = RoadNetIdDatabase.AddVertex(roadIndex, 0);
#else
            RoadNetIdDatabase.RegisterRoad(road!, roadNetId);
            if (road!.joints.Count > 0)
                RoadNetIdDatabase.RegisterVertex(roadIndex, 0, vertexNetId);
#endif
            Logger.DevkitServer.LogDebug(nameof(InitializeRoad), $"Assigned road NetId: {roadNetId.Format()}.");
            Logger.DevkitServer.LogDebug(nameof(InitializeRoad), $" + Assigned vertex {0.Format()} NetId: {vertexNetId.Format()}.");
            return;
        }

        Logger.DevkitServer.LogWarning(nameof(InitializeRoad), $"Did not find road of transform {transform.name.Format()}.");
    }
    internal static void InitializeVertex(Road road, int roadIndex, int vertexIndex,
#if SERVER
        out
#endif
        NetId netId)
    {
#if SERVER
        netId = NetId.INVALID;
#endif
        if (road.joints.Count >= vertexIndex || vertexIndex < 0)
        {
#if SERVER
            netId = RoadNetIdDatabase.AddVertex(roadIndex, vertexIndex);
#else
            RoadNetIdDatabase.RegisterVertex(roadIndex, vertexIndex, netId);
#endif
            Logger.DevkitServer.LogDebug(nameof(InitializeVertex), $"Assigned vertex {roadIndex.Format()}/{vertexIndex.Format()} NetId: {netId.Format()}.");
            return;
        }

        Logger.DevkitServer.LogWarning(nameof(InitializeVertex), $"Did not find vertex in road # {roadIndex.Format()}: # {vertexIndex.Format()}.");
    }
}