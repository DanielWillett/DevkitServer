using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Util;
public static class LevelObjectUtil
{
    public static LevelObject? FindObject(uint instanceId)
    {
        if (TryFindObjectCoordinates(instanceId, out byte x, out byte y, out ushort index))
            return LevelObjects.objects[x, y][index];

        return null;
    }
    public static LevelObject? FindObject(Vector3 pos, uint instanceId)
    {
        if (TryFindObjectCoordinates(pos, instanceId, out byte x, out byte y, out ushort index))
            return LevelObjects.objects[x, y][index];

        return null;
    }

    public static bool TryFindObjectCoordinates(uint instanceId, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                List<LevelObject> region = LevelObjects.objects[x2, y2];
                int c = Math.Min(region.Count, ushort.MaxValue);
                for (int i = 0; i < c; ++i)
                {
                    if (region[i].instanceID == instanceId)
                    {
                        x = (byte)x2;
                        y = (byte)y2;
                        index = (ushort)i;
                        return true;
                    }
                }
            }
        }

        x = byte.MaxValue;
        y = byte.MaxValue;
        index = ushort.MaxValue;
        return false;
    }

    public static bool TryFindObjectCoordinates(Vector3 pos, uint instanceId, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();

        if (Regions.tryGetCoordinate(pos, out x, out y))
        {
            List<LevelObject> region = LevelObjects.objects[x, y];
            int c = Math.Min(region.Count, ushort.MaxValue);
            for (int i = 0; i < c; ++i)
            {
                if (region[i].instanceID == instanceId)
                {
                    index = (ushort)i;
                    return true;
                }
            }
        }

        return TryFindObjectCoordinates(instanceId, out x, out y, out index);
    }
#if SERVER
    public static bool CheckMovePermission(ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return VanillaPermissions.EditNodes.Has(user, false) ||
               VanillaPermissions.MoveSavedNodes.Has(user, false) ||
               VanillaPermissions.PlaceNodes.Has(user, false) &&
               LevelObjectResponsibilities.IsPlacer(item.instanceID, user);
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
