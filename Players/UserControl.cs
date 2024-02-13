using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Networking;
#if CLIENT
using DevkitServer.API.Abstractions;
using DevkitServer.Multiplayer;
using HarmonyLib;
using SDG.Framework.Devkit;
using System.Reflection;
using System.Reflection.Emit;
#endif

namespace DevkitServer.Players;

[EarlyTypeInit(-1)]
#if CLIENT
[HarmonyPatch]
#endif
public class UserControl : MonoBehaviour
{
    private const string Source = "INPUT";
    private static readonly CachedMulticastEvent<Action<EditorUser>> EventOnUserControllerUpdated = new CachedMulticastEvent<Action<EditorUser>>(typeof(UserControl), nameof(OnUserControllerUpdated));
#if SERVER
    private static readonly CachedMulticastEvent<UserControllerUpdateRequested> EventOnUserControllerUpdateRequested = new CachedMulticastEvent<UserControllerUpdateRequested>(typeof(UserControl), nameof(OnUserControllerUpdateRequested));
#endif

    [UsedImplicitly]
    private static readonly NetCall<ulong, CameraController> SendUpdateController = new NetCall<ulong, CameraController>(DevkitServerNetCall.SendUpdateController);
    [UsedImplicitly]
    private static readonly NetCall<CameraController> RequestUpdateController = new NetCall<CameraController>(DevkitServerNetCall.RequestUpdateController);
    [UsedImplicitly]
    private static readonly NetCall RequestInitialState = new NetCall(DevkitServerNetCall.RequestInitialState);
#if CLIENT
    // internal static CameraController CleaningUpController;
    internal static MethodInfo GetLocalControllerMethod = typeof(UserControl).GetProperty(nameof(LocalController), BindingFlags.Static | BindingFlags.Public)?.GetMethod!;
    public static CameraController LocalController
    {
        get
        {
            if (DevkitServerModule.IsEditing && EditorUser.User?.Control is not null)
                return EditorUser.User.Control.Controller;

            return Level.isEditor ? CameraController.Editor : (Level.isLoaded || Level.isLoading ? CameraController.Player : CameraController.None);
        }
    }
    public static Transform LocalAim
    {
        get
        {
            if (DevkitServerModule.IsEditing && EditorUser.User?.Control is not null && EditorUser.User.Control.Aim != null)
                return EditorUser.User.Control.Aim;

            return Level.isEditor || !Level.isLoaded ? MainCamera.instance.transform : Player.player.look.aim;
        }
    }
#endif
    private CameraController _controller;

    public static event Action<EditorUser> OnUserControllerUpdated
    {
        add => EventOnUserControllerUpdated.Add(value);
        remove => EventOnUserControllerUpdated.Remove(value);
    }
#if SERVER
    public static event UserControllerUpdateRequested OnUserControllerUpdateRequested
    {
        add => EventOnUserControllerUpdateRequested.Add(value);
        remove => EventOnUserControllerUpdateRequested.Remove(value);
    }
#endif
    public EditorUser User { get; internal set; } = null!;
    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public bool IsOwner { get; private set; }
    public GameObject? ControllerObject { get; private set; }
#if CLIENT
    /// <summary>
    /// The controller of the user's camera. Either <see cref="CameraController.Editor"/> or <see cref="CameraController.Player"/>.
    /// </summary>
    /// <remarks>Set with <see cref="RequestSetController"/>.</remarks>
#elif SERVER
    /// <summary>
    /// The controller of the user's camera. Either <see cref="CameraController.Editor"/> or <see cref="CameraController.Player"/>.
    /// </summary>
    /// <remarks>Setter replicates to clients.</remarks>
#endif
    public CameraController Controller
    {
        get => _controller;
#if CLIENT
        private
#endif
        set
        {
            ThreadUtil.assertIsGameThread();

            if (_controller == value)
                return;

            if (value is not CameraController.Editor and not CameraController.Player)
                throw new ArgumentOutOfRangeException(nameof(value), "Must be either Player or Editor.");

            ControllerObject = value switch
            {
                CameraController.Editor => User.EditorObject,
                CameraController.Player => User.Player!.player.gameObject,
                _ => ControllerObject
            };
            
            _controller = value;
            HandleControllerUpdated();
#if SERVER
            SendUpdateController.Invoke(Provider.GatherRemoteClientConnections(), User.SteamId.m_SteamID, value);
#endif
        }
    }

