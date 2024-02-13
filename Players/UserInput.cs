using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using SDG.NetPak;
#if CLIENT
using DevkitServer.API.Abstractions;
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
public class UserInput : MonoBehaviour
{
    private const string Source = "INPUT";
    private static readonly CachedMulticastEvent<Action<EditorUser>> EventOnUserEditorPositionUpdated = new CachedMulticastEvent<Action<EditorUser>>(typeof(UserInput), nameof(OnUserEditorPositionUpdated));
    private static readonly CachedMulticastEvent<Action<EditorUser>> EventOnUserControllerUpdated = new CachedMulticastEvent<Action<EditorUser>>(typeof(UserInput), nameof(OnUserControllerUpdated));
#if SERVER
    private static readonly CachedMulticastEvent<UserControllerUpdateRequested> EventOnUserControllerUpdateRequested = new CachedMulticastEvent<UserControllerUpdateRequested>(typeof(UserInput), nameof(OnUserControllerUpdateRequested));
#endif
    public const ushort SaveDataVersion = 5;
    public const ushort SendDataVersion = 0;
    [UsedImplicitly]
    private static readonly NetCall<ulong, Vector3, Vector3> SendTransform = new NetCall<ulong, Vector3, Vector3>(DevkitServerNetCall.SendTransform);
    [UsedImplicitly]
    private static readonly NetCall<ulong, CameraController> SendUpdateController = new NetCall<ulong, CameraController>(DevkitServerNetCall.SendUpdateController);
    [UsedImplicitly]
    private static readonly NetCall<CameraController> RequestUpdateController = new NetCall<CameraController>(DevkitServerNetCall.RequestUpdateController);
    [UsedImplicitly]
    private static readonly NetCall RequestInitialState = new NetCall(DevkitServerNetCall.RequestInitialState);
#if CLIENT
    private static readonly StaticSetter<float>? SetPitch = Accessor.GenerateStaticSetter<EditorLook, float>("_pitch");
    private static readonly StaticSetter<float>? SetYaw = Accessor.GenerateStaticSetter<EditorLook, float>("_yaw");
    // internal static CameraController CleaningUpController;
    internal static MethodInfo GetLocalControllerMethod = typeof(UserInput).GetProperty(nameof(LocalController), BindingFlags.Static | BindingFlags.Public)?.GetMethod!;
    public static CameraController LocalController
    {
        get
        {
            if (DevkitServerModule.IsEditing && EditorUser.User?.Input is not null)
                return EditorUser.User.Input.Controller;

            return Level.isEditor ? CameraController.Editor : (Level.isLoaded || Level.isLoading ? CameraController.Player : CameraController.None);
        }
    }
    public static Transform LocalAim
    {
        get
        {
            if (DevkitServerModule.IsEditing && EditorUser.User?.Input is not null && EditorUser.User.Input.Aim != null)
                return EditorUser.User.Input.Aim;

            return Level.isEditor || !Level.isLoaded ? MainCamera.instance.transform : Player.player.look.aim;
        }
    }
#endif
    private CameraController _controller;

    /*#if CLIENT
    private static StaticSetter<float>? SetLookPitch = Accessor.GenerateStaticSetter<EditorLook, float>("_pitch");
    private static StaticSetter<float>? SetLookYaw = Accessor.GenerateStaticSetter<EditorLook, float>("_yaw");
    #endif*/
    public static event Action<EditorUser> OnUserEditorPositionUpdated
    {
        add => EventOnUserEditorPositionUpdated.Add(value);
        remove => EventOnUserEditorPositionUpdated.Remove(value);
    }

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
    private bool _hasStopped = true;
#if CLIENT
    private float _lastFlush;
    private float _lastQueue;
    private bool _bufferHasStop;
    private bool _nextPacketSendRotation;
    private bool _nextPacketSendStop;
    private int _rotSkip;
#endif
    private UserInputPacket _pendingPacket;
    private float _pendingPacketTime;
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

