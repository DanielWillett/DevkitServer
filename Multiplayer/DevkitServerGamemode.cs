using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;

namespace DevkitServer.Multiplayer;
public class DevkitServerGamemode : GameMode
{
    public static void SetupEditorObject(GameObject editor, EditorUser user)
    {
        bool isOwner = user.IsOwner;
        editor.AddComponent<EditorTerrain>().User = user;
        editor.AddComponent<UserInput>().User = user;
#if CLIENT
        if (!isOwner)
        {
            editor.AddComponent<UserTPVControl>().User = user;
        }
#endif
    }
#if SERVER
    public static ClientInfo GetClientInfo(EditorUser user)
    {
        return new ClientInfo
        {
            Permissions = UserPermissions.UserHandler.GetPermissions(user.SteamId.m_SteamID, true)?.ToArray() ?? Array.Empty<Permission>(),
            PermissionGroups = UserPermissions.UserHandler.GetPermissionGroups(user.SteamId.m_SteamID, true)?.ToArray() ?? Array.Empty<PermissionGroup>(),
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
