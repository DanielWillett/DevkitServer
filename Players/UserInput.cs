using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Players.UI;
using SDG.NetPak;
#if CLIENT
using EditorUI = SDG.Unturned.EditorUI;
using HarmonyLib;
#endif

namespace DevkitServer.Players;

[EarlyTypeInit(-1)]
#if CLIENT
[HarmonyPatch]
#endif
public class UserInput : MonoBehaviour
{
    public const ushort SaveDataVersion = 4;
    public const ushort SendDataVersion = 0;
    [UsedImplicitly]
    private static readonly NetCall<ulong, Vector3> SendInitialPosition = new NetCall<ulong, Vector3>(NetCalls.SendInitialPosition);
    [UsedImplicitly]
    private static readonly NetCall<ulong, CameraController> SendUpdateController = new NetCall<ulong, CameraController>(NetCalls.SendUpdateController);
    [UsedImplicitly]
    private static readonly NetCall<CameraController> RequestUpdateController = new NetCall<CameraController>(NetCalls.RequestUpdateController);
    [UsedImplicitly]
    private static readonly NetCall RequestInitialState = new NetCall(NetCalls.RequestInitialState);
#if CLIENT
    internal static CameraController CleaningUpController;
    public static CameraController LocalController => EditorUser.User?.Input is { } input ? input.Controller : 0;
#endif
    private CameraController _controller;
    private static Delegate[] _onUpdatedActions = Array.Empty<Delegate>();

    /*#if CLIENT
    private static StaticSetter<float>? SetLookPitch = Accessor.GenerateStaticSetter<EditorLook, float>("_pitch");
    private static StaticSetter<float>? SetLookYaw = Accessor.GenerateStaticSetter<EditorLook, float>("_yaw");
    #endif*/
    private static event Action<EditorUser>? _onUpdated;
    public static event Action<EditorUser>? OnUserPositionUpdated
    {
        add
        {
            _onUpdated += value;
            _onUpdatedActions = _onUpdated?.GetInvocationList() ?? Array.Empty<Delegate>();
        }
        remove
        {
            _onUpdated -= value;
            _onUpdatedActions = _onUpdated?.GetInvocationList() ?? Array.Empty<Delegate>();
        }
    }

    public static event Action<EditorUser>? OnUserControllerUpdated; 
    private bool _hasStopped = true;
#if CLIENT
    private float _lastFlush;
    private bool _bufferHasStop;
    private bool _nextPacketSendRotation;
    private int _rotSkip;
#endif
    private bool _networkedInitialPosition;
    private Vector3 _lastPos;
    private float _lastYaw;
    private float _lastPitch;
    private float _nextPacketApplyTime;
    public EditorUser User { get; internal set; } = null!;
    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public bool IsOwner { get; private set; }
    public GameObject? ControllerObject { get; private set; }
#if CLIENT
    /// <summary>Set with <see cref="RequestSetController"/>.</summary>
#elif SERVER
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

            switch (value)
            {
                case CameraController.Editor:
                    ControllerObject = User.EditorObject;
                    break;
                case CameraController.Player:
                    ControllerObject = User.Player!.player.gameObject;
                    break;
            }
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

    private static readonly Func<IDevkitTool>? GetDevkitTool;
    [UsedImplicitly]
    private static readonly InstanceGetter<EditorMovement, float> GetSpeed = Accessor.GenerateInstanceGetter<EditorMovement, float>("speed", BindingFlags.NonPublic, throwOnError: true)!;
    [UsedImplicitly]
    private static readonly InstanceGetter<EditorMovement, Vector3> GetInput = Accessor.GenerateInstanceGetter<EditorMovement, Vector3>("input", BindingFlags.NonPublic, throwOnError: true)!;
    [UsedImplicitly]
    private static readonly InstanceSetter<PlayerInput, float>? SetLastInputted = Accessor.GenerateInstanceSetter<PlayerInput, float>("lastInputed");
    [UsedImplicitly]
    private static readonly Action<PlayerUI> RestartPlayerUI = Accessor.GenerateInstanceCaller<PlayerUI, Action<PlayerUI>>("InitializePlayer", throwOnError: true)!;
#if SERVER
    private static readonly Action<Player, SteamPlayer> SendInitialState = Accessor.GenerateInstanceCaller<Player, Action<Player, SteamPlayer>>("SendInitialPlayerState", new Type[] { typeof(SteamPlayer) }, throwOnError: true)!;
#endif
#if CLIENT
    private EditorMovement? _movement;
    private static readonly ByteWriter Writer = new ByteWriter(false, 245);
    private Transform? _playerUiObject;
    private Transform? _editorUiObject;
    private UserInputPacket _lastPacket;
#endif
    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private readonly Queue<UserInputPacket> _packets = new Queue<UserInputPacket>();

