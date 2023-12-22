using DevkitServer.Multiplayer.Networking;
#if SERVER
using DevkitServer.API.UI;
using DevkitServer.Multiplayer;
#endif

namespace DevkitServer.Players;
public class UserTransactions : MonoBehaviour
{
    [UsedImplicitly]
    private static readonly NetCall<bool> SendReunRequest = new NetCall<bool>((ushort)DevkitServerNetCall.ReunRequest);
    public EditorUser User { get; internal set; } = null!;

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public bool IsOwner { get; private set; }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(nameof(UserTransactions), "Invalid UserTransactions setup; EditorUser not found!");
            return;
        }

#if CLIENT
        IsOwner = User == EditorUser.User;
#endif

        Logger.DevkitServer.LogDebug(nameof(UserTransactions), "User transaction module created for " + User.SteamId.m_SteamID.Format() + " ( owner: " + IsOwner.Format() + " ).");
    }
#if SERVER
    private void ReceiveReunRequest(bool isRedo)
    {
        if (isRedo)
            EditorMessage.SendEditorMessage(User, DevkitServerModule.MessageLocalization.Translate("RedoNotSupported"));
        else
            EditorMessage.SendEditorMessage(User, DevkitServerModule.MessageLocalization.Translate("UndoNotSupported"));
        Logger.DevkitServer.LogDebug(nameof(UserTransactions), $"{User.Format()} Requested a{(isRedo ? " redo." : "n undo.")}");
    }
    [NetCall(NetCallSource.FromClient, (ushort)DevkitServerNetCall.ReunRequest)]
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