    /// <summary>
    /// Aim component used for raycasts. Changes based on the current <see cref="Controller"/>.
    /// </summary>
    public Transform? Aim => Controller switch
    {
        CameraController.Editor => IsOwner ? MainCamera.instance.transform : ControllerObject?.transform,
        CameraController.Player => User.Player?.player.look.aim,
        _ => null
    };

    [UsedImplicitly]
    private static readonly Action<PlayerUI> RestartPlayerUI = Accessor.GenerateInstanceCaller<PlayerUI, Action<PlayerUI>>("InitializePlayer", throwOnError: true, allowUnsafeTypeBinding: true)!;
#if SERVER
    private static readonly Action<Player, SteamPlayer> SendInitialState = Accessor.GenerateInstanceCaller<Player, Action<Player, SteamPlayer>>("SendInitialPlayerState", throwOnError: true, allowUnsafeTypeBinding: true)!;
#endif

#if CLIENT
    private static readonly Func<IDevkitTool?>? GetDevkitTool;
    private static readonly Action<IDevkitTool?>? SetDevkitTool;

    /// <summary>
    /// Get or set the active devkit tool (from EditorInteract.activeTool).
    /// </summary>
    public static IDevkitTool? ActiveTool
    {
        get => GetDevkitTool?.Invoke();
        set => SetDevkitTool?.Invoke(value);
    }
    static UserControl()
    {
        Type? type = Accessor.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract");
        if (type == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorInteract.");
            return;
        }
        FieldInfo? instanceField = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (instanceField == null || !instanceField.IsStatic || !type.IsAssignableFrom(instanceField.FieldType))
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorInteract.instance.");
            return;
        }
        FieldInfo? toolField = type.GetField("activeTool", BindingFlags.Instance | BindingFlags.NonPublic);
        if (toolField == null || toolField.IsStatic || !typeof(IDevkitTool).IsAssignableFrom(toolField.FieldType))
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorInteract.activeTool.");
        }
        MethodInfo? setToolMethod = type.GetMethod("SetActiveTool", BindingFlags.Instance | BindingFlags.Static |
                                                                    BindingFlags.NonPublic | BindingFlags.Public, null,
                                                                    CallingConventions.Any, new Type[] { typeof(IDevkitTool) }, null);
        if (setToolMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorInteract.SetActiveTool.");
        }

        Accessor.GetDynamicMethodFlags(true, out MethodAttributes attributes, out CallingConventions conventions);
        if (toolField != null)
        {
            DynamicMethod method = new DynamicMethod("get_activeTool", attributes,
                conventions, typeof(IDevkitTool),
                Type.EmptyTypes, type, true);
            IOpCodeEmitter il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Ldsfld, instanceField);
            il.Emit(OpCodes.Ldfld, toolField);
            il.Emit(OpCodes.Ret);
            GetDevkitTool = (Func<IDevkitTool?>)method.CreateDelegate(typeof(Func<IDevkitTool?>));
        }

        if (setToolMethod != null)
        {
            DynamicMethod method = new DynamicMethod("set_activeTool", attributes,
                conventions, typeof(void),
                new Type[] { typeof(IDevkitTool) }, type, true);
            IOpCodeEmitter il = method.GetILGenerator().AsEmitter();
            if (!setToolMethod.IsStatic)
                il.Emit(OpCodes.Ldsfld, instanceField);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(setToolMethod.GetCall(), setToolMethod);
            if (setToolMethod.ReturnType != typeof(void))
                il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
            SetDevkitTool = (Action<IDevkitTool?>)method.CreateDelegate(typeof(Action<IDevkitTool?>));
        }
    }
#endif
    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(Source, "Invalid UserInput setup; EditorUser not found!");
            return;
        }

