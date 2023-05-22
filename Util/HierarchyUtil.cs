using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;

namespace DevkitServer.Util;
public static class HierarchyUtil
{
    public static IDevkitHierarchyItem? FindItem(uint instanceId)
    {
        int ind = FindItemIndex(instanceId);
        return ind < 0 ? null : LevelHierarchy.instance.items[ind];
    }
    /// <returns>Inverse of binary search if not found, will always be &lt; 0 when not found.</returns>
    public static int FindItemIndex(uint instanceId)
    {
        ThreadUtil.assertIsGameThread();

        List<IDevkitHierarchyItem> items = LevelHierarchy.instance.items;
        int min = 0;
        int max = items.Count - 1;

        // binary search because it should be mostly in order.
        while (min <= max)
        {
            int index = min + (max - min >> 1);
            int comparison = items[index].instanceID.CompareTo(instanceId);
            if (comparison == 0)
                return index;
            if (comparison < 0)
                min = index + 1;
            else
                max = index - 1;
        }

        // then slow loop
        for (int i = 0; i < items.Count; ++i)
            if (instanceId == items[i].instanceID)
                return i;
        return ~min;
    }
#if SERVER
    public static bool CheckMovePermission(IDevkitHierarchyItem item, ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(user, false) ||
                            VanillaPermissions.MoveSavedNodes.Has(user, false) ||
                            VanillaPermissions.PlaceNodes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.MoveSavedCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            VolumeBase =>   VanillaPermissions.EditVolumes.Has(user, false) ||
                            VanillaPermissions.MoveSavedVolumes.Has(user, false) ||
                            VanillaPermissions.PlaceVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            _ => false
        };
    }
    public static bool CheckPlacePermission(IHierarchyItemTypeIdentifier type, ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return type switch
        {
            NodeItemTypeIdentifier => VanillaPermissions.EditNodes.Has(user, false) ||
                                      VanillaPermissions.PlaceNodes.Has(user, false),

            VolumeItemTypeIdentifier v => typeof(CartographyVolume).IsAssignableFrom(v.Type)
                ? (VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                   VanillaPermissions.PlaceCartographyVolumes.Has(user, false))
                : (VanillaPermissions.EditVolumes.Has(user, false) ||
                   VanillaPermissions.PlaceVolumes.Has(user, false)),

            _ => false
        };
    }
    public static bool CheckDeletePermission(IDevkitHierarchyItem item, ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(user, false) ||
                            VanillaPermissions.RemoveSavedNodes.Has(user, false) ||
                            VanillaPermissions.PlaceNodes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.RemoveSavedCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            VolumeBase => VanillaPermissions.EditVolumes.Has(user, false) ||
                          VanillaPermissions.RemoveSavedVolumes.Has(user, false) ||
                          VanillaPermissions.PlaceVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            _ => false
        };
    }
#elif CLIENT
    public static bool CheckMovePermission(IDevkitHierarchyItem item)
    {
        if (Permission.SuperuserPermission.Has())
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(false) ||
                            VanillaPermissions.MoveSavedNodes.Has(false) ||
                            VanillaPermissions.PlaceNodes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(false) ||
                                 VanillaPermissions.MoveSavedCartographyVolumes.Has(false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            VolumeBase => VanillaPermissions.EditVolumes.Has(false) ||
                            VanillaPermissions.MoveSavedVolumes.Has(false) ||
                            VanillaPermissions.PlaceVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            _ => false
        };
    }
    public static bool CheckPlacePermission(IHierarchyItemTypeIdentifier type)
    {
        if (Permission.SuperuserPermission.Has())
            return true;

        return type switch
        {
            NodeItemTypeIdentifier => VanillaPermissions.EditNodes.Has(false) ||
                                      VanillaPermissions.PlaceNodes.Has(false),

            VolumeItemTypeIdentifier v => typeof(CartographyVolume).IsAssignableFrom(v.Type)
                ? (VanillaPermissions.EditCartographyVolumes.Has(false) ||
                   VanillaPermissions.PlaceCartographyVolumes.Has(false))
                : (VanillaPermissions.EditVolumes.Has(false) ||
                   VanillaPermissions.PlaceVolumes.Has(false)),

            _ => false
        };
    }
    public static bool CheckDeletePermission(IDevkitHierarchyItem item)
    {
        if (Permission.SuperuserPermission.Has())
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(false) ||
                            VanillaPermissions.RemoveSavedNodes.Has(false) ||
                            VanillaPermissions.PlaceNodes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(false) ||
                                 VanillaPermissions.RemoveSavedCartographyVolumes.Has(false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            VolumeBase => VanillaPermissions.EditVolumes.Has(false) ||
                          VanillaPermissions.RemoveSavedVolumes.Has(false) ||
                          VanillaPermissions.PlaceVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            _ => false
        };
    }
#endif
}

