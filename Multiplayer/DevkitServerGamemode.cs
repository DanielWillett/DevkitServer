using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Movement;
using DevkitServer.Players;
using DevkitServer.Multiplayer.Sync;
#if SERVER
using DevkitServer.API.Permissions;
#endif
#if CLIENT
using DevkitServer.Multiplayer.Networking;
#endif

namespace DevkitServer.Multiplayer;
public class DevkitServerGamemode : GameMode
{
    public static void SetupEditorObject(GameObject editor, EditorUser user)
    {
        EditorActions actions = editor.AddComponent<EditorActions>();
        actions.User = user;
        editor.AddComponent<UserInput>().User = user;
        editor.AddComponent<UserTransactions>().User = user;
        editor.AddComponent<TileSync>().User = user;
        editor.AddComponent<ObjectSync>().User = user;
        editor.AddComponent<HierarchySync>().User = user;
        editor.AddComponent<NavigationSync>().User = user;
        editor.AddComponent<RoadSync>().User = user;
        bool isOwner = user.IsOwner;
#if CLIENT
        actions.IsOwner = isOwner;
        if (!isOwner)
            editor.AddComponent<UserTPVControl>().User = user;
#endif
        if (!isOwner)
            editor.AddComponent<UserMovement>().User = user;
    }
#if SERVER
    public static ClientInfo GetClientInfo(CSteamID user)
    {
        return new ClientInfo
        {
            Permissions = PermissionManager.UserPermissions.GetPermissions(user.m_SteamID, true)?.ToArray() ?? Array.Empty<PermissionBranch>(),
            PermissionGroups = PermissionManager.UserPermissions.GetPermissionGroups(user.m_SteamID, true)?.ToArray() ?? Array.Empty<PermissionGroup>()
        };
    }
#endif
    public override GameObject getPlayerGameObject(SteamPlayerID playerID)
    {
        if (!DevkitServerModule.IsEditing)
            return base.getPlayerGameObject(playerID);
        GameObject obj = base.getPlayerGameObject(playerID);
        EditorUser user = obj.AddComponent<EditorUser>();
        user.PreInit(playerID.steamID, playerID.playerName);
#if CLIENT
        if (playerID.steamID.m_SteamID == Provider.client.m_SteamID)
        {
            user.Connection = NetFactory.GetPlayerTransportConnection();
            EditorUser.User = user;
        }
#endif
        return obj;
    }
}
