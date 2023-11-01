using DevkitServer.API;

#if CLIENT
namespace DevkitServer.Multiplayer.Actions;

[EarlyTypeInit]
public static class ClientEvents
{
    internal static CachedMulticastEvent<EditHeightmapRequest> EventOnEditHeightmapPermissionDenied = new CachedMulticastEvent<EditHeightmapRequest>(typeof(ClientEvents), nameof(OnEditHeightmapPermissionDenied), false);
    internal static CachedMulticastEvent<EditSplatmapRequest> EventOnEditSplatmapPermissionDenied = new CachedMulticastEvent<EditSplatmapRequest>(typeof(ClientEvents), nameof(EventOnEditSplatmapPermissionDenied), false);
    internal static CachedMulticastEvent<EditHolesRequest> EventOnEditHolesPermissionDenied = new CachedMulticastEvent<EditHolesRequest>(typeof(ClientEvents), nameof(OnEditHolesPermissionDenied), false);

    internal static CachedMulticastEvent<EditHeightmapRequest> EventOnEditHeightmapRequested = new CachedMulticastEvent<EditHeightmapRequest>(typeof(ClientEvents), nameof(OnEditHeightmapRequested));
    internal static CachedMulticastEvent<EditSplatmapRequest> EventOnEditSplatmapRequested = new CachedMulticastEvent<EditSplatmapRequest>(typeof(ClientEvents), nameof(OnEditSplatmapRequested));
    internal static CachedMulticastEvent<EditHolesRequest> EventOnEditHolesRequested = new CachedMulticastEvent<EditHolesRequest>(typeof(ClientEvents), nameof(OnEditHolesRequested));
    
    internal static CachedMulticastEvent<TryInstantiateHierarchyObject> EventOnTryInstantiateHierarchyObject = new CachedMulticastEvent<TryInstantiateHierarchyObject>(typeof(ClientEvents), nameof(OnTryInstantiateHierarchyObject));
    internal static CachedMulticastEvent<RequestInstantiateHierarchyObject> EventOnRequestInstantiateHierarchyObject = new CachedMulticastEvent<RequestInstantiateHierarchyObject>(typeof(ClientEvents), nameof(OnRequestInstantiateHierarchyObject));
    internal static CachedMulticastEvent<RequestInstantiateRoad> EventOnRequestInstantiateRoad = new CachedMulticastEvent<RequestInstantiateRoad>(typeof(ClientEvents), nameof(EventOnRequestInstantiateRoad));
    internal static CachedMulticastEvent<RequestInstantiateRoadVertex> EventOnRequestInstantiateRoadVertex = new CachedMulticastEvent<RequestInstantiateRoadVertex>(typeof(ClientEvents), nameof(EventOnRequestInstantiateRoadVertex));

    public static event EditHeightmapRequest OnEditHeightmapPermissionDenied
    {
        add => EventOnEditHeightmapPermissionDenied.Add(value);
        remove => EventOnEditHeightmapPermissionDenied.Remove(value);
    }
    public static event EditSplatmapRequest OnEditSplatmapPermissionDenied
    {
        add => EventOnEditSplatmapPermissionDenied.Add(value);
        remove => EventOnEditSplatmapPermissionDenied.Remove(value);
    }
    public static event EditHolesRequest OnEditHolesPermissionDenied
    {
        add => EventOnEditHolesPermissionDenied.Add(value);
        remove => EventOnEditHolesPermissionDenied.Remove(value);
    }

    public static event EditHeightmapRequest OnEditHeightmapRequested
    {
        add => EventOnEditHeightmapRequested.Add(value);
        remove => EventOnEditHeightmapRequested.Remove(value);
    }
    public static event EditSplatmapRequest OnEditSplatmapRequested
    {
        add => EventOnEditSplatmapRequested.Add(value);
        remove => EventOnEditSplatmapRequested.Remove(value);
    }
    public static event EditHolesRequest OnEditHolesRequested
    {
        add => EventOnEditHolesRequested.Add(value);
        remove => EventOnEditHolesRequested.Remove(value);
    }

    public static event TryInstantiateHierarchyObject OnTryInstantiateHierarchyObject
    {
        add => EventOnTryInstantiateHierarchyObject.Add(value);
        remove => EventOnTryInstantiateHierarchyObject.Remove(value);
    }
    public static event RequestInstantiateHierarchyObject OnRequestInstantiateHierarchyObject
    {
        add => EventOnRequestInstantiateHierarchyObject.Add(value);
        remove => EventOnRequestInstantiateHierarchyObject.Remove(value);
    }
    public static event RequestInstantiateRoad OnRequestInstantiateRoad
    {
        add => EventOnRequestInstantiateRoad.Add(value);
        remove => EventOnRequestInstantiateRoad.Remove(value);
    }
    public static event RequestInstantiateRoadVertex OnRequestInstantiateRoadVertex
    {
        add => EventOnRequestInstantiateRoadVertex.Add(value);
        remove => EventOnRequestInstantiateRoadVertex.Remove(value);
    }