    public static IDevkitTool? ActiveTool => GetDevkitTool?.Invoke();

    static UserInput()
    {
        Type? type = Accessor.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract");
        if (type == null)
        {
            Logger.LogWarning("Unable to find type: SDG.Unturned.EditorInteract.");
            return;
        }
        FieldInfo? instanceField = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (instanceField == null || !instanceField.IsStatic || !type.IsAssignableFrom(instanceField.FieldType))
        {
            Logger.LogWarning("Unable to find field: EditorInteract.instance.");
            return;
        }
        FieldInfo? toolField = type.GetField("activeTool", BindingFlags.Instance | BindingFlags.NonPublic);
        if (toolField == null || toolField.IsStatic || !typeof(IDevkitTool).IsAssignableFrom(toolField.FieldType))
        {
            Logger.LogWarning("Unable to find field: EditorInteract.activeTool.");
            return;
        }
        const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
        DynamicMethod method = new DynamicMethod("get_instance", attr,
            CallingConventions.Standard, typeof(IDevkitTool),
            Array.Empty<Type>(), type, true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, instanceField);
        il.Emit(OpCodes.Ldfld, toolField);
        il.Emit(OpCodes.Ret);
        GetDevkitTool = (Func<IDevkitTool>)method.CreateDelegate(typeof(Func<IDevkitTool>));
    }

#if CLIENT
    [UsedImplicitly]
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendInitialPosition)]
    private static void ReceiveInitialPosition(MessageContext ctx, ulong player, Vector3 pos)
    {
        EditorUser? user = UserManager.FromId(player);
        if (user != null && user.Input != null)
        {
            UserInput input = user.Input;
            input.transform.position = pos;
            input._networkedInitialPosition = true;
            if (input.IsOwner)
                input._nextPacketSendRotation = true;
            Logger.LogInfo("Received initial transform " + user + ": " + pos + ".");
            ctx.Acknowledge(StandardErrorCode.Success);
            TryInvokeOnUserPositionUpdated(user);
        }
    }
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, NetCalls.RequestInitialState)]
    private static void ReceiveRequestInitialState(MessageContext ctx)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline || user.Player?.player == null)
            return;
        SendInitialState(user.Player.player, user.Player);
    }
#endif
    private static void TryInvokeOnUserPositionUpdated(EditorUser user)
    {
        for (int i = 0; i < _onUpdatedActions.Length; ++i)
        {
            Action<EditorUser> action = (Action<EditorUser>)_onUpdatedActions[i];
            try
            {
                action(user);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(EditorUser) + "." + nameof(OnUserPositionUpdated) + ".");
                Logger.LogError(ex);
            }
        }
    }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.LogError("Invalid UserInput setup; EditorUser not found!");
            return;
        }

#if CLIENT
        IsOwner = User == EditorUser.User;
        if (IsOwner)
        {
            if (!User.EditorObject.TryGetComponent(out _movement))
            {
                Destroy(this);
                Logger.LogError("Invalid UserInput setup; EditorMovement not found!");
                return;
            }
            PlayerUI? plUi = User.gameObject.GetComponentInChildren<PlayerUI>();
            if (plUi != null)
                _playerUiObject = plUi.transform;
            EditorUI? edUi = User.EditorObject.GetComponentInChildren<EditorUI>();
            if (edUi != null)
                _editorUiObject = edUi.transform;
        }
        Controller = CameraController.Editor;
#endif

        _nextPacketApplyTime = CachedTime.RealtimeSinceStartup;
        TryInvokeOnUserPositionUpdated(User);
        
