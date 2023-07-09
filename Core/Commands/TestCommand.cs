﻿using System.Diagnostics;
using System.Globalization;
using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using System.Reflection;
using DevkitServer.AssetTools;
using DevkitServer.Configuration;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Sync;
#if CLIENT
using DevkitServer.Players.UI;
using DevkitServer.Util.Debugging;
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
    private static void grab(CommandContext ctx)
    {
        if (ctx.HasArgsExact(1) && ctx.TryGet(0, out string resourcePath))
        {
            string actualPath = Path.Combine(DevkitServerConfig.Directory, "AssetExports", resourcePath);
            if (Grabber.DownloadResource<Object>(resourcePath, actualPath))
            {
                ctx.ReplyString("Saved to <#fff>" + actualPath + "</color>.");
                if (!Directory.Exists(actualPath))
                    actualPath = Path.GetDirectoryName(actualPath)!;
                Process.Start(actualPath);
            }
            else ctx.ReplyString("<#ffae3d>Couldn't save.");
        }
        else if (ctx.HasArgsExact(2) && ctx.TryGet(0, out string bundlePath) && ctx.TryGet(1, out resourcePath))
        {
            if (bundlePath.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
            {
                Bundle bundle = new Bundle(bundlePath, true, "Temp bundle");

                string actualPath = Path.Combine(DevkitServerConfig.Directory, "AssetExports", Path.GetFileName(bundlePath), resourcePath);
                if (Grabber.DownloadFromBundle<Object>(bundle, resourcePath, actualPath))
                {
                    Process.Start(Path.GetDirectoryName(actualPath)!);
                    ctx.ReplyString("Saved to <#fff>" + actualPath + "</color>.");
                }
                else ctx.ReplyString("<#ffae3d>Couldn't save.");

                bundle.unload();
            }
        }
        else ctx.SendCorrectUsage("/test savetexture <resource>");
    }

#if DEBUG
    private static void tiledebug(CommandContext ctx)
    {
        RegionDebug.TilesEnabled = !RegionDebug.TilesEnabled;
        ctx.ReplyString($"Tile Debug: {RegionDebug.TilesEnabled}.");
    }
    private static void regiondebug(CommandContext ctx)
    {
        RegionDebug.RegionsEnabled = !RegionDebug.RegionsEnabled;
        ctx.ReplyString($"Tile Debug: {RegionDebug.RegionsEnabled}.");
    }
#endif

#endif
    private static void syncall(CommandContext ctx)
    {
        TileSync? tileSyncAuth = TileSync.GetAuthority();
#if CLIENT
        if (tileSyncAuth == null || !tileSyncAuth.IsOwner)
            ctx.BreakAndRunOnServer();
#endif
        if (tileSyncAuth == null)
            throw ctx.SendUnknownError();
        float t = CachedTime.RealtimeSinceStartup;
        if (!ctx.HasArg(0) || ctx.MatchParameter(0, "hm", "heightmap", "heights"))
            tileSyncAuth.InvalidateBounds(CartographyUtil.CaptureBounds, TileSync.DataType.Heightmap, t);
        if (!ctx.HasArg(0) || ctx.MatchParameter(0, "sm", "splatmap", "materials"))
            tileSyncAuth.InvalidateBounds(CartographyUtil.CaptureBounds, TileSync.DataType.Splatmap, t);
        if (!ctx.HasArg(0) || ctx.MatchParameter(0, "holes", "holemap"))
            tileSyncAuth.InvalidateBounds(CartographyUtil.CaptureBounds, TileSync.DataType.Holes, t);

        ObjectSync? objectSyncAuth = ObjectSync.GetAuthority();
#if CLIENT
        if (objectSyncAuth == null || !objectSyncAuth.IsOwner)
            ctx.BreakAndRunOnServer();
#endif

        if (objectSyncAuth == null)
            throw ctx.SendUnknownError();
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
                List<LevelObject> objects = LevelObjects.objects[x, y];
                for (int i = 0; i < objects.Count; ++i)
                {
                    objectSyncAuth.EnqueueSync(objects[i]);
                }
                List<LevelBuildableObject> buildables = LevelObjects.buildables[x, y];
                for (int i = 0; i < buildables.Count; ++i)
                {
                    objectSyncAuth.EnqueueSync(new RegionIdentifier(x, y, i));
                }
            }
        }

        ctx.ReplyString("<#7bbc5f>Syncing...");
    }

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