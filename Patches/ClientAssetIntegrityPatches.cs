using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Formatting;
using DevkitServer.API;
using DevkitServer.Configuration;
using HarmonyLib;
using SDG.NetPak;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Patches;


internal static class ClientAssetIntegrityPatches
{
    private const string Source = "CLIENT ASSET INTEGRITY PATCHES";

    public static void Patch()
    {
#if SERVER
        Type? msgHandlerType = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ServerMessageHandler_ValidateAssets", false, false);
        if (msgHandlerType == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch client asset integrity request. Unknown type: {"SDG.Unturned.ServerMessageHandler_ValidateAssets".Colorize(FormattingColorType.Class)}.");
            return;
        }

        MethodInfo? readMessage = msgHandlerType.GetMethod("ReadMessage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (readMessage == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch client asset integrity request. Failed to find method: {Accessor.Formatter.Format(
                new MethodDefinition("ReadMessage")
                    .DeclaredIn(msgHandlerType, isStatic: true)
                    .WithParameter<ITransportConnection>("transportConnection")
                    .WithParameter<NetPakReader>("reader")
                    .ReturningVoid()
            )}.");
            return;
        }

        try
        {
            PatchesMain.Patcher.Patch(readMessage, transpiler: Accessor.Active.GetHarmonyMethod(TranspileReadMessage)!);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch client asset integrity request. Failed to patch method: {readMessage.Format()}.");
        }
#elif CLIENT
        Type? clientAssetIntegrity = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ClientAssetIntegrity", false, false);
        if (clientAssetIntegrity == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch client asset integrity loop. Unknown type: {"SDG.Unturned.ClientAssetIntegrity".Colorize(FormattingColorType.Class)}.");
            return;
        }

        MethodInfo? sendRequests = clientAssetIntegrity.GetMethod("SendRequests", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (sendRequests == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch client asset integrity loop. Failed to find method: {Accessor.Formatter.Format(
                new MethodDefinition("SendRequests")
                    .DeclaredIn(clientAssetIntegrity, isStatic: true)
                    .WithNoParameters()
                    .ReturningVoid()
            )}.");
            return;
        }

        try
        {
            PatchesMain.Patcher.Patch(sendRequests, prefix: Accessor.Active.GetHarmonyMethod(PrefixSendRequests)!);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch client asset integrity loop. Failed to patch method: {sendRequests.Format()}.");
        }
#endif
    }
    public static void Unpatch()
    {
#if SERVER
        Type? msgHandlerType = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ServerMessageHandler_ValidateAssets", false, false);
        if (msgHandlerType == null)
            return;

        MethodInfo? readMessage = msgHandlerType.GetMethod("ReadMessage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (readMessage == null)
            return;

        try
        {
            PatchesMain.Patcher.Unpatch(readMessage, Accessor.GetMethod(TranspileReadMessage)!);
        }
        catch
        {
            // ignored
        }
#elif CLIENT
        Type? clientAssetIntegrity = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ClientAssetIntegrity", false, false);
        if (clientAssetIntegrity == null)
            return;

        MethodInfo? sendRequests = clientAssetIntegrity.GetMethod("SendRequests", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (sendRequests == null)
            return;

        try
        {
            PatchesMain.Patcher.Unpatch(sendRequests, Accessor.GetMethod(PrefixSendRequests)!);
        }
        catch
        {
            // ignored
        }
#endif
    }

#if SERVER
    public static IEnumerable<CodeInstruction> TranspileReadMessage(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        TranspileContext ctx = new TranspileContext(method, generator, instructions);

        FieldInfo? sendKickMissingAsset = typeof(Assets).GetField("SendKickForInvalidGuid", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (sendKickMissingAsset == null)
        {
            return ctx.Fail(new FieldDefinition("SendKickForInvalidGuid")
                .DeclaredIn<Assets>(isStatic: true)
                .WithFieldType<ClientStaticMethod<Guid>>());
        }
        
        FieldInfo? sendKickHash = typeof(Assets).GetField("SendKickForHashMismatch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (sendKickHash == null)
        {
            return ctx.Fail(new FieldDefinition("SendKickForHashMismatch")
                .DeclaredIn<Assets>(isStatic: true)
                .WithFieldType<ClientStaticMethod<Guid, string, string, byte[], string, string>>());
        }

        MethodInfo kickMethod = Accessor.GetMethod(Provider.kick)!;

        int rtnCt = 0;
        while (ctx.MoveNext())
        {
            if (ctx.Instruction.LoadsField(sendKickMissingAsset) || ctx.Instruction.LoadsField(sendKickHash))
            {
                PatchUtility.ReturnIfFalse(ctx, CheckCanKick);
                ++rtnCt;
            }
            else if (rtnCt == 2 && ctx.Instruction.Calls(kickMethod))
            {
                ++rtnCt;

                int index = ctx.GetLastUnconsumedIndex(null);
                int oldIndex = ctx.CaretIndex;
                ctx.CaretIndex = index;

                oldIndex += PatchUtility.ReturnIfFalse(ctx, CheckCanKick);
                ctx.CaretIndex = oldIndex;
            }
        }

        return ctx;
    }

    private static bool CheckCanKick()
    {
        return DevkitServerConfig.Config is not { DisableAssetValidation: true };
    }
#elif CLIENT

    private static bool PrefixSendRequests()
    {
        return !DevkitServerModule.IsEditing || Provider.isServer || NetFactory.IsAccepted;
    }
#endif
}