#if SERVER
        _controller = CameraController.Editor;
        ControllerObject = User.EditorObject;
        Load();
        SaveManager.onPostSave += Save;
        for (int i = 0; i < UserManager.Users.Count; ++i)
        {
            UserInput inp = UserManager.Users[i].Input;
            if (inp != null && inp != this)
                SendInitialPosition.Invoke(User.Connection, inp.User.SteamId.m_SteamID, inp._lastPos);
        }
#endif

        Logger.LogDebug("User input module created for " + User.SteamId.m_SteamID.Format() + " ( owner: " + IsOwner.Format() + " ).");
    }
    private void HandleControllerUpdated()
    {
        User.Player!.player.gameObject.SetActive(true);
        User.EditorObject.SetActive(true);
        if (Controller == CameraController.Editor)
        {
            // on controller set to editor
#if SERVER
            SetEditorPosition(User.Player!.player.look.aim.transform.position);
            if (User.Player.player.life.isDead)
                User.Player.player.life.ReceiveRespawnRequest(false);
            else
                User.Player.player.life.sendRevive(); // heal the player
#endif
            User.Player.player.movement.canAddSimulationResultsToUpdates = false;
            User.Player!.player.gameObject.SetActive(false);
        }
        else if (Controller == CameraController.Player)
        {
            // on controller set to player
            User.Player!.player.gameObject.SetActive(true);
            User.Player.player.movement.canAddSimulationResultsToUpdates = true;
#if SERVER
            Vector3 position = User.EditorObject.transform.position;
            float yaw = User.EditorObject.transform.rotation.eulerAngles.y;
            if (Physics.Raycast(new Ray(position with { y = Level.HEIGHT }, Vector3.down), out RaycastHit info, Level.HEIGHT, RayMasks.BLOCK_COLLISION, QueryTriggerInteraction.Ignore))
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
                Logger.LogDebug($"Unable to set main camera: {ctrl.Format()}.");
                return;
            }
            if (ctrl == User.EditorObject)
            {
                ChangeUI(true);
            }
            else if (ctrl == User.Player!.player.gameObject)
            {
                ChangeUI(false);
            }
            Logger.LogInfo($"Camera controller set to {Controller.Format()}.", ConsoleColor.DarkCyan);
        }
#endif
        OnUserControllerUpdated?.Invoke(User);
    }
