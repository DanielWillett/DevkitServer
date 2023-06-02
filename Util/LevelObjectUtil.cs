using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;

namespace DevkitServer.Util;
public static class LevelObjectUtil
{
    public static LevelObject? FindObject(Transform transform)
    {
        ThreadUtil.assertIsGameThread();
        if (transform == null)
            return null;

        bool r = false;
        if (Regions.tryGetCoordinate(transform.position, out byte x, out byte y))
        {
            LevelObject? obj =
                SearchInRegion(transform, x, y) ??
                SearchInRegion(transform, x + 1, y) ??
                SearchInRegion(transform, x - 1, y) ??
                SearchInRegion(transform, x, y + 1) ??
                SearchInRegion(transform, x + 1, y + 1) ??
                SearchInRegion(transform, x + 1, y - 1) ??
                SearchInRegion(transform, x - 1, y - 1) ??
                SearchInRegion(transform, x - 1, y + 1);

            if (obj != null)
                return obj;

            r = true;
        }

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                    continue;
                List<LevelObject> region = LevelObjects.objects[x2, y2];
                int c = Math.Min(region.Count, ushort.MaxValue);
                for (int i = 0; i < c; ++i)
                {
                    if (ReferenceEquals(region[i].transform, transform))
                        return region[i];
                }
            }
        }

        return null;
    }
    public static bool TryFindObject(Transform transform, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();
        if (transform != null)
        {
            bool r = false;
            if (Regions.tryGetCoordinate(transform.position, out x, out y))
            {
                if (SearchInRegion(transform, x, y, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x + 1, y, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x - 1, y, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x, y + 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x + 1, y + 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x + 1, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x - 1, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x - 1, y + 1, ref x, ref y, out index))
                    return true;

                r = true;
            }

            for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
            {
                for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
                {
                    if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                        continue;
                    List<LevelObject> region = LevelObjects.objects[x2, y2];
                    int c = Math.Min(region.Count, ushort.MaxValue);
                    for (int i = 0; i < c; ++i)
                    {
                        if (ReferenceEquals(region[i].transform, transform))
                        {
                            x = (byte)x2;
                            y = (byte)y2;
                            index = (ushort)i;
                            return true;
                        }
                    }
                }
            }
        }

        x = byte.MaxValue;
        y = byte.MaxValue;
        index = ushort.MaxValue;
        return false;
    }
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

    public static bool TryFindObjectCoordinates(Vector3 expectedPosition, uint instanceId, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();

        bool r = false;
        if (Regions.tryGetCoordinate(expectedPosition, out x, out y))
        {
            if (SearchInRegion(x, y, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x + 1, y, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x, y - 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x - 1, y, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x, y + 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x + 1, y + 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x + 1, y - 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x - 1, y - 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x - 1, y + 1, instanceId, ref x, ref y, out index))
                return true;

            r = true;
        }

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                    continue;
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
    public static bool SearchInRegion(int regionX, int regionY, uint instanceId, ref byte x, ref byte y, out ushort index)
    {
        if (regionX is < 0 or > byte.MaxValue || regionY is < 0 or > byte.MaxValue)
        {
            index = ushort.MaxValue;
            return false;
        }
        if (SearchInRegion((byte)regionX, (byte)regionY, instanceId, out index))
        {
            x = (byte)regionX;
            y = (byte)regionY;
            return true;
        }

        return false;
    }
    public static LevelObject? SearchInRegion(Transform transform, int regionX, int regionY)
    {
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
                return region[i];
        }

        return null;
    }
    public static bool SearchInRegion(Transform transform, int regionX, int regionY, ref byte x, ref byte y, out ushort index)
    {
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
            {
                x = (byte)regionX;
                y = (byte)regionY;
                index = (ushort)i;
                return true;
            }
        }

        index = ushort.MaxValue;
        return false;
    }
    public static bool SearchInRegion(byte regionX, byte regionY, uint instanceId, out ushort index)
    {
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        int c = Math.Min(region.Count, ushort.MaxValue - 1);
        for (int i = 0; i < c; ++i)
        {
            if (region[i].instanceID == instanceId)
            {
                index = (ushort)i;
                return true;
            }
        }

        index = ushort.MaxValue;
        return false;
    }
#if SERVER
    public static bool CheckMovePermission(uint instanceId, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.MoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               LevelObjectResponsibilities.IsPlacer(instanceId, user);
    }
    public static bool CheckPlacePermission(ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.PlaceObjects.Has(user, false);
    }
    public static bool CheckDeletePermission(uint instanceId, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.RemoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) && HierarchyResponsibilities.IsPlacer(instanceId, user);
    }
#elif CLIENT
    public static bool CheckMovePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.MoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               LevelObjectResponsibilities.IsPlacer(instanceId);
    }
    public static bool CheckPlacePermission()
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.PlaceObjects.Has(false);
    }
    public static bool CheckDeletePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.RemoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) && HierarchyResponsibilities.IsPlacer(instanceId);
    }
#endif
}
