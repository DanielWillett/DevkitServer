#if SERVER
using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using System.Globalization;
using System.Text;

namespace DevkitServer.Core.Commands;
internal sealed class PermissionsCommand : DevkitServerCommand, ICommandLocalizationFile
{
    private readonly string[] _userMatches = { "user", "users", "player", "players", "u", "p" };
    private readonly string[] _permMatches = { "perm", "permission", "perms", "permissions", "p" };
    [Permission]
    public static readonly PermissionLeaf All = new PermissionLeaf("permissions.*", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf SeePermissions = new PermissionLeaf("permissions", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf AllGroup = new PermissionLeaf("permissions.group.*", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf EditGroup = new PermissionLeaf("permissions.group.edit.*", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf EditGroupPermissions = new PermissionLeaf("permissions.group.edit.permissions.*", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf EditGroupAddPermission = new PermissionLeaf("permissions.group.edit.permissions.add", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf EditGroupRemovePermission = new PermissionLeaf("permissions.group.edit.permissions.remove", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf EditGroupInfo = new PermissionLeaf("permissions.group.edit.info", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf CreateGroup = new PermissionLeaf("permissions.group.create", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf DeleteGroup = new PermissionLeaf("permissions.group.delete", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf GrantGroup = new PermissionLeaf("permissions.user.group.grant", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf RevokeGroup = new PermissionLeaf("permissions.user.group.revoke", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf GrantPermission = new PermissionLeaf("permissions.user.permission.grant", devkitServer: true);
    [Permission]
    public static readonly PermissionLeaf RevokePermission = new PermissionLeaf("permissions.user.permission.revoke", devkitServer: true);

    Local ILocalizedCommand.Translations { get; set; } = null!;
    public PermissionsCommand() : base("permissions")
    {
        AddAlias("p");
        AddAlias("perm");
        AddAlias("perms");
        AddAlias("permission");
    }

    public override UniTask Execute(CommandContext ctx, CancellationToken token)
    {
        ctx.AssertHelpCheckFormat(0, "CorrectUsage");

        if (ctx.HasArgsExact(0))
        {
            ctx.AssertRanByPlayer();

            int c = 0;
            StringBuilder sb = new StringBuilder();
            bool init = false;
            foreach (string perm in UserPermissions.UserHandler
                         .GetPermissions(ctx.Caller.playerID.steamID.m_SteamID, false)
                         .Select(x => x.ToString())
                         .Concat(UserPermissions.UserHandler.GetPermissionGroups(ctx.Caller.playerID.steamID.m_SteamID)
                             .OrderByDescending(x => x.Priority)
                             .SelectMany(x => x.Permissions)
                             .Select(x => x.ToString())))
            {
                if (!init)
                {
                    init = true;
                    ctx.Reply("PermissionListIntro");
                }

                if (c != 0)
                    sb.Append(", ");
                ++c;
                sb.Append(perm);
                if (c > 10)
                {
                    c = 0;
                    ctx.Reply("PermissionListValues", sb.ToString());
                    sb.Clear();
                }
            }
            if (c > 0)
                ctx.Reply("PermissionListValues", sb.ToString());
            else if (!init)
                ctx.Reply("NoPermissions");

            return UniTask.CompletedTask;
        }

        bool pAll = ctx.HasPermission(All);

        if (ctx.MatchParameter(0, "group", "grp", "g"))
        {
            ctx.AssertHelpCheckFormat(1, "CorrectUsageGroup");

            pAll |= ctx.HasPermission(AllGroup);

            if (ctx.MatchParameter(1, "edit", "set", "e"))
            {
                ctx.AssertHelpCheckFormat(2, "CorrectUsageGroupEdit");

                pAll |= ctx.HasPermission(EditGroup);

                if (ctx.MatchParameter(2, "name", "display", "n"))
                {
                    if (pAll || ctx.HasPermission(EditGroupInfo))
                    {
                        if (ctx.TryGet(3, out string id) && ctx.TryGet(4, out string? name))
                        {
                            if (string.IsNullOrWhiteSpace(name) ||
                                name.Equals("null", StringComparison.InvariantCultureIgnoreCase))
                                name = null;
                            if (UserPermissions.Handler.TryFindPermissionGroup(id, out PermissionGroup group))
                            {
                                group.DisplayName = name ?? id;
                                UserPermissions.Handler.SavePermissionGroup(group, false);
                                throw ctx.Reply("ChangedName", group.Id, group.DisplayName, "#" + ColorUtility.ToHtmlStringRGB(group.Color));
                            }
                            
                            FailToFindGroup(id);
                        }
                        else throw ctx.Reply("CorrectUsageGroupEdit");
                    }
                    else throw ctx.SendNoPermission();
                }
                else if (ctx.MatchParameter(2, "color", "clr", "c"))
                {
                    if (pAll || ctx.HasPermission(EditGroupInfo))
                    {
                        if (ctx.TryGet(3, out string id))
                        {
                            if (!ctx.TryGet(4, out Color32 color))
                                throw ctx.Reply("FailedToParseColor");
                            if (UserPermissions.Handler.TryFindPermissionGroup(id, out PermissionGroup group))
                            {
                                Color oldColor = group.Color;
                                group.Color = color;
                                UserPermissions.Handler.SavePermissionGroup(group, false);
                                throw ctx.Reply("ChangedColor", group.Id, "#" + ColorUtility.ToHtmlStringRGB(group.Color), "#" + ColorUtility.ToHtmlStringRGB(oldColor));
                            }
                            
                            FailToFindGroup(id);
                        }
                        else throw ctx.Reply("CorrectUsageGroupEdit");
                    }
                    else throw ctx.SendNoPermission();
                }
                else if (ctx.MatchParameter(2, "priority", "p"))
                {
                    if (pAll || ctx.HasPermission(EditGroupInfo))
                    {
                        if (ctx.TryGet(3, out string id))
                        {
                            if (!ctx.TryGet(4, out int priority))
                                throw ctx.Reply("FailedToParsePriority");
                            if (UserPermissions.Handler.TryFindPermissionGroup(id, out PermissionGroup group))
                            {
                                group.Priority = priority;
                                UserPermissions.Handler.SavePermissionGroup(group, true);
                                throw ctx.Reply("ChangedPriority", group.Id, group.Priority.ToString(CultureInfo.InvariantCulture), "#" + ColorUtility.ToHtmlStringRGB(group.Color));
                            }
                            
                            FailToFindGroup(id);
                        }
                        else throw ctx.Reply("CorrectUsageGroupEdit");
                    }
                    else throw ctx.SendNoPermission();
                }
                else throw ctx.Reply("CorrectUsageGroupEdit");
            }
            else
            {
                bool remove = ctx.MatchParameter(1, "remove", "delete", "r");
                if (remove || ctx.MatchParameter(1, "add", "a"))
                {
                    ctx.AssertHelpCheckFormat(2, "CorrectUsageGroupEditPermissions");
                    ctx.AssertHelpCheckFormat(3, "CorrectUsageGroupEditPermissions");

                    pAll |= ctx.HasPermission(EditGroupPermissions);
                    if (pAll || !remove && ctx.HasPermission(EditGroupAddPermission) || remove && ctx.HasPermission(EditGroupRemovePermission))
                    {
                        if (ctx.TryGet(2, out string id) && ctx.TryGet(3, out string permStr))
                        {
                            if (!GroupPermission.TryParse(permStr, out GroupPermission perm))
                                throw ctx.Reply("PermissionNotFound", permStr);
                            if (UserPermissions.Handler.TryFindPermissionGroup(id, out PermissionGroup group))
                            {
                                if (remove)
                                    ctx.Reply(group.RemovePermission(perm) ? "RemovedPermission" : "PermissionAlreadyRemoved", group.Id, perm.ToString(), "#" + ColorUtility.ToHtmlStringRGB(group.Color));
                                else
                                    ctx.Reply(group.AddPermission(perm) ? "AddedPermission" : "PermissionAlreadyAdded", group.Id, perm.ToString(), "#" + ColorUtility.ToHtmlStringRGB(group.Color));
                                UserPermissions.Handler.SavePermissionGroup(group, false);
                                return UniTask.CompletedTask;
                            }

                            FailToFindGroup(id);
                        }
                        else throw ctx.Reply("CorrectUsageGroupEditPermissions");
                    }
                    else throw ctx.SendNoPermission();
                }
                else if (ctx.MatchParameter(1, "create", "new", "c"))
                {
                    if (!pAll && !ctx.HasPermission(CreateGroup))
                        throw ctx.SendNoPermission();

                    ctx.AssertHelpCheckFormat(2, "CorrectUsageGroupCreate");
                    ctx.AssertHelpCheckFormat(3, "CorrectUsageGroupCreate");
                    ctx.AssertHelpCheckFormat(4, "CorrectUsageGroupCreate");
                    ctx.AssertHelpCheckFormat(5, "CorrectUsageGroupCreate");

                    if (ctx.TryGet(2, out string id) && ctx.TryGet(3, out string displayName))
                    {
                        if (!ctx.TryGet(4, out Color32 color))
                            color = Color.white;
                        if (!ctx.TryGet(5, out int priority))
                            priority = 0;

                        PermissionGroup group = new PermissionGroup(id, displayName, color, priority, Array.Empty<GroupPermission>());
                        if (UserPermissions.Handler.Register(group))
                            throw ctx.Reply("PermissionGroupCreated", group.Id, group.DisplayName, "#" + ColorUtility.ToHtmlStringRGB(group.Color));

                        throw ctx.Reply("PermissionGroupCreateAlreadyExists", group.Id, "#" + ColorUtility.ToHtmlStringRGB(group.Color));
                    }

                    throw ctx.Reply("CorrectUsageGroupCreate");
                }
                else if (ctx.MatchParameter(1, "delete", "remove", "d"))
                {
                    if (!pAll && !ctx.HasPermission(DeleteGroup))
                        throw ctx.SendNoPermission();

                    ctx.AssertHelpCheckFormat(2, "CorrectUsageGroupDelete");

                    if (ctx.TryGet(2, out string id))
                    {
                        if (UserPermissions.Handler.TryFindPermissionGroup(id, out PermissionGroup group) && UserPermissions.Handler.Deregister(group))
                            throw ctx.Reply("PermissionGroupDeleted", group.Id, "#" + ColorUtility.ToHtmlStringRGB(group.Color));

                        FailToFindGroup(id);
                    }

                    throw ctx.Reply("CorrectUsageGroupCreate");
                }
                else throw ctx.Reply("CorrectUsageGroup");
            }
        }
        else if (ctx.MatchParameter(0, _userMatches))
        {
            bool revoke = ctx.MatchParameter(1, "revoke", "remove", "leave", "r");
            bool clear = ctx.MatchParameter(1, "clear", "c", "clr");
            if (!revoke && !clear && !ctx.MatchParameter(1, "grant", "add", "join", "a"))
                throw ctx.Reply("CorrectUsageUser");

            bool group = ctx.MatchParameter(3, "group", "grp", "g", "groups");
            if (!group && !ctx.MatchParameter(3, _permMatches))
                throw ctx.Reply("CorrectUsageUser");

            if (!ctx.TryGet(2, out ulong user, out SteamPlayer? onlinePlayer, false))
                throw ctx.Reply("PlayerNotValid", ctx.Get(2));
            string name = onlinePlayer != null ? onlinePlayer.playerID.characterName : user.ToString();
            if (ctx.TryGetRange(4, out string id))
            {
                if (group)
                {
                    if (!UserPermissions.Handler.TryFindPermissionGroup(id, out PermissionGroup grp))
                        FailToFindGroup(id); // throws

                    if (revoke)
                        UserPermissions.UserHandler.RemovePermissionGroup(user, grp);
                    else if (!clear)
                        UserPermissions.UserHandler.AddPermissionGroup(user, grp);
                    else
                    {
                        UserPermissions.UserHandler.ClearPermissionGroups(user);
                        throw ctx.Reply("UserClearedPermissionGroups", name);
                    }
                    throw ctx.Reply(revoke ? "UserRevokedPermissionGroup" : "UserAddedPermissionGroup", grp.Id, "#" + ColorUtility.ToHtmlStringRGB(grp.Color), name);
                }

                if (!PermissionLeaf.TryParse(id, out PermissionLeaf permission))
                    throw ctx.Reply("PermissionNotFound", id);

                if (revoke)
                    UserPermissions.UserHandler.RemovePermission(user, permission);
                else if (!clear)
                    UserPermissions.UserHandler.AddPermission(user, permission);
                else
                {
                    UserPermissions.UserHandler.ClearPermissions(user);
                    throw ctx.Reply("UserClearedPermissionGroups", name);
                }
                throw ctx.Reply(revoke ? "UserRevokedPermission" : "UserAddedPermission", permission.ToString(), name);
            }
        }
        else throw ctx.Reply("CorrectUsage");

        void FailToFindGroup(string id)
        {
            ctx.Reply("GroupNotFound", id);
            StringBuilder sb = new StringBuilder();
            foreach (PermissionGroup grp in UserPermissions.Handler.PermissionGroups)
            {
                if (sb.Length != 0)
                    sb.Append(", ");
                sb.Append(grp.Id);
            }

            throw ctx.Reply("GroupList", sb.ToString());
        }
        return UniTask.CompletedTask;
    }

    public string TranslationsDirectory => nameof(PermissionsCommand);
    public LocalDatDictionary DefaultTranslations => new LocalDatDictionary
    {
        { "CorrectUsage", "<#b08598>Correct Usage: /p <group|user> - Edit permissions." },
        { "CorrectUsageGroup", "<#b08598>Correct Usage: /p group <delete|create|edit|add|remove> - Edit permission group data." },
        { "CorrectUsageGroupEdit", "<#b08598>Correct Usage: /p group edit <name|color|priority> <group id> <value... > - Edit permission group." },
        { "CorrectUsageGroupEditPermissions", "<#b08598>Correct Usage: /p group <add|remove> <group id> [-]<permission> - Add or remove permissions." },
        { "CorrectUsageGroupCreate", "<#b08598>Correct Usage: /p group create <id> <name> [color = white] [priority = 0] - Create a new permission group." },
        { "CorrectUsageGroupDelete", "<#b08598>Correct Usage: /p group delete <id> - Delete a permission group." },
        { "CorrectUsageUser", "<#b08598>Correct Usage: /p user <grant|revoke|clear> <user> <group|permission> <id> - Revoke or grant a group or permission." },
        { "GroupNotFound", "<#b08598>Failed to find a permission group with the ID <#ffe4f8>{0}</color>." },
        { "PermissionNotFound", "<#b08598>The permission <#ffe4f8>{0}</color> doesn't exist." },
        { "PermissionAlreadyAdded", "<#b08598>The permission <#ffe4f8>{1}</color> is already a part of <{2}>{0}</color>." },
        { "PermissionAlreadyRemoved", "<#b08598>The permission <#ffe4f8>{1}</color> isn't a part of <{2}>{0}</color>." },
        { "GroupList", "<#ffe4f8>Permission groups: {0}" },
        { "PermissionListIntro", "<#b70075>Permissions:" },
        { "PermissionListValues", "<#ffe4f8>{0}" },
        { "NoPermissions", "<#b70075>No permissions." },
        { "FailedToParseColor", "<#b08598>Unable to parse <#ffe4f8>{0}</color> as a color. Try formatting it like '<#fff>#b08598</color>', or '<#fff>rgb(176,133,152)</color>' instead." },
        { "PlayerNotValid", "<#b08598>Unable to find a user from <#ffe4f8>{0}</color>. If they're offline try using their Steam64 ID instead." },
        { "FailedToParsePriority", "<#b08598>Unable to parse <#ffe4f8>{0}</color> as an integer. Positive or negative whole numbers are allowed." },
        { "ChangedName", "<#72e4dc>Changed display name of <{2}>{0}</color> to '<#fff>{1}</color>'." },
        { "ChangedColor", "<#72e4dc>Changed color of <{2}>{0}</color> to '<{1}>{1}</color>." },
        { "ChangedPriority", "<#72e4dc>Changed priority of <{2}>{0}</color> to <#fff>{1}</color>." },
        { "AddedPermission", "<#72e4dc>Added the <#b08598>{1}</color> permission to <{2}>{0}</color>." },
        { "RemovedPermission", "<#72e4dc>Removed the <#b08598>{1}</color> permission from <{2}>{0}</color>." },
        { "PermissionGroupCreated", "<#72e4dc>Created permission group: <{2}>{0}</color> (<i><{2}>{1}</color></i>)." },
        { "PermissionGroupCreateAlreadyExists", "<#b08598>A permission group with the Id <{1}>{0}</color> already exists." },
        { "PermissionGroupDeleted", "<#72e4dc>Deleted permission group: <{2}>{0}</color>." },
        { "UserRevokedPermissionGroup", "<#72e4dc>Removed the permission group <{1}>{0}</color> from <#ffe4f8>{2}</color>." },
        { "UserAddedPermissionGroup", "<#72e4dc>Added the permission group <{1}>{0}</color> to <#ffe4f8>{2}</color>." },
        { "UserClearedPermissionGroups", "<#72e4dc>Cleared all permission groups from <#ffe4f8>{0}</color>." },
        { "UserRevokedPermission", "<#72e4dc>Removed the permission <#b08598>{0}</color> from <#ffe4f8>{1}</color>." },
        { "UserAddedPermission", "<#72e4dc>Added the permission <#b08598>{0}</color> to <#ffe4f8>{1}</color>." },
        { "UserClearedPermissions", "<#72e4dc>Cleared all permissions from <#ffe4f8>{0}</color>." },
    };
}
#endif