    [UsedImplicitly]
    private static readonly InstanceGetter<EditorMovement, float> GetSpeed = Accessor.GenerateInstanceGetter<EditorMovement, float>("speed", throwOnError: true)!;
    [UsedImplicitly]
    private static readonly InstanceGetter<EditorMovement, Vector3> GetInput = Accessor.GenerateInstanceGetter<EditorMovement, Vector3>("input", throwOnError: true)!;
    [UsedImplicitly]
    private static readonly InstanceSetter<PlayerInput, float>? SetLastInputted = Accessor.GenerateInstanceSetter<PlayerInput, float>("lastInputed");
    [UsedImplicitly]
    private static readonly Action<PlayerUI> RestartPlayerUI = Accessor.GenerateInstanceCaller<PlayerUI, Action<PlayerUI>>("InitializePlayer", throwOnError: true, allowUnsafeTypeBinding: true)!;
#if SERVER
    private static readonly Action<Player, SteamPlayer> SendInitialState = Accessor.GenerateInstanceCaller<Player, Action<Player, SteamPlayer>>("SendInitialPlayerState", throwOnError: true, allowUnsafeTypeBinding: true)!;
#endif
#if CLIENT
    private EditorMovement? _movement;
    private static readonly ByteWriter Writer = new ByteWriter(false, 245);
    private Transform? _playerUiObject;
    private Transform? _editorUiObject;
    private UserInputPacket _lastPacket;
#endif
    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private readonly List<UserInputPacket> _packets = new List<UserInputPacket>();
    private int _packetsIndex;
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
    static UserInput()
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
        _lastFlush = CachedTime.RealtimeSinceStartup;
        IsOwner = User == EditorUser.User;
        if (IsOwner)
        {
            if (!User.EditorObject.TryGetComponent(out _movement))
            {
                Destroy(this);
                Logger.DevkitServer.LogError(Source, "Invalid UserInput setup; EditorMovement not found!");
                return;
            }
            PlayerUI? plUi = User.gameObject.GetComponentInChildren<PlayerUI>();
            if (plUi != null)
                _playerUiObject = plUi.transform;
            EditorUI? edUi = User.EditorObject.GetComponentInChildren<EditorUI>();
            if (edUi != null)
                _editorUiObject = edUi.transform;
            _controller = CameraController.Editor;
            ControllerObject = User.EditorObject;
            SetActiveMainCamera(User.EditorObject.transform);
        }
        else Controller = CameraController.Editor;
#endif
        _nextPacketApplyTime = CachedTime.RealtimeSinceStartup;
        EventOnUserEditorPositionUpdated.TryInvoke(User);
        
#if SERVER
        _controller = CameraController.Editor;
        ControllerObject = User.EditorObject;
        Load();
        SaveManager.onPostSave += Save;
        for (int i = 0; i < UserManager.Users.Count; ++i)
        {
            UserInput inp = UserManager.Users[i].Input;
            if (inp != null && inp != this)
                SendTransform.Invoke(User.Connection, inp.User.SteamId.m_SteamID, inp._lastPos, new Vector3(inp._lastPitch, inp._lastYaw, 0f));
        }
#endif

        Logger.DevkitServer.LogDebug(Source, $"User input module created for {User.SteamId.m_SteamID.Format()} ( owner: {IsOwner.Format()} ).");
    }

#if CLIENT
    [UsedImplicitly]
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendTransform)]
    private static void ReceiveTransform(MessageContext ctx, ulong player, Vector3 pos, Vector3 eulerRotation)
    {
        EditorUser? user = UserManager.FromId(player);
        if (user == null || user.Input == null)
            return;

        UserInput input = user.Input;
        input._networkedInitialPosition = true;
        if (input.IsOwner)
        {
            input.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, eulerRotation.y, 0f));
            MainCamera.instance.transform.localRotation = Quaternion.Euler(eulerRotation.x, 0f, 0f);
            input._nextPacketSendStop = true;
            input._hasStopped = false;
            SetPitch?.Invoke(Mathf.Clamp(eulerRotation.x > 90f ? eulerRotation.x - 360f : eulerRotation.x, -90f, 90f));
            SetYaw?.Invoke(eulerRotation.y);
        }
        else
        {
            input.transform.SetPositionAndRotation(pos, Quaternion.Euler(eulerRotation));
        }
        Logger.DevkitServer.LogDebug(Source, $"Received initial transform {user.Format()}: {pos.Format()}, {eulerRotation.Format()}.");
        ctx.Acknowledge(StandardErrorCode.Success);
        EventOnUserEditorPositionUpdated.TryInvoke(user);
    }
#endif
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
            SetEditorPosition(User.Player!.player.look.aim.transform.position, User.Player!.player.look.aim.rotation.eulerAngles);
            if (User.Player.player.life.isDead)
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
#endif

    internal static void ReceiveMovementRelay(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.DevkitServer.LogError(Source, "Failed to read incoming movement packet length.");
            return;
        }
        NetFactory.IncrementByteCount(DevkitServerMessage.MovementRelay, false, len + sizeof(ushort));