    public static event PaintRamp? OnPaintRamp;
    public static event AdjustHeightmap? OnAdjustHeightmap;
    public static event FlattenHeightmap? OnFlattenHeightmap;
    public static event SmoothHeightmap? OnSmoothHeightmap;
    public static event PaintSplatmap? OnPaintSplatmap;
    public static event PaintSplatmap? OnAutoPaintSplatmap;
    public static event SmoothSplatmap? OnSmoothSplatmap;
    public static event PaintHoles? OnPaintHoles;
    public static event UpdateLandscapeTile? OnAddTile;
    public static event UpdateLandscapeTile? OnDeleteTile;
    public static event AddFoliage? OnAddFoliage;
    public static event RemoveFoliage? OnRemoveFoliage;
    public static event RemoveResourceSpawnpointFoliage? OnRemoveResourceSpawnpointFoliage;
    public static event RemoveLevelObjectFoliage? OnRemoveLevelObjectFoliage;
    public static event UpdateLandscapeTile? OnUpdateTileSplatmapLayers;
    public static event MoveHierarchyObjectFinal? OnMoveHierarchyObjectFinal;
    public static event MoveHierarchyObjectsFinal? OnMoveHierarchyObjectsFinal;
    public static event DeleteHierarchyObject? OnDeleteHierarchyObject;
    public static event DeleteHierarchyObjects? OnDeleteHierarchyObjects;
    public static event DeleteLevelObject? OnDeleteLevelObject;
    public static event DeleteLevelObjects? OnDeleteLevelObjects;
    public static event MoveLevelObjectFinal? OnMoveLevelObjectFinal;
    public static event MoveLevelObjectsFinal? OnMoveLevelObjectsFinal;
    public static event UpdateObjectsCustomMaterialPaletteOverride? OnUpdateObjectsCustomMaterialPaletteOverride;
    public static event UpdateObjectsMaterialIndexOverride? OnUpdateObjectsMaterialIndexOverride;
    public static event MoveRoadVertex? OnMoveRoadVertex;
    public static event MoveRoadTangentHandle? OnMoveRoadTangentHandle;
    public static event DeleteRoadVertex? OnDeleteRoadVertex;
    public static event DeleteRoad? OnDeleteRoad;
    public static event MoveRoadVertexRequested? OnMoveRoadVertexRequested;
    public static event MoveRoadTangentHandleRequested? OnMoveRoadTangentHandleRequested;
    public static event DeleteRoadVertexRequested? OnDeleteRoadVertexRequested;
    public static event DeleteRoadRequested? OnDeleteRoadRequested;
    public static event RequestInstantiateRoadVertexRequested? OnRequestInstantiateRoadVertexRequested;
    public static event RequestInstantiateRoadRequested? OnRequestInstantiateRoadRequested;

    public static bool ListeningOnEditHeightmapPermissionDenied => !EventOnEditHeightmapPermissionDenied.IsEmpty;
    public static bool ListeningOnEditSplatmapPermissionDenied => !EventOnEditSplatmapPermissionDenied.IsEmpty;
    public static bool ListeningOnEditHolesPermissionDenied => !EventOnEditHolesPermissionDenied.IsEmpty;
    
    public static bool ListeningOnEditHeightmapRequested => !EventOnEditHeightmapRequested.IsEmpty;
    public static bool ListeningOnEditSplatmapRequested => !EventOnEditSplatmapRequested.IsEmpty;
    public static bool ListeningOnEditHolesRequested => !EventOnEditHolesRequested.IsEmpty;

    public static bool ListeningOnTryInstantiateHierarchyObject => !EventOnTryInstantiateHierarchyObject.IsEmpty;
    public static bool ListeningOnRequestInstantiateHierarchyObject => !EventOnRequestInstantiateHierarchyObject.IsEmpty;