#if CLIENT
    private void ChangeUI(bool editor)
    {
        Component? ui = !editor ? UIAccessTools.EditorUI : UIAccessTools.PlayerUI;
        if (editor)
        {
            GameObject? editorObj = DevkitServerUtility.GetEditorObject();
            if (editorObj != null && editorObj)
                editorObj.SetActive(true);
            CleaningUpController = CameraController.Editor;
        }
        if (ui != null)
        {
            if (!editor)
                _editorUiObject = ui.transform;
            else
                _playerUiObject = ui.transform;
            Destroy(ui);
            Logger.LogInfo("Cleaned up " + (!editor ? "EditorUI." : "PlayerUI."));
        }
        else
        {
            Logger.LogWarning((editor ? "EditorUI" : "PlayerUI") + " not available to clean up.");
        }
        Transform? parent = editor ? _editorUiObject : _playerUiObject;
        if (parent == null)
        {
            Logger.LogWarning("Failed to find parent of " + (editor ? "EditorUI." : "PlayerUI."));
            return;
        }
        StartCoroutine(SwitchToUI(parent.gameObject, editor)); // need to wait for OnDestroy to run
    }
    private static IEnumerator SwitchToUI(GameObject parent, bool editor)
    {
        try
        {
            yield return null;
            while (!Level.isLoaded)
                yield return null;
            if (parent == null) // check parent wasn't destroyed
                yield break;

            Component comp = parent.gameObject.AddComponent(editor ? typeof(EditorUI) : typeof(PlayerUI));
            yield return null;
            if (!editor)
                CleaningUpController = CameraController.Player;
            Logger.LogInfo("Added " + (editor ? "EditorUI." : "PlayerUI."));

            if (comp is PlayerUI player) // loaded
            {
                try
                {
                    RestartPlayerUI(player);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error initializing character.");
                    Logger.LogError(ex);
                }
                Logger.LogInfo("Restarted PlayerUI.");
            }
            else if (editor)
            {
                try
                {
                    VehicleManager.exitVehicle();
                    Player.player.equipment.ReceiveEquip(byte.MaxValue, byte.MaxValue, byte.MaxValue, Guid.Empty, 100, Array.Empty<byte>(), NetId.INVALID);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error deinitializing character.");
                    Logger.LogError(ex);
                }
            }
            if (Player.player.first != null)
            {
                Player.player.first.gameObject.SetActive(!editor);
            }
        }
        finally
        {
            if (!editor)
            {
                GameObject? editorObj = DevkitServerUtility.GetEditorObject();
                if (editorObj != null && editorObj)
                    editorObj.SetActive(false);
                RequestInitialState.Invoke();
            }
        }
    }
    [HarmonyPatch(typeof(PlayerLook), "updateScope")]
    [HarmonyFinalizer]
    [UsedImplicitly]
    private static void PlayerLookUpdateScopeFinalizer(ref Exception __exception)
    {
        if (__exception != null)
        {
            Logger.LogError("Error updating player look scope settings.");
            Logger.LogError(__exception);
            __exception = null!;
        }
    }

    private static bool SetActiveMainCamera(Transform tranform)
    {
        Transform? child = tranform.FindChildRecursive("Camera");
        if (child == null)
        {
            Logger.LogWarning("Failed to find 'Camera' child.");
            return false;
        }

        MainCamera? cameraObj = child.GetComponentInChildren<MainCamera>();
        if (cameraObj == null)
        {
            Logger.LogWarning("Failed to find MainCamera component.");
            return false;
        }
        
        if (!cameraObj.transform.TryGetComponent(out Camera camera))
        {
            Logger.LogWarning("Failed to find Camera component on MainCamera object.");
            return false;
        }

        if (MainCamera.instance == camera)
        {
            Logger.LogDebug("Camera already set to the correct instance.");
            return true;
        }
        camera.enabled = true;
        Logger.LogDebug("Camera enabled: " + camera.gameObject.name + ".");

        Camera oldCamera = MainCamera.instance;
        if (oldCamera != null)
        {
            oldCamera.enabled = false;
            Logger.LogDebug("Camera disabled: " + oldCamera.gameObject.name + ".");
        }

        try
        {
            cameraObj.Awake();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to set camera instance to \"" + tranform.gameObject.name + "\".");
            Logger.LogError(ex);
        }

        return false;
    }
#endif

    internal static void ReceiveMovementRelay(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.LogError("Failed to read incoming movement packet length.");
            return;
        }
        NetFactory.IncrementByteCount(false, DevkitMessage.MovementRelay, len + sizeof(ushort));

#if SERVER
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.LogError("Failed to find user for movement packet from transport connection: " + transportConnection.Format() + ".");
            return;
        }
#endif
        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.LogError("Failed to read movement packet.");
            return;
        }
        Reader.LoadNew(buffer);
        Reader.Skip(offset);
#if CLIENT
        ulong s64 = Reader.ReadUInt64();
        EditorUser? user = UserManager.FromId(s64);
        if (user == null)
        {
            Logger.LogError("Failed to find user for movement packet from a steam id: " + s64.Format() + ".");
            return;
        }
#endif
        user.Input.HandleReadPackets(Reader);

#if SERVER
        if (Provider.clients.Count > 1)
        {
            byte[] sendBytes = new byte[sizeof(ulong) + len];
            Buffer.BlockCopy(buffer, offset, sendBytes, sizeof(ulong), len);
            UnsafeBitConverter.GetBytes(sendBytes, user.SteamId.m_SteamID);
            IList<ITransportConnection> list = NetFactory.GetPooledTransportConnectionList(Provider.clients.Count - 1);
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                SteamPlayer pl = Provider.clients[i];
                if (pl.playerID.steamID.m_SteamID != user.SteamId.m_SteamID)
                    list.Add(pl.transportConnection);
            }

            NetFactory.SendGeneric(DevkitMessage.MovementRelay, sendBytes, list, reliable: false);
        }
#endif
    }
#if CLIENT
    private void FlushPackets()
    {
        if (_packets.Count < 1) return;
        HandleFlushPackets(Writer);
        int len = Writer.Count;
        NetFactory.SendGeneric(DevkitMessage.MovementRelay, Writer.FinishWrite(), 0, len, _bufferHasStop);
        _bufferHasStop = false;
    }
    private void HandleFlushPackets(ByteWriter writer)
    {
        int c = Math.Min(byte.MaxValue, _packets.Count);
        writer.Write(SendDataVersion);
        writer.Write((byte)c);
        for (int i = 0; i < c; ++i)
        {
            UserInputPacket p = _packets.Dequeue();
            p.Write(writer);
        }
    }
