using DevkitServer.API.Permissions;

namespace DevkitServer.Core.Permissions;
public static class DevkitServerGlobalPermissions
{
    public static PermissionLeaf UploadFiles = new PermissionLeaf("files.upload", devkitServer: true);
}