#if CLIENT
        IsOwner = User == EditorUser.User;
        if (IsOwner)
        {
            if (!User.EditorObject.TryGetComponent(out EditorMovement _))
            {
                Destroy(this);
                Logger.DevkitServer.LogError(Source, "Invalid UserInput setup; EditorMovement not found!");
                return;
            }
            _controller = CameraController.Editor;
            ControllerObject = User.EditorObject;
            SetActiveMainCamera(User.EditorObject.transform);
        }
        else Controller = CameraController.Editor;
#endif
#if SERVER
        _controller = CameraController.Editor;
        ControllerObject = User.EditorObject;
#endif

        Logger.DevkitServer.LogDebug(Source, $"User input module created for {User.SteamId.m_SteamID.Format()} ( owner: {IsOwner.Format()} ).");
    }
#if SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestInitialState)]
    private static void ReceiveRequestInitialState(MessageContext ctx)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline || user.Player?.player == null)
            return;
        SendInitialState(user.Player.player, user.Player);
    }
#endif

    private void HandleControllerUpdated()
    {
        if (Controller == CameraController.Editor)
        {
            // on controller set to editor
#if SERVER
            User.Movement?.SetEditorPosition(User.Player!.player.look.aim.transform.position, User.Player!.player.look.aim.rotation.eulerAngles);
            if (User.Player!.player.life.isDead)
                User.Player.player.life.ReceiveRespawnRequest(false);
            else
                User.Player.player.life.sendRevive(); // heal the player
#endif
            User.Player!.player.movement.canAddSimulationResultsToUpdates = false;
            User.Player!.player.gameObject.SetActive(false);
        }
        else if (Controller == CameraController.Player)
        {
            // on controller set to player
            User.Player!.player.gameObject.SetActive(true);
            User.Player!.player.movement.canAddSimulationResultsToUpdates = true;
#if SERVER
            Vector3 position = User.EditorObject.transform.position;
            float yaw = User.EditorObject.transform.rotation.eulerAngles.y;
            if (Physics.Raycast(new Ray(position with { y = Level.HEIGHT }, Vector3.down), out RaycastHit info, Level.HEIGHT * 2f, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore))
            {
                position.y = info.point.y + 1.5f;
            }
            else
            {
                PlayerSpawnpoint spawn = LevelPlayers.getSpawn(false);
                position = spawn.point + new Vector3(0.0f, 1.5f, 0.0f);
                yaw = spawn.angle;
            }
            User.Player!.player.teleportToLocationUnsafe(position, yaw);
#endif
        }
#if CLIENT
        if (IsOwner)
        {
            GameObject? ctrl = ControllerObject;
            if (ctrl == null || !SetActiveMainCamera(ctrl.transform))
            {
                Logger.DevkitServer.LogDebug(Source, $"Unable to set main camera: {ctrl.Format()}.");
                return;
            }
#if DEBUG
            PlayerUI? playerUi = UIAccessTools.PlayerUI;
            if (playerUi != null)
                Logger.DevkitServer.LogDebug(Source, "PlayerUI " + (playerUi.gameObject.activeInHierarchy ? "active" : "inactive") +
                                                     " (" + (playerUi.enabled ? "enabled" : "disabled") + "): " + playerUi.gameObject.GetSceneHierarchyPath().Format(false) + ".");
            EditorUI? editorUi = UIAccessTools.EditorUI;
            if (editorUi != null)
                Logger.DevkitServer.LogDebug(Source, "EditorUI " + (editorUi.gameObject.activeInHierarchy ? "active" : "inactive") +
                                                     " (" + (editorUi.enabled ? "enabled" : "disabled") + "): " + editorUi.gameObject.GetSceneHierarchyPath().Format(false) + ".");
#endif

            Logger.DevkitServer.LogInfo(Source, $"Camera controller set to {Controller.Format()}.", ConsoleColor.DarkCyan);
        }
#endif
        EventOnUserControllerUpdated.TryInvoke(User);
    }
