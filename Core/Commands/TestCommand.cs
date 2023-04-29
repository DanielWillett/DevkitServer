using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using System.Reflection;
#if CLIENT
using DevkitServer.Players.UI;
#endif

namespace DevkitServer.Core.Commands;
internal sealed class TestCommand : SynchronousCommand, ICommandLocalizationFile
{
    [Permission]
    public static readonly Permission Test = new Permission("test", devkitServer: true);
    [Permission]
    public static readonly Permission TestAll = new Permission("test.*", devkitServer: true);

    public IReadOnlyList<Permission> SubcommandPermissions;
    Local ILocalizedCommand.Translations { get; set; } = null!;
    public TestCommand() : base("test")
    {
        AddAlias("tset");
        AddAlias("tets");
        AddPermission(Test);
        AddPermission(TestAll);
        Permission[] perms = new Permission[CommandTests.Commands.Length];
        for (int i = 0; i < CommandTests.Commands.Length; ++i)
        {
            Permission perm = new Permission("test." + CommandTests.Commands[i].Method.Name.ToLowerInvariant(), devkitServer: true);
            AddPermission(perm);
            UserPermissions.Handler.Register(perm);
            perms[i] = perm;
        }

        SubcommandPermissions = perms;
    }

    public override void Execute(CommandContext ctx)
    {
        ctx.AssertRanByPlayer();

        ctx.AssertHelpCheckFormat(0, "CorrectUsage");

        if (ctx.TryGet(0, out string method))
        {
            for (int i = 0; i < CommandTests.Commands.Length; ++i)
            {
                if (CommandTests.Commands[i].Method.Name.Equals(method, StringComparison.InvariantCultureIgnoreCase))
                {
                    ctx.AssertPermissionsOr(TestAll, SubcommandPermissions[i]);

                    ++ctx.ArgumentOffset;
                    try
                    {
                        CommandTests.Commands[i](ctx);
                    }
                    catch (CommandContext)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        ctx.Reply("Exception", CommandTests.Commands[i].Method.Name.ToLowerInvariant(), ex.GetType().Name);
                        Logger.LogError(ex);
                    }
                    finally
                    {
                        --ctx.ArgumentOffset;
                    }
                    return;
                }
            }
#if CLIENT
            ctx.BreakAndRunOnServer();
#endif
            ctx.Reply("CommandNotFound", method);
        }
        else ctx.Reply("CorrectUsage");
    }

    public string TranslationsDirectory => nameof(TestCommand);
    public LocalDatDictionary DefaultTranslations => new LocalDatDictionary
    {
        { "CorrectUsage", "<#ff8c69>Correct Usage: /test <command name>." },
        { "CommandNotFound", "<#ff8c69>Unknown subcommand: <#00bcff>{0}</color>." },
        { "Exception", "<#ff8c69>Error while running the <#00bcff>{0}</color> subcommand: <#006ecd>{1}</color>." }
    };
}
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE1006 // Naming Styles
internal static class CommandTests
{
    public static readonly Action<CommandContext>[] Commands;
#if CLIENT
    private static void ui(CommandContext ctx)
    {
        ctx.AssertRanByPlayer();
        ctx.AssertHelpCheck(0, "/test ui <open|close> - Show or hide ui.");
        if (ctx.MatchParameter(0, "open", "show"))
        {
            DevkitEditorHUD.Open();
            ctx.ReplyString("<#7bbc5f>Opened DevkitEditorHUD.");
        }
        else if (ctx.MatchParameter(0, "close", "hide"))
        {
            DevkitEditorHUD.Close(false);
            ctx.ReplyString("<#7bbc5f>Closed DevkitEditorHUD.");
        }
        else ctx.SendCorrectUsage("/test ui <open|close>");
    }
#endif

#if SERVER


#endif

    static CommandTests()
    {
        try
        {
            MethodInfo[] methods = typeof(CommandTests).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(
                x =>
                {
                    ParameterInfo[] p = x.GetParameters();
                    return p.Length == 1 && p[0].ParameterType.IsAssignableFrom(typeof(CommandContext));
                }).ToArray();
            Commands = new Action<CommandContext>[methods.Length];
            for (int i = 0; i < methods.Length; ++i)
                Commands[i] = (Action<CommandContext>)methods[i].CreateDelegate(typeof(Action<CommandContext>));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }
}
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore IDE0051 // Remove unused private members
// ReSharper restore UnusedParameter.Local
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming