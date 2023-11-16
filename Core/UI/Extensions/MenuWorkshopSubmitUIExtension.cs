#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Patches;
using HarmonyLib;
using SDG.Provider;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.API;

namespace DevkitServer.Core.UI.Extensions;

[UIExtension(typeof(MenuWorkshopSubmitUI), SuppressUIExtensionParentWarning = true)]
internal class MenuWorkshopSubmitUIExtension : IDisposable
{
    [ExistingMember("container")]
    private SleekFullscreenBox Container { get; }

    private static ISleekField _existingModId = null!;
    private MethodInfo? _patchedCreate;
    private MethodInfo? _patchedPublished;
    private readonly MethodInfo _transpiler;
    private readonly MethodInfo _postfix;
    public MenuWorkshopSubmitUIExtension()
    {
        _existingModId = Glazier.Get().CreateStringField();
        _existingModId.PositionOffset_X = -200;
        _existingModId.PositionOffset_Y = 100;
        _existingModId.PositionScale_X = 0.5f;
        _existingModId.SizeOffset_X = 200;
        _existingModId.SizeOffset_Y = 30;
        _existingModId.AddLabel(DevkitServerModule.MainLocalization.Translate("WorkshopSubmitMenuExistingModIdLabel"), ESleekSide.RIGHT);
        Container!.AddChild(_existingModId);
        _transpiler = Accessor.GetMethod(CreateModTranspiler)!;
        _postfix = Accessor.GetMethod(ClearTextPostfix)!;
        try
        {
            MethodInfo? onClickedCreateButton = typeof(MenuWorkshopSubmitUI).GetMethod("onClickedCreateButton", BindingFlags.Static | BindingFlags.NonPublic);
            if (onClickedCreateButton == null)
            {
                Logger.LogError("Unable to find method: MenuWorkshopSubmitUI.onClickedCreateButton. Override mod ID feature for workshop uploading will not work.");
                return;
            }

            PatchesMain.Patcher.Patch(onClickedCreateButton, transpiler: new HarmonyMethod(_transpiler));
            _patchedCreate = onClickedCreateButton;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to patch MenuWorkshopSubmitUI.onClickedCreateButton. Override mod ID feature for workshop uploading will not work.");
            Logger.LogError(ex);
        }

        try
        {

            MethodInfo? onClickedPublish = typeof(MenuWorkshopSubmitUI).GetMethod("onClickedPublished", BindingFlags.Static | BindingFlags.NonPublic);
            if (onClickedPublish == null)
            {
                Logger.LogWarning("Unable to find method: MenuWorkshopSubmitUI.onClickedPublished.");
                return;
            }

            PatchesMain.Patcher.Patch(onClickedPublish, postfix: new HarmonyMethod(_postfix));
            _patchedPublished = onClickedPublish;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to patch MenuWorkshopSubmitUI.onClickedPublished.");
            Logger.LogError(ex);
        }
    }

    private static bool HasModId() => _existingModId is not null && ulong.TryParse(_existingModId.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out ulong id) && id != 0;
    private static PublishedFileId_t GetModId() => new PublishedFileId_t(_existingModId is not null && ulong.TryParse(_existingModId.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out ulong id) ? id : 0);
    private static TempSteamworksWorkshop GetWorkshop() => Provider.provider.workshopService;
    private static void ClearText() => _existingModId.Text = string.Empty;
    [UsedImplicitly]
    private static void ClearTextPostfix(ISleekElement button) => _existingModId.Text = string.Empty;
    private static IEnumerable<CodeInstruction> CreateModTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        MethodInfo? prepareUgcStep1 = typeof(TempSteamworksWorkshop).GetMethod("prepareUGC", BindingFlags.Public | BindingFlags.Instance, null, new Type[]
        {
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(ESteamUGCType), typeof(string), typeof(string), typeof(ESteamUGCVisibility)
        }, null);
        if (prepareUgcStep1 == null)
        {
            Logger.LogWarning($"{method.Format()} - Failed to find method: TempSteamworksWorkshop.prepareUGC(...) (prepare step 1). Override mod ID feature for workshop uploading will not work.");
        }
        MethodInfo? checkEntered = typeof(MenuWorkshopSubmitUI).GetMethod("checkEntered", BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
        if (checkEntered == null)
        {
            Logger.LogWarning($"{method.Format()} - Failed to find method: MenuWorkshopSubmitUI.checkEntered(). Override mod ID feature for workshop uploading will not work.");
        }
        MethodInfo? prepareUgcStep2 = typeof(TempSteamworksWorkshop).GetMethod("prepareUGC", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(PublishedFileId_t) }, null);
        if (prepareUgcStep2 == null)
        {
            Logger.LogWarning($"{method.Format()} - Failed to find method: TempSteamworksWorkshop.prepareUGC(PublishedFileId_t) (prepare step 2). Override mod ID feature for workshop uploading will not work.");
        }
        MethodInfo? createUgc = typeof(TempSteamworksWorkshop).GetMethod("createUGC", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(bool) }, null);
        if (createUgc == null)
        {
            Logger.LogWarning($"{method.Format()} - Failed to find method: TempSteamworksWorkshop.createUgc(bool). Override mod ID feature for workshop uploading will not work.");
        }
        MethodInfo? updateUGC = typeof(TempSteamworksWorkshop).GetMethod("updateUGC", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (createUgc == null)
        {
            Logger.LogWarning($"{method.Format()} - Failed to find method: TempSteamworksWorkshop.updateUGC(). Override mod ID feature for workshop uploading will not work.");
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        Label skipCheck = generator.DefineLabel();
        Label prepareInsteadOfCreate = generator.DefineLabel();
        Label createInsteadOfPrepare = generator.DefineLabel();
        bool br0 = false, br1 = false, br2 = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (PatchUtil.MatchPattern(ins, i,
                    x => checkEntered != null && x.Calls(checkEntered),
                    x => x.opcode.IsBrAny()))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(HasModId)));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brtrue, skipCheck));
                ins[i + 4].labels.Add(skipCheck);
                br0 = true;
                i += 2;
            }
            else if (br0 && PatchUtil.FollowPattern(ins, ref i,
                    x => prepareUgcStep1 != null && x.Calls(prepareUgcStep1)
                ))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(HasModId)));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brtrue, prepareInsteadOfCreate));
                br1 = true;
            }
            else if (br1 && PatchUtil.FollowPattern(ins, ref i,
                    x => createUgc != null && x.Calls(createUgc)
                ))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Br, createInsteadOfPrepare));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(GetWorkshop)).WithLabels(prepareInsteadOfCreate));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Dup));
                ins.Insert(i + 3, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(GetModId)));
                ins.Insert(i + 4, new CodeInstruction(OpCodes.Callvirt, prepareUgcStep2));
                ins.Insert(i + 5, new CodeInstruction(OpCodes.Callvirt, updateUGC));
                ins[i + 6].labels.Add(createInsteadOfPrepare);
                br2 = true;
            }
        }
        if (!br2)
        {
            Logger.LogWarning($"Failed to patch {method.Format()}. Override mod ID feature for workshop uploading will not work.");
        }

        int index = ins[ins.Count - 1].opcode == OpCodes.Ret ? ins.Count - 1 : ins.Count;
        ins.Insert(index, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(ClearText)));

        return ins;
    }
    public void Dispose()
    {
        try
        {
            if (_patchedCreate != null)
            {
                PatchesMain.Patcher.Unpatch(_patchedCreate, _transpiler);
                _patchedCreate = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to unpatch MenuWorkshopSubmitUI.onClickedCreateButton.");
            Logger.LogError(ex);
        }

        try
        {
            if (_patchedPublished != null)
            {
                PatchesMain.Patcher.Unpatch(_patchedPublished, _postfix);
                _patchedPublished = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to unpatch MenuWorkshopSubmitUI.onClickedPublished.");
            Logger.LogError(ex);
        }

        _existingModId = null!;
    }
}
#endif