#if SERVER
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.DevkitServer.LogError(Source, "Failed to find user for movement packet from transport connection: " + transportConnection.Format() + ".");
            return;
        }
#endif
        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.DevkitServer.LogError(Source, "Failed to read movement packet.");
            return;
        }
        Reader.LoadNew(new ArraySegment<byte>(buffer, offset, len));
#if CLIENT
        ulong s64 = Reader.ReadUInt64();
        EditorUser? user = UserManager.FromId(s64);
        if (user == null)
        {
            Logger.DevkitServer.LogError(Source, $"Failed to find user for movement packet from a steam id: {s64.Format()}.");
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
            PooledTransportConnectionList list = NetFactory.GetPooledTransportConnectionList(Provider.clients.Count - 1);
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                SteamPlayer pl = Provider.clients[i];
                if (pl.playerID.steamID.m_SteamID != user.SteamId.m_SteamID)
                    list.Add(pl.transportConnection);
            }

            NetFactory.SendGeneric(DevkitServerMessage.MovementRelay, sendBytes, list, reliable: false);
        }
#endif
    }
#if CLIENT
    private void FlushPackets()
    {
        if (_packets.Count < 1) return;
        HandleFlushPackets(Writer);
        //NetFactory.SendGeneric(DevkitServerMessage.MovementRelay, Writer.ToArraySegmentAndDontFlush(), _bufferHasStop);
        Writer.Flush();
        _bufferHasStop = false;
    }
    private void HandleFlushPackets(ByteWriter writer)
    {
        int c = Math.Min(byte.MaxValue, _packets.Count);
        writer.Write(SendDataVersion);
        writer.Write((byte)c);
        for (int i = 0; i < c; ++i)
        {
            UserInputPacket p = _packets[_packetsIndex];
            ++_packetsIndex;

            if (_packets.Count <= _packetsIndex)
            {
                _packets.Clear();
                _packetsIndex = 0;
            }
            p.Write(writer);
        }
    }