#endif
    private void HandleReadPackets(ByteReader reader)
    {
        _ = reader.ReadUInt16(); // UserInput.SendDataVersion
        byte c = reader.ReadUInt8();
        for (int i = 0; i < c; ++i)
        {
            UserInputPacket p = new UserInputPacket();
            p.Read(reader);
            _packets.Enqueue(p);
        }
        if (User.Player != null && User.Player.player != null)
            SetLastInputted?.Invoke(User.Player.player.input, CachedTime.RealtimeSinceStartup);
        float time = CachedTime.RealtimeSinceStartup;
        if (time > _nextPacketApplyTime)
            _nextPacketApplyTime = time;
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        Logger.LogDebug("EditorInput destroyed.");
#if SERVER
        SaveManager.onPostSave -= Save;
#endif
        User = null!;
    }
#if CLIENT
    [UsedImplicitly]
    private void LateUpdate()
    {
        if (IsOwner)
        {
            if (!User.IsOnline)
            {
                Destroy(this);
                return;
            }
            float t = CachedTime.RealtimeSinceStartup;
            if (!_networkedInitialPosition || _movement == null)
                return;
            Vector3 pos = transform.position;
            float yaw = EditorLook.yaw % 360f;
            if (yaw < 0)
                yaw += 360;
            float pitch = EditorLook.pitch;
            bool posDiff = pos != _lastPos;
            bool rotDiff = pitch != _lastPitch || yaw != _lastYaw;
            if (posDiff || rotDiff || (_nextPacketSendRotation && _hasStopped))
            {
                TryInvokeOnUserPositionUpdated(User);
                _hasStopped = false;
                Flags flags = Flags.None;
                if (posDiff)
                    flags |= Flags.Position;
                if (rotDiff || _nextPacketSendRotation)
                {
                    flags |= Flags.Rotation;
                    _nextPacketSendRotation = false;
                }
                if (!posDiff && !_nextPacketSendRotation)
                {
                    ++_rotSkip;
                    if (_rotSkip % PlayerInput.SAMPLES != 1)
                        return;
                }
                _lastPacket = new UserInputPacket
                {
                    Pitch = _lastPitch = pitch,
                    Yaw = _lastYaw = yaw,
                    Position = _lastPos = pos,
                    Flags = flags,
                    Speed = GetSpeed(_movement),
                    Input = GetInput(_movement) with
                    {
                        y = InputEx.GetKey(ControlsSettings.ascend)
                            ? 1f
                            : (InputEx.GetKey(ControlsSettings.descend) ? -1f : 0f)
                    },
                    DeltaTime = CachedTime.DeltaTime
                };
                _packets.Enqueue(_lastPacket);
            }
            else if (!_hasStopped)
            {
                _hasStopped = true;
                _lastPacket = new UserInputPacket
                {
                    Flags = Flags.StopMsg,
                    DeltaTime = CachedTime.DeltaTime,
                    Yaw = _lastYaw = yaw,
                    Pitch = _lastPitch = pitch,
                    Position = _lastPos = pos
                };
                _packets.Enqueue(_lastPacket);
                _bufferHasStop = true;
            }

            if (t - _lastFlush > Time.fixedDeltaTime * 16)
            {
                FlushPackets();
                _lastFlush = t;
            }
        }
    }