#if CLIENT
    internal static bool SetActiveMainCamera(Transform tranform)
    {
        Transform? child = tranform.FindChildRecursive("Camera");
        if (child == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Failed to find 'Camera' child.");
            return false;
        }

        MainCamera? cameraObj = child.GetComponentInChildren<MainCamera>();
        if (cameraObj == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Failed to find MainCamera component.");
            return false;
        }
        
        if (!cameraObj.transform.TryGetComponent(out Camera camera))
        {
            Logger.DevkitServer.LogWarning(Source, "Failed to find Camera component on MainCamera object.");
            return false;
        }

        if (MainCamera.instance == camera)
        {
            Logger.DevkitServer.LogDebug(Source, "Camera already set to the correct instance.");
            return true;
        }
        camera.enabled = true;
        Logger.DevkitServer.LogDebug(Source, "Camera enabled: " + camera.gameObject.GetSceneHierarchyPath().Format(false) + ".");

        Camera oldCamera = MainCamera.instance;
        if (oldCamera != null)
        {
            oldCamera.enabled = false;
            Logger.DevkitServer.LogDebug(Source, "Camera disabled: " + oldCamera.gameObject.GetSceneHierarchyPath().Format(false) + ".");
        }

        try
        {
            cameraObj.Awake();
            return true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Failed to set camera instance to " + tranform.gameObject.GetSceneHierarchyPath().Format(false) + ".");
        }

        return false;
    }
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendUpdateController)]
    private static void ReceiveControllerUpdated(MessageContext ctx, ulong steam64, CameraController controller)
    {
        EditorUser? user = UserManager.FromId(steam64);
        if (user != null)
            user.Control.Controller = controller;

        ctx.Acknowledge();
    }
    /// <returns><see langword="false"/> if the player lacks permission.</returns>
    public bool RequestSetController(CameraController controller)
    {
        if (controller is not CameraController.Editor and not CameraController.Player)
            throw new ArgumentOutOfRangeException(nameof(controller), "Must be either Player or Editor.");
        if (!IsOwner)
            throw new InvalidOperationException("Can not set another user's controller.");

        if (CheckPermissionForController(controller))
            RequestUpdateController.Invoke(controller);
        else return false;
        return true;
    }
#endif
    public static bool CheckPermissionForController(CameraController controller
#if SERVER
        , ulong user
#endif
    )
    {
        PermissionLeaf perm = controller == CameraController.Player
            ? VanillaPermissions.UsePlayerController
            : VanillaPermissions.UseEditorController;
        PermissionLeaf otherPerm = controller != CameraController.Player
            ? VanillaPermissions.UsePlayerController
            : VanillaPermissions.UseEditorController;
        return perm.Has(
#if SERVER
        user
#endif
            ) ||
               !otherPerm.Has(
#if SERVER
        user
#endif
            );
    }
#if SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestUpdateController)]
    private static StandardErrorCode ReceiveUpdateControllerRequest(MessageContext ctx, CameraController controller)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
            return StandardErrorCode.NotFound;

        if (CheckPermissionForController(controller, user.SteamId.m_SteamID))
        {
            bool shouldAllow = true;
            EventOnUserControllerUpdateRequested.TryInvoke(user, ref shouldAllow);
            if (!shouldAllow)
            {
                EditorMessage.SendNoPermissionMessage(user);
                return StandardErrorCode.NoPermissions;
            }

            user.Control.Controller = controller;
            return StandardErrorCode.Success;
        }
        
        EditorMessage.SendNoPermissionMessage(user);
        return StandardErrorCode.NoPermissions;
    }
#endif

#if CLIENT
    public static Ray GetLocalLookRay()
    {
        if (EditorUser.User != null && EditorUser.User.Control.Aim != null)
            return new Ray(EditorUser.User.Control.Aim.position, EditorUser.User.Control.Aim.forward);

        return new Ray(MainCamera.instance.transform.position, MainCamera.instance.transform.forward);
    }
#endif
}

public delegate void UserControllerUpdateRequested(EditorUser user, ref bool shouldAllow);
public enum CameraController : byte
{
    None = 0,
    Player = 1,
    Editor = 2
}