namespace DevkitServer.API.Permissions;

/// <summary>
/// Thrown when a client-side action requires permissions that aren't granted.
/// </summary>
public class NoPermissionsException : Exception
{
    /// <summary>
    /// Permission that was required to perform the action.
    /// </summary>
    public PermissionLeaf? RequiredPermission { get; }

    public NoPermissionsException(PermissionLeaf? requiredPermission) : base(requiredPermission != null ? "Missing permission: \"" + requiredPermission + "\"" : "Missing permissions")
    {
        RequiredPermission = requiredPermission;
    }

    public NoPermissionsException(PermissionLeaf? requiredPermission, Exception inner) : base(requiredPermission != null ? "Missing permission: \"" + requiredPermission + "\"" : "Missing permissions", inner)
    {
        RequiredPermission = requiredPermission;
    }
}