#endif
    private void HandleReadPackets(ByteReader reader)
    {
        ushort version = reader.ReadUInt16();
        byte c = reader.ReadUInt8();
        for (int i = 0; i < c; ++i)
        {
            UserInputPacket p = new UserInputPacket();
            p.Read(reader, version);
            _packets.Add(p);
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
        Logger.DevkitServer.LogDebug(Source, "EditorInput destroyed.");
#if SERVER
        SaveManager.onPostSave -= Save;
#endif
        User = null!;
    }
    [UsedImplicitly]
    private void Update()
    {
        float t = CachedTime.RealtimeSinceStartup;
#if CLIENT
        if (IsOwner)
        {
            if (!User.IsOnline)
            {
                Destroy(this);
                return;
            }
            if (!_networkedInitialPosition || _movement == null)
                return;
            Vector3 pos = transform.position;
            float yaw = EditorLook.yaw % 360f;
            if (yaw < 0)
                yaw += 360;
            float pitch = EditorLook.pitch;
            bool posDiff = pos != _lastPos;
            bool rotDiff = pitch != _lastPitch || yaw != _lastYaw;
            if (!_nextPacketSendStop && (posDiff || rotDiff || (_nextPacketSendRotation && _hasStopped)))
            {
                EventOnUserEditorPositionUpdated.TryInvoke(User);
                _hasStopped = false;
                Flags flags = Flags.None;
                if (posDiff)
                    flags |= Flags.Position;
                if (rotDiff || _nextPacketSendRotation)
                {
                    flags |= Flags.Rotation;
                    _nextPacketSendRotation = false;
                }

                if (_lastPacket.Flags == 0 || (_lastPacket.Flags & Flags.StopMsg) != 0)
                    flags |= Flags.HasTimeSinceLastQueue;

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
                    TimeSinceLastQueue = t - Mathf.Max(_lastQueue, _lastFlush),
                    Speed = GetSpeed(_movement),
                    Input = GetInput(_movement) with
                    {
                        y = InputEx.GetKey(ControlsSettings.ascend)
                            ? 1f
                            : (InputEx.GetKey(ControlsSettings.descend) ? -1f : 0f)
                    },
                    DeltaTime = CachedTime.DeltaTime
                };
                _packets.Add(_lastPacket);
                _lastQueue = t;
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
                _lastQueue = t;
                _packets.Add(_lastPacket);
                _bufferHasStop = true;
            }

            if (t - _lastFlush > Time.fixedDeltaTime * 16)
            {
                FlushPackets();
                _lastFlush = t;
            }
        }
#endif
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

        if ((_pendingPacket.Flags & Flags.HasTimeSinceLastQueue) != 0)
        {
            // this queues the first packet at the end of the current packet instead of the beginning
            // to remove that stutter from starting at the beginning, waiting, then continuing with full packets

            if (t >= _pendingPacketTime)
            {
                ApplyPacket(ref _pendingPacket);
                _pendingPacket = default;
                _pendingPacketTime = 0f;
            }
        }
        else
        {
            while (_packets is { Count: > 0 } && t >= _nextPacketApplyTime)
            {
                UserInputPacket packet = _packets[_packetsIndex];
                ++_packetsIndex;

                if (_packets.Count <= _packetsIndex)
                {
                    _packets.Clear();
                    _packetsIndex = 0;
                }

                if (_networkedInitialPosition)
                {
                    if ((packet.Flags & Flags.HasTimeSinceLastQueue) != 0 && packet.TimeSinceLastQueue > 0f)
                    {
                        _pendingPacket = packet;
                        _pendingPacketTime = t + packet.TimeSinceLastQueue + 0.25f;
                    }
                    else
                    {
                        _nextPacketApplyTime += packet.DeltaTime;
                        ApplyPacket(ref packet);
                    }
                }
            }
        }
    }
    private void ApplyPacket(ref UserInputPacket packet)
    {
        float t = CachedTime.RealtimeSinceStartup;

        if ((packet.Flags & Flags.StopMsg) == 0)
        {
            if (_hasStopped)
                _nextPacketApplyTime = t + packet.DeltaTime;
            _hasStopped = false;
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
            EventOnUserEditorPositionUpdated.TryInvoke(User);
        }
        else
        {
            _nextPacketApplyTime = t + packet.DeltaTime;
            _hasStopped = true;
            transform.SetPositionAndRotation(packet.Position, Quaternion.Euler(packet.Pitch, packet.Yaw, 0f));
            _lastPos = packet.Position;
            _lastPitch = packet.Pitch;
            _lastYaw = packet.Yaw;
            EventOnUserEditorPositionUpdated.TryInvoke(User);
        }
#if CLIENT
        _lastPacket = packet;
#endif
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendUpdateController)]
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

            user.Input.Controller = controller;
            return StandardErrorCode.Success;
        }
        
        EditorMessage.SendNoPermissionMessage(user);
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
                _lastPitch = 0;
                _lastYaw = 0;
            }
            else if (v is < 3 or > 4)
            {
                _lastPitch = block.readSingle();
                _lastYaw = block.readSingle();
            }
            if (pos.IsFinite() && Mathf.Abs(pos.x) <= ushort.MaxValue && Mathf.Abs(pos.y) <= ushort.MaxValue)
            {
                if (v is > 0 and < 4)
                    block.readByte();

                Logger.DevkitServer.LogDebug(Source, $" Loaded position: {pos.Format()}, rotation: {_lastYaw.Format()}, {_lastPitch.Format()}.");
                SetEditorPosition(pos, new Vector3(_lastYaw, _lastPitch, 0f));
                return;
            }
        }

        PlayerSpawnpoint spawn = LevelPlayers.getSpawn(false);
        pos = spawn.point + new Vector3(0.0f, 2f, 0.0f);
        Logger.DevkitServer.LogDebug(Source, $" Loaded random position: {pos.Format()}, {spawn.angle.Format()}°.");
        SetEditorPosition(pos, new Vector3(0f, spawn.angle, 0f));

    }
    public void SetEditorPosition(Vector3 pos, Vector3 eulerRotation)
    {
        this.transform.position = pos;
        _lastPos = pos;
#if SERVER
        SendTransform.Invoke(Provider.GatherRemoteClientConnections(), User.SteamId.m_SteamID, pos, eulerRotation);
#endif
    }
    public void Save()
    {
        if (!_networkedInitialPosition || User?.Player == null)
            return;
        Block block = new Block();
        block.writeUInt16(SaveDataVersion);
        block.writeSingleVector3(_lastPos);
        Logger.DevkitServer.LogDebug(Source, $" Saved position: {_lastPos.Format()}, ({_lastPitch.Format()}, {_lastYaw.Format()}).");
        PlayerSavedata.writeBlock(User.Player.playerID, "/DevkitServer/Input.dat", block);
    }