#endif
    [UsedImplicitly]
    private void Update()
    {
#if SERVER
        if (Controller == CameraController.Player && User.Player?.player != null)
            _lastPos = User.Player.player.transform.position;
#endif
        if (IsOwner) return;
        if (!User.IsOnline)
        {
            Destroy(this);
            return;
        }

        float t = CachedTime.RealtimeSinceStartup;
        while (_packets is { Count: > 0 } && t >= _nextPacketApplyTime)
        {
            UserInputPacket packet = _packets.Dequeue();
            if (_networkedInitialPosition)
            {
                _nextPacketApplyTime += packet.DeltaTime;
                if ((packet.Flags & Flags.StopMsg) == 0)
                {
                    if (_hasStopped)
                        _nextPacketApplyTime = t + packet.DeltaTime;
                    _hasStopped = false;
                    ApplyPacket(in packet);
                }
                else
                {
                    _nextPacketApplyTime = t + packet.DeltaTime;
                    _hasStopped = true;
                    transform.SetPositionAndRotation(packet.Position, Quaternion.Euler(packet.Pitch, packet.Yaw, 0f));
                    _lastPos = packet.Position;
                    _lastPitch = packet.Pitch;
                    _lastYaw = packet.Yaw;
#if CLIENT
                    _lastPacket = packet;
#endif
                    TryInvokeOnUserPositionUpdated(User);
                }
            }
        }
    }
    private void ApplyPacket(in UserInputPacket packet)
    {
        float dt = packet.DeltaTime;
        Quaternion rot = (packet.Flags & Flags.Rotation) != 0 ? Quaternion.Euler(packet.Pitch, packet.Yaw, 0f) : transform.rotation;
        Vector3 pos = transform.position + rot *
                                              packet.Input with { y = 0 } *
                                              packet.Speed *
                                              dt
                                              +
                                              Vector3.up *
                                              packet.Input.y *
                                              dt *
                                              packet.Speed;
        if ((packet.Flags & Flags.Position) != 0)
            pos = Vector3.Lerp(pos, packet.Position, 1f / (_packets.Count + 1) * ((pos - packet.Position).sqrMagnitude / 16f));
        
        if ((packet.Flags & Flags.Rotation) != 0)
            transform.SetPositionAndRotation(pos, rot);
        else
            transform.position = pos;
        _lastPos = pos;
        TryInvokeOnUserPositionUpdated(User);
#if CLIENT
        _lastPacket = packet;
#endif
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendUpdateController)]
    private static void ReceiveControllerUpdated(MessageContext ctx, ulong steam64, CameraController controller)
    {
        EditorUser? user = UserManager.FromId(steam64);
        if (user != null)
            user.Input.Controller = controller;

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
        Permission perm = controller == CameraController.Player
            ? VanillaPermissions.UsePlayerController
            : VanillaPermissions.UseEditorController;
        Permission otherPerm = controller != CameraController.Player
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
    [NetCall(NetCallSource.FromClient, NetCalls.RequestUpdateController)]
    private static StandardErrorCode ReceiveUpdateControllerRequest(MessageContext ctx, CameraController controller)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
            return StandardErrorCode.NotFound;

        if (CheckPermissionForController(controller, user.SteamId.m_SteamID))
        {
            user.Input.Controller = controller;
            return StandardErrorCode.Success;
        }
        
        UIMessage.SendNoPermissionMessage(user);
        return StandardErrorCode.NoPermissions;
    }
    private void Load()
    {
        if (User?.Player == null) return;
        _networkedInitialPosition = true;
        Vector3 pos;
        if (PlayerSavedata.fileExists(User.Player.playerID, "/DevkitServer/Input.dat"))
        {
            Block block = PlayerSavedata.readBlock(User.Player.playerID, "/DevkitServer/Input.dat", 0);
            ushort v = block.readUInt16();
            pos = block.readSingleVector3();
            if (v < 2)
            {
                block.readSingleQuaternion();
            }
            else if (v < 3)
            {
                block.readSingle();
                block.readSingle();
            }
            if (pos.IsFinite() && Mathf.Abs(pos.x) <= ushort.MaxValue && Mathf.Abs(pos.y) <= ushort.MaxValue)
            {
                _lastPitch = 0;
                _lastYaw = 0;
                if (v is > 0 and < 4)
                    block.readByte();

                Logger.LogDebug(" Loaded position: " + pos.Format());
                SetEditorPosition(pos);
                return;
            }
        }

        PlayerSpawnpoint spawn = LevelPlayers.getSpawn(false);
        pos = spawn.point + new Vector3(0.0f, 2f, 0.0f);
        Logger.LogDebug(" Loaded random position: " + pos.Format() + ", " + spawn.angle.Format() + "°.");
        SetEditorPosition(pos);

    }
    public void SetEditorPosition(Vector3 pos)
    {
        this.transform.position = pos;
#if CLIENT
        _lastPacket = new UserInputPacket
        {
            Position = pos,
            Pitch = 0,
            Yaw = 0,
            Flags = Flags.StopMsg,
            DeltaTime = CachedTime.DeltaTime,
            Input = Vector3.zero,
            Speed = 0f
        };
#endif
        _lastPos = pos;
#if SERVER
        SendInitialPosition.Invoke(Provider.GatherRemoteClientConnections(), User.SteamId.m_SteamID, pos);
#endif
    }
    public void Save()
    {
        if (!_networkedInitialPosition || User?.Player == null)
            return;
        Block block = new Block();
        block.writeUInt16(SaveDataVersion);
        block.writeSingleVector3(_lastPos);
        Logger.LogDebug(" Saved position: " + _lastPos.Format() + ", (" + _lastPitch.Format() + ", " + _lastYaw.Format() + ").");
        PlayerSavedata.writeBlock(User.Player.playerID, "/DevkitServer/Input.dat", block);
    }
