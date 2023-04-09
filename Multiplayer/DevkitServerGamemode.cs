using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
    public override GameObject getPlayerGameObject(SteamPlayerID playerID)
    {
        if (!DevkitServerModule.IsEditing)
            return base.getPlayerGameObject(playerID);
        bool owner = playerID.steamID.m_SteamID == Provider.client.m_SteamID;
        GameObject obj = base.getPlayerGameObject(playerID);
        MainCamera? c = obj.GetComponentInChildren<MainCamera>();
        Transform? cam = obj.transform.FindChildRecursive("Camera");
        if (c != null)
        {
            Object.Destroy(c);
            Logger.LogDebug("Removed main camera from character object.");
        }
        if (cam != null)
        {
            cam.gameObject.SetActive(false);
            Logger.LogDebug("Disabled camera object on character object.");
            if (owner)
            {
                c = Editor.editor.transform.GetComponentInChildren<MainCamera>();
                GameObject parent = c.gameObject;
                if (c != null)
                    Object.Destroy(c);
                parent.AddComponent<MainCamera>();
                Logger.LogDebug("Replaced camera object on editor object.");
            }
        }
        Logger.DumpGameObject(obj);
        if (!owner)
        {
            if (obj.TryGetComponent(out Rigidbody body))
            {
                body.useGravity = false;
                body.detectCollisions = false;
            }
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
