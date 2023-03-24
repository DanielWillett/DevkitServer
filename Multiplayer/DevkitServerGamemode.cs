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
        GameObject go = base.getPlayerGameObject(playerID);
        if (!DevkitServerModule.IsEditing)
            return go;
        EditorUser euser = go.AddComponent<EditorUser>();
        euser.Init(playerID.steamID,
#if SERVER
            Provider.findTransportConnection(playerID.steamID),
#else
            NetFactory.GetPlayerTransportConnection(),
#endif
            playerID.characterName);
        if (go.TryGetComponent(out Rigidbody body))
        {
            body.useGravity = false;
            body.detectCollisions = false;
        }
        return go;
    }
}