#endif
    private struct UserInputPacket
    {
        public Vector3 Position { get; set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Speed { get; set; }
        public float DeltaTime { get; set; }
        public Flags Flags { get; set; }

        // Y = ascend
        public Vector3 Input { get; set; }

        public void Read(ByteReader reader)
        {
            Flags = reader.ReadEnum<Flags>();
            DeltaTime = reader.ReadFloat();
            if ((Flags & Flags.StopMsg) == 0)
            {
                if ((Flags & Flags.Position) != 0)
                {
                    Speed = reader.ReadFloat();
                    byte inputFlag = reader.ReadUInt8();
                    Input = new Vector3((sbyte)((byte)((inputFlag & 0b00001100) >> 2) - 1),
                        (sbyte)((byte)(inputFlag & 0b00000011) - 1),
                        (sbyte)((byte)((inputFlag & 0b00110000) >> 4) - 1));
                    Position = reader.ReadVector3();
                }

                if ((Flags & Flags.Rotation) != 0)
                {
                    Pitch = reader.ReadInt8() / 1.4f;
                    Yaw = reader.ReadUInt8() * 1.5f;
                }
            }
            else
            {
                Position = reader.ReadVector3();
                Pitch = reader.ReadFloat();
                Yaw = reader.ReadFloat();
            }
        }

#if CLIENT
        public void Write(ByteWriter writer)
        {
            writer.Write(Flags);
            writer.Write(DeltaTime);
            if ((Flags & Flags.StopMsg) == 0)
            {
                if ((Flags & Flags.Position) != 0)
                {
                    writer.Write(Speed);
                    byte inputFlag = (byte)((byte)(Mathf.Clamp(Input.y, -1, 1) + 1) |
                                            (byte)((byte)(Mathf.Clamp(Input.x, -1, 1) + 1) << 2) |
                                            (byte)((byte)(Mathf.Clamp(Input.z, -1, 1) + 1) << 4));
                    writer.Write(inputFlag);
                    writer.Write(Position);
                }
                    
                if ((Flags & Flags.Rotation) != 0)
                {
                    writer.Write((sbyte)Mathf.Clamp(Pitch * 1.4f, sbyte.MinValue, sbyte.MaxValue));
                    writer.Write((byte)Mathf.Clamp(Yaw / 1.5f, byte.MinValue, byte.MaxValue));
                }
            }
            else
            {
                writer.Write(Position);
                writer.Write(Pitch);
                writer.Write(Yaw);
            }
        }
#endif

        public override string ToString()
        {
            if ((Flags & Flags.StopMsg) != 0)
                return $"Stop at {Position:F2} ({Pitch:F2}°, {Yaw:F2}°, 0°)";

            string str = "Input";
            if ((Flags & Flags.Position) != 0)
                str += $" at {Position:F2}";
            if ((Flags & Flags.Rotation) != 0)
                str += $" ({Pitch:F2}°, {Yaw:F2}°, 0°)";
            if (str.Length == 5)
                str += $" Speed: {Speed:F2} Dir: {Input:F2}";
            return str;
        }
    }
    [Flags]
    public enum Flags : byte
    {
        None = 1,
        StopMsg = 2,
        Position = 4,
        Rotation = 8
    }
}

public enum CameraController : byte
{
    Player = 1,
    Editor
}