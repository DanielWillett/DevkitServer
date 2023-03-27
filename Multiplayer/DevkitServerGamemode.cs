using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.Players;

namespace DevkitServer.Multiplayer;
public class DevkitServerGamemode : GameMode
{
    public override GameObject getPlayerGameObject(SteamPlayerID playerID)
    {
        if (!DevkitServerModule.IsEditing)
            return base.getPlayerGameObject(playerID);
        bool owner = playerID.steamID.m_SteamID == Provider.client.m_SteamID;
        GameObject obj;
        if (!owner)
        {
            obj = base.getPlayerGameObject(playerID);
            if (obj.TryGetComponent(out Rigidbody body))
            {
                body.useGravity = false;
                body.detectCollisions = false;
            }
        }
        else
        {
            obj = Level.editing.gameObject;
        }
        EditorUser user = obj.AddComponent<EditorUser>();
        user.Init(playerID.steamID,
#if SERVER
            Provider.findTransportConnection(playerID.steamID),
#else
            NetFactory.GetPlayerTransportConnection(),
#endif
            playerID.playerName);

        return obj;
    }
}
