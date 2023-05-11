using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Configuration;
#endif
#if CLIENT
using DevkitServer.Multiplayer.Networking;
#endif

namespace DevkitServer.Multiplayer;
public class DevkitServerGamemode : GameMode
{
    public static void SetupEditorObject(GameObject editor, EditorUser user)
    {
        editor.AddComponent<EditorActions>().User = user;
        editor.AddComponent<UserInput>().User = user;
        editor.AddComponent<UserTransactions>().User = user;
        editor.AddComponent<TileSync>().User = user;
#if CLIENT
        bool isOwner = user.IsOwner;
        if (!isOwner)
            editor.AddComponent<UserTPVControl>().User = user;
#endif
    }
#if SERVER
    public static ClientInfo GetClientInfo(EditorUser user)
    {
        return new ClientInfo
        {
            Permissions = UserPermissions.UserHandler.GetPermissions(user.SteamId.m_SteamID, true)?.ToArray() ?? Array.Empty<Permission>(),
            PermissionGroups = UserPermissions.UserHandler.GetPermissionGroups(user.SteamId.m_SteamID, true)?.ToArray() ?? Array.Empty<PermissionGroup>(),
            EnablePixelAverageSplatmapSmoothing = DevkitServerConfig.Config.EnablePixelAverageSplatmapSmoothing
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