    public static bool ListeningOnPaintRamp => OnPaintRamp != null;
    public static bool ListeningOnAdjustHeightmap => OnAdjustHeightmap != null;
    public static bool ListeningOnFlattenHeightmap => OnFlattenHeightmap != null;
    public static bool ListeningOnSmoothHeightmap => OnSmoothHeightmap != null;
    public static bool ListeningOnPaintSplatmap => OnPaintSplatmap != null;
    public static bool ListeningOnAutoPaintSplatmap => OnAutoPaintSplatmap != null;
    public static bool ListeningOnSmoothSplatmap => OnSmoothSplatmap != null;
    public static bool ListeningOnPaintHoles => OnPaintHoles != null;
    public static bool ListeningOnAddTile => OnAddTile != null;
    public static bool ListeningOnDeleteTile => OnDeleteTile != null;
    public static bool ListeningOnAddFoliage => OnAddFoliage != null;
    public static bool ListeningOnRemoveFoliage => OnRemoveFoliage != null;
    public static bool ListeningOnRemoveResourceSpawnpointFoliage => OnRemoveResourceSpawnpointFoliage != null;
    public static bool ListeningOnRemoveLevelObjectFoliage => OnRemoveLevelObjectFoliage != null;
    public static bool ListeningOnUpdateTileSplatmapLayers => OnUpdateTileSplatmapLayers != null;
    public static bool ListeningOnMoveHierarchyObjectFinal => OnMoveHierarchyObjectFinal != null;
    public static bool ListeningOnMoveHierarchyObjectsFinal => OnMoveHierarchyObjectsFinal != null;
    public static bool ListeningOnDeleteHierarchyObject => OnDeleteHierarchyObject != null;
    public static bool ListeningOnDeleteHierarchyObjects => OnDeleteHierarchyObjects != null;
    public static bool ListeningOnDeleteLevelObject => OnDeleteLevelObject != null;
    public static bool ListeningOnDeleteLevelObjects => OnDeleteLevelObjects != null;
    public static bool ListeningOnMoveLevelObjectFinal => OnMoveLevelObjectFinal != null;
    public static bool ListeningOnMoveLevelObjectsFinal => OnMoveLevelObjectsFinal != null;
    public static bool ListeningOnUpdateObjectsCustomMaterialPaletteOverride => OnUpdateObjectsCustomMaterialPaletteOverride != null;
    public static bool ListeningOnUpdateObjectsMaterialIndexOverride => OnUpdateObjectsMaterialIndexOverride != null;
    public static bool ListeningOnMoveRoadVertex => OnMoveRoadVertex != null;
    public static bool ListeningOnMoveRoadTangentHandle => OnMoveRoadTangentHandle != null;
    public static bool ListeningOnDeleteRoadVertex => OnDeleteRoadVertex != null;
    public static bool ListeningOnDeleteRoad => OnDeleteRoad != null;
    public static bool ListeningOnMoveRoadVertexRequested => OnMoveRoadVertexRequested != null;
    public static bool ListeningOnMoveRoadTangentHandleRequested => OnMoveRoadTangentHandleRequested != null;
    public static bool ListeningOnDeleteRoadVertexRequested => OnDeleteRoadVertexRequested != null;
    public static bool ListeningOnDeleteRoadRequested => OnDeleteRoadRequested != null;
    public static bool ListeningOnRequestInstantiateRoadRequested => OnRequestInstantiateRoadRequested != null;
    public static bool ListeningOnRequestInstantiateRoadVertexRequested => OnRequestInstantiateRoadVertexRequested != null;
    
    internal static void InvokeOnPaintRamp(in PaintRampProperties properties) => OnPaintRamp?.Invoke(in properties);
    internal static void InvokeOnAdjustHeightmap(in AdjustHeightmapProperties properties) => OnAdjustHeightmap?.Invoke(in properties);
    internal static void InvokeOnFlattenHeightmap(in FlattenHeightmapProperties properties) => OnFlattenHeightmap?.Invoke(in properties);
    internal static void InvokeOnSmoothHeightmap(in SmoothHeightmapProperties properties) => OnSmoothHeightmap?.Invoke(in properties);
    internal static void InvokeOnPaintSplatmap(in PaintSplatmapProperties properties) => OnPaintSplatmap?.Invoke(in properties);
    internal static void InvokeOnAutoPaintSplatmap(in PaintSplatmapProperties properties) => OnAutoPaintSplatmap?.Invoke(in properties);
    internal static void InvokeOnSmoothSplatmap(in SmoothSplatmapProperties properties) => OnSmoothSplatmap?.Invoke(in properties);
    internal static void InvokeOnPaintHoles(in PaintHolesProperties properties) => OnPaintHoles?.Invoke(in properties);
    internal static void InvokeOnAddTile(in UpdateLandscapeTileProperties properties) => OnAddTile?.Invoke(in properties);
    internal static void InvokeOnDeleteTile(in UpdateLandscapeTileProperties properties) => OnDeleteTile?.Invoke(in properties);
    internal static void InvokeOnAddFoliage(in AddFoliageProperties properties) => OnAddFoliage?.Invoke(in properties);
    internal static void InvokeOnRemoveFoliage(in RemoveFoliageProperties properties) => OnRemoveFoliage?.Invoke(in properties);
    internal static void InvokeOnRemoveResourceSpawnpointFoliage(in RemoveResourceSpawnpointFoliageProperties properties) => OnRemoveResourceSpawnpointFoliage?.Invoke(in properties);
    internal static void InvokeOnRemoveLevelObjectFoliage(in RemoveLevelObjectFoliageProperties properties) => OnRemoveLevelObjectFoliage?.Invoke(in properties);
    internal static void InvokeOnUpdateTileSplatmapLayers(in UpdateLandscapeTileProperties properties) => OnUpdateTileSplatmapLayers?.Invoke(in properties);
    internal static void InvokeOnMoveHierarchyObjectFinal(in MoveHierarchyObjectFinalProperties properties) => OnMoveHierarchyObjectFinal?.Invoke(in properties);
    internal static void InvokeOnMoveHierarchyObjectsFinal(in MoveHierarchyObjectsFinalProperties properties) => OnMoveHierarchyObjectsFinal?.Invoke(in properties);
    internal static void InvokeOnDeleteHierarchyObject(in DeleteHierarchyObjectProperties properties) => OnDeleteHierarchyObject?.Invoke(in properties);
    internal static void InvokeOnDeleteHierarchyObjects(in DeleteHierarchyObjectsProperties properties) => OnDeleteHierarchyObjects?.Invoke(in properties);
    internal static void InvokeOnDeleteLevelObject(in DeleteLevelObjectProperties properties) => OnDeleteLevelObject?.Invoke(in properties);
    internal static void InvokeOnDeleteLevelObjects(in DeleteLevelObjectsProperties properties) => OnDeleteLevelObjects?.Invoke(in properties);
    internal static void InvokeOnMoveLevelObjectFinal(in MoveLevelObjectFinalProperties properties) => OnMoveLevelObjectFinal?.Invoke(in properties);
    internal static void InvokeOnMoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties) => OnMoveLevelObjectsFinal?.Invoke(in properties);
    internal static void InvokeOnUpdateObjectsCustomMaterialPaletteOverride(in UpdateObjectsCustomMaterialPaletteOverrideProperties properties) => OnUpdateObjectsCustomMaterialPaletteOverride?.Invoke(in properties);
    internal static void InvokeOnUpdateObjectsMaterialIndexOverride(in UpdateObjectsMaterialIndexOverrideProperties properties) => OnUpdateObjectsMaterialIndexOverride?.Invoke(in properties);
    internal static void InvokeOnMoveRoadVertex(in MoveRoadVertexProperties properties) => OnMoveRoadVertex?.Invoke(in properties);
    internal static void InvokeOnMoveRoadTangentHandle(in MoveRoadTangentHandleProperties properties) => OnMoveRoadTangentHandle?.Invoke(in properties);
    internal static void InvokeOnDeleteRoadVertex(in DeleteRoadVertexProperties properties) => OnDeleteRoadVertex?.Invoke(in properties);
    internal static void InvokeOnDeleteRoad(in DeleteRoadProperties properties) => OnDeleteRoad?.Invoke(in properties);
    internal static void InvokeOnMoveRoadVertexRequested(in MoveRoadVertexProperties properties, ref bool shouldAllow) => OnMoveRoadVertexRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnMoveRoadTangentHandleRequested(in MoveRoadTangentHandleProperties properties, ref bool shouldAllow) => OnMoveRoadTangentHandleRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteRoadVertexRequested(in DeleteRoadVertexProperties properties, ref bool shouldAllow) => OnDeleteRoadVertexRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteRoadRequested(in DeleteRoadProperties properties, ref bool shouldAllow) => OnDeleteRoadRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateRoadRequested(in RequestInstantiateRoadProperties properties, ref bool shouldAllow) => OnRequestInstantiateRoadRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateRoadVertexRequested(in RequestInstantiateRoadVertexProperties properties, ref bool shouldAllow) => OnRequestInstantiateRoadVertexRequested?.Invoke(in properties, ref shouldAllow);
}

public delegate void TryInstantiateHierarchyObject(ref InstantiateHierarchyObjectProperties properties, ref bool shouldAllow);

public delegate void EditHeightmapRequest(TerrainEditor.EDevkitLandscapeToolHeightmapMode mode, ref bool allow);
public delegate void EditSplatmapRequest(TerrainEditor.EDevkitLandscapeToolSplatmapMode mode, ref bool allow);
public delegate void EditHolesRequest(bool isFilling, ref bool allow);
#endif