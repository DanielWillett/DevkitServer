using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Core.Commands;
internal sealed class ControlCommand : DevkitServerCommand, ICommandLocalizationFile
{
    public static readonly PermissionLeaf ChangeController = new PermissionLeaf("control", devkitServer: true);
    public static readonly PermissionLeaf ChangeControllerPlayer = new PermissionLeaf("control.player", devkitServer: true);
    public static readonly PermissionLeaf ChangeControllerEditor = new PermissionLeaf("control.editor", devkitServer: true);
    public static readonly PermissionLeaf ChangeControllerAll = new PermissionLeaf("control.*", devkitServer: true);

    Local ILocalizedCommand.Translations { get; set; } = null!;
    public ControlCommand() : base("control")
    {
        AddAlias("ctrl");
        AddAlias("controller");
        AddPermission(ChangeController);
        AddPermission(ChangeControllerPlayer);
        AddPermission(ChangeControllerEditor);
        AddPermission(ChangeControllerAll);
    }

    public override UniTask Execute(CommandContext ctx, CancellationToken token)
    {
#if CLIENT
        if (DevkitServerModule.IsEditing)
            ctx.BreakAndRunOnServer();
        
        throw ctx.Reply("ControlSingleplayerUnsupported");
#elif SERVER

        ctx.AssertRanByEditorUser();

        ctx.AssertHelpCheckFormat(0, "CorrectUsage");

        if (ctx.MatchParameter(0, "edit", "editor", "e"))
        {
            string fmt = ctx.Translate("ControllerEditor");
            if (ChangeControllerEditor.Has(ctx.CallerId.m_SteamID) || ChangeControllerAll.Has(ctx.CallerId.m_SteamID))
            {
                if (ctx.EditorUser!.Input.Controller == CameraController.Editor)
                    throw ctx.Reply("AlreadySet", fmt);

                ctx.EditorUser.Input.Controller = CameraController.Editor;
                ctx.Reply("SetController", fmt);
            }
            else throw ctx.Reply("NoPermission", fmt);
        }
        else if (ctx.MatchParameter(0, "player", "character", "p"))
        {
            string fmt = ctx.Translate("ControllerPlayer");
            if (ChangeControllerPlayer.Has(ctx.CallerId.m_SteamID) || ChangeControllerAll.Has(ctx.CallerId.m_SteamID))
            {
                if (ctx.EditorUser!.Input.Controller == CameraController.Player)
                    throw ctx.Reply("AlreadySet", fmt);

                ctx.EditorUser.Input.Controller = CameraController.Player;
                ctx.Reply("SetController", fmt);
            }
            else throw ctx.Reply("NoPermission", fmt);
        }
        else throw ctx.Reply("CorrectUsage");

        return UniTask.CompletedTask;
#endif
    }

    public string TranslationsDirectory => nameof(ControlCommand);
    public LocalDatDictionary DefaultTranslations => new LocalDatDictionary
    {
#if SERVER
        { "CorrectUsage", "<#DB5375>Correct Usage: /control <" + nameof(CameraController.Editor).ToLower() + "|" + nameof(CameraController.Player).ToLower() + "> - Set player controller." },
        { "NoPermission", "<#DB5375>You do not have permission to change to a <#DFBE99>{0}</color> controller." },
        { "ControllerEditor", "Editor" },
        { "ControllerPlayer", "Player" },
        { "SetController", "<#B5BD89>Changed to <#729EA1>{0}</color> controller." },
        { "AlreadySet", "<#DFBE99>You are already on the <#B5BD89>{0}</color> controller." },
#endif
        { "ControlSingleplayerUnsupported", "<#DFBE99>Editor control setting only works on DevkitServer servers." }
    };
}