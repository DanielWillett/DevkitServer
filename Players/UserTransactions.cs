using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players.UI;
using JetBrains.Annotations;
using SDG.Framework.Devkit.Transactions;

namespace DevkitServer.Players;
public class UserTransactions : MonoBehaviour
{
    private static readonly NetCall<bool> SendReunRequest = new NetCall<bool>((ushort)NetCalls.ReunRequest);
    public EditorUser User { get; internal set; } = null!;

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public bool IsOwner { get; private set; }
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.LogError("Invalid UserTransactions setup; EditorUser not found!");
            return;
        }

#if CLIENT
        IsOwner = User == EditorUser.User;
#endif

        Logger.LogDebug("User transaction module created for " + User.SteamId.m_SteamID.Format() + " ( owner: " + IsOwner.Format() + " ).");
    }
#if SERVER
    private void ReceiveReunRequest(bool isRedo)
    {
        if (isRedo)
            UIMessage.SendEditorMessage(User, DevkitServerModule.MessageLocalization.Translate("RedoNotSupported"));
        else
            UIMessage.SendEditorMessage(User, DevkitServerModule.MessageLocalization.Translate("UndoNotSupported"));
        Logger.LogDebug($"{User.Format()} Requested a{(isRedo ? " redo." : "n undo.")}");
    }
    [NetCall(NetCallSource.FromClient, (ushort)NetCalls.ReunRequest)]
    [UsedImplicitly]
    private static void ReceiveReunRequest(MessageContext ctx, bool isRedo)
    {
        EditorUser? user = UserManager.FromConnection(ctx.Connection);
        if (user != null && user.Transactions != null)
            user.Transactions.ReceiveReunRequest(isRedo);
    }
#endif
#if CLIENT
    public void RequestUndo()
    {
        SendReunRequest.Invoke(false);
    }
    public void RequestRedo()
    {
        SendReunRequest.Invoke(true);
    }
#endif
}

public readonly struct UserTransaction
{
    public readonly ulong User;
    public readonly DevkitTransactionGroup Transaction;
}