#endif

#if CLIENT
    /// <summary>
    /// Teleports the local editor's camera to the specified position and rotation
    /// </summary>
    public static void SetEditorTransform(Vector3 position, Quaternion rotation)
    {
        UserInput? input = EditorUser.User?.Input;
        Vector3 euler = rotation.eulerAngles;
        if (input == null) // singleplayer
        {
            Editor.editor.gameObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, euler.y, 0f));
            MainCamera.instance.transform.localRotation = Quaternion.Euler(euler.x, 0f, 0f);
        }
        else
        {
            input.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, euler.y, 0f));
            MainCamera.instance.transform.localRotation = Quaternion.Euler(euler.x, 0f, 0f);
            input._nextPacketSendStop = true;
            input._hasStopped = false;
        }
        SetPitch?.Invoke(Mathf.Clamp(euler.x, -90f, 90f));
        SetYaw?.Invoke(euler.y);
        Logger.DevkitServer.LogDebug(Source, $"Set editor transform: {position.Format()}, {euler.Format()}.");
    }
    public static Ray GetLocalLookRay()
    {
        if (EditorUser.User != null && EditorUser.User.Input.Aim != null)
            return new Ray(EditorUser.User.Input.Aim.position, EditorUser.User.Input.Aim.forward);

        return new Ray(MainCamera.instance.transform.position, MainCamera.instance.transform.forward);
    }
#endif
    private struct UserInputPacket
    {
        public Vector3 Position { get; set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Speed { get; set; }
        public float DeltaTime { get; set; }
        public float TimeSinceLastQueue { get; set; }
        public Flags Flags { get; set; }

        // Y = ascend
        public Vector3 Input { get; set; }

        // ReSharper disable once UnusedParameter.Local
        public void Read(ByteReader reader, ushort version)
        {
            Flags = (Flags)reader.ReadUInt8();
            DeltaTime = reader.ReadFloat();
            if ((Flags & Flags.HasTimeSinceLastQueue) != 0)
                TimeSinceLastQueue = reader.ReadHalfPrecisionFloat();
            if ((Flags & Flags.StopMsg) == 0)
            {
                if ((Flags & Flags.Position) != 0)
                {
                    Speed = reader.ReadHalfPrecisionFloat();
                    byte inputFlag = reader.ReadUInt8();
                    Input = new Vector3((sbyte)((byte)((inputFlag & 0b00001100) >> 2) - 1),
                        (sbyte)((byte)(inputFlag & 0b00000011) - 1),
                        (sbyte)((byte)((inputFlag & 0b00110000) >> 4) - 1));
                    Position = reader.ReadHalfPrecisionVector3();
                }

                if ((Flags & Flags.Rotation) != 0)
                {
                    Pitch = reader.ReadHalfPrecisionFloat();
                    Yaw = reader.ReadHalfPrecisionFloat();
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
            writer.Write((byte)Flags);
            writer.Write(DeltaTime);
            if ((Flags & Flags.HasTimeSinceLastQueue) != 0)
                writer.WriteHalfPrecision(TimeSinceLastQueue);
            if ((Flags & Flags.StopMsg) == 0)
            {
                if ((Flags & Flags.Position) != 0)
                {
                    writer.WriteHalfPrecision(Speed);
                    byte inputFlag = (byte)((byte)(Mathf.Clamp(Input.y, -1, 1) + 1) |
                                            (byte)((byte)(Mathf.Clamp(Input.x, -1, 1) + 1) << 2) |
                                            (byte)((byte)(Mathf.Clamp(Input.z, -1, 1) + 1) << 4));
                    writer.Write(inputFlag);
                    writer.WriteHalfPrecision(Position);
                }
                    
                if ((Flags & Flags.Rotation) != 0)
                {
                    writer.WriteHalfPrecision(Pitch);
                    writer.WriteHalfPrecision(Yaw);
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
        Rotation = 8,
        HasTimeSinceLastQueue = 16
    }
}

public delegate void UserControllerUpdateRequested(EditorUser user, ref bool shouldAllow);
public enum CameraController : byte
{
    None = 0,
    Player = 1,
    Editor = 2
}