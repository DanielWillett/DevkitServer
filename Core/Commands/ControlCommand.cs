#if SERVER
using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Players;

namespace DevkitServer.Core.Commands;
internal sealed class ControlCommand : DevkitServerCommand, ICommandLocalizationFile
{
    [Permission]
    public static readonly Permission ChangeController = new Permission("control", devkitServer: true);
    [Permission]
    public static readonly Permission ChangeControllerPlayer = new Permission("control.player", devkitServer: true);
    [Permission]
    public static readonly Permission ChangeControllerEditor = new Permission("control.editor", devkitServer: true);
    [Permission]
    public static readonly Permission ChangeControllerAll = new Permission("control.*", devkitServer: true);

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
        ctx.AssertRanByPlayer();

        ctx.AssertHelpCheckFormat(0, "CorrectUsage");

        if (ctx.MatchParameter(0, "edit", "editor", "e"))
        {
            string fmt = ctx.Translate("ControllerEditor");
            if (ChangeControllerEditor.Has(ctx.Caller) || ChangeControllerAll.Has(ctx.Caller))
            {
                if (ctx.Caller.Input.Controller == CameraController.Editor)
                    throw ctx.Reply("AlreadySet", fmt);

                ctx.Caller.Input.Controller = CameraController.Editor;
                ctx.Reply("SetController", fmt);
            }
            else throw ctx.Reply("NoPermission", fmt);
        }
        else if (ctx.MatchParameter(0, "player", "character", "p"))
        {
            string fmt = ctx.Translate("ControllerPlayer");
            if (ChangeControllerPlayer.Has(ctx.Caller) || ChangeControllerAll.Has(ctx.Caller))
            {
                if (ctx.Caller.Input.Controller == CameraController.Player)
                    throw ctx.Reply("AlreadySet", fmt);

                ctx.Caller.Input.Controller = CameraController.Player;
                ctx.Reply("SetController", fmt);
            }
            else throw ctx.Reply("NoPermission", fmt);
        }
        else throw ctx.Reply("CorrectUsage");

        return UniTask.CompletedTask;
    }

    public string TranslationsDirectory => nameof(ControlCommand);
    public LocalDatDictionary DefaultTranslations => new LocalDatDictionary
    {
        { "CorrectUsage", "<#DB5375>Correct Usage: /control <" + nameof(CameraController.Editor).ToLower() + "|" + nameof(CameraController.Player).ToLower() + "> - Set player controller." },
        { "NoPermission", "<#DB5375>You do not have permission to change to a <#DFBE99>{0}</color> controller." },
        { "ControllerEditor", "Editor" },
        { "ControllerPlayer", "Player" },
        { "SetController", "<#B5BD89>Changed to <#729EA1>{0}</color> controller."},
        { "AlreadySet", "<#DFBE99>You are already on the <#B5BD89>{0}</color> controller." }
    };
}
#endif