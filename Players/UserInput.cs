using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using System.Reflection;
using System.Reflection.Emit;
#if CLIENT
using DevkitServer.Players.UI;
#endif
using SDG.NetPak;
using EditorUI = SDG.Unturned.EditorUI;

namespace DevkitServer.Players;

[EarlyTypeInit(-1)]
public class UserInput : MonoBehaviour
{
    private CameraController _controller;
    public static event Action<EditorUser>? OnUserPositionUpdated;
    public EditorUser User { get; internal set; } = null!;
    private bool _hasStopped = true;
#if CLIENT
    private float _lastFlush;
    private Vector3 _lastPos;
    private Quaternion _lastRot;
    private bool _bufferHasStop;
    private bool _applied = false;
#endif
    private float _nextPacketApplyTime;
    public bool IsOwner { get; private set; }
    public GameObject? ControllerObject { get; private set; }
    public CameraController Controller
    {
        get => _controller;
        set
        {
            if (_controller == value || value is not CameraController.Editor and not CameraController.Player)
                return;
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
        }
    }
    private static readonly Func<IDevkitTool>? GetDevkitTool;
    private static readonly InstanceGetter<EditorMovement, float> GetSpeed = Accessor.GenerateInstanceGetter<EditorMovement, float>("speed", BindingFlags.NonPublic, throwOnError: true)!;
    private static readonly InstanceGetter<EditorMovement, Vector3> GetInput = Accessor.GenerateInstanceGetter<EditorMovement, Vector3>("input", BindingFlags.NonPublic, throwOnError: true)!;
    private static readonly InstanceSetter<PlayerInput, float>? SetLastInputted = Accessor.GenerateInstanceSetter<PlayerInput, float>("lastInputed");
    private static readonly Action<PlayerUI> RestartPlayerUI = Accessor.GenerateInstanceCaller<PlayerUI, Action<PlayerUI>>("InitializePlayer", throwOnError: true)!;

#if CLIENT
    private EditorMovement? _movement;
    private static readonly ByteWriter Writer = new ByteWriter(false, 245);
#endif
    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private Queue<UserInputPacket> packets = new Queue<UserInputPacket>();
    private UserInputPacket _lastPacket;
    private Transform? _playerUiObject;
    private Transform? _editorUiObject;

    public static IDevkitTool? ActiveTool => GetDevkitTool?.Invoke();

    static UserInput()
    {
        Type? type = typeof(Provider).Assembly.GetType("SDG.Unturned.EditorInteract");
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
        if (IsOwner && !User.EditorObject.TryGetComponent(out _movement))
        {
            Destroy(this);
            Logger.LogError("Invalid UserInput setup; EditorMovement not found!");
            return;
        }
#endif

        _nextPacketApplyTime = Time.realtimeSinceStartup;
        _lastPacket = new UserInputPacket
        {
            Flags = Flags.StopMsg,
            DeltaTime = Time.deltaTime,
            Position = transform.position,
            Rotation = IsOwner ? MainCamera.instance.transform.rotation : Quaternion.Euler(Vector3.forward)
        };

        if (IsOwner)
        {
            PlayerUI? plUi = User.gameObject.GetComponentInChildren<PlayerUI>();
            if (plUi != null)
                _playerUiObject = plUi.transform;
            EditorUI? edUi = User.EditorObject.GetComponentInChildren<EditorUI>();
            if (edUi != null)
                _editorUiObject = edUi.transform;
            Controller = CameraController.Editor;
        }

        Logger.LogDebug("User input module created for " + User.SteamId.m_SteamID + " ( owner: " + IsOwner + " ).");
    }
    private void HandleControllerUpdated()
    {
        if (IsOwner)
        {
            GameObject? ctrl = ControllerObject;
            if (ctrl == null || !SetActiveMainCamera(ctrl.transform))
                return;
            if (ctrl == User.EditorObject)
            {
                Logger.LogInfo("Camera controller set to {Editor}.", ConsoleColor.DarkCyan);
#if CLIENT
                ChangeUI(true);
#endif
}
            else if (ctrl == User.Player!.player.gameObject)
            {
                Logger.LogInfo("Camera controller set to {Player}.", ConsoleColor.DarkCyan);
#if CLIENT
                ChangeUI(false);
#endif
            }
            else
                Logger.LogInfo("Camera controller set to \"" + ctrl.name + "\".", ConsoleColor.DarkCyan);
        }
    }
#if CLIENT
    private void ChangeUI(bool editor)
    {
        Component? ui = !editor ? UIAccessTools.EditorUI : UIAccessTools.PlayerUI;
        if (ui != null)
        {
            Destroy(ui);
            if (!editor)
                _editorUiObject = ui.transform;
            else
                _playerUiObject = ui.transform;
            Logger.LogInfo("Cleaned up " + (editor ? "EditorUI." : "PlayerUI."));
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
        yield return null;
        if (parent == null) // check parent wasn't destroyed
            yield break;

        Component comp = parent.gameObject.AddComponent(editor ? typeof(EditorUI) : typeof(PlayerUI));
        Logger.LogInfo("Added " + (editor ? "EditorUI." : "PlayerUI."));
        if (comp is PlayerUI player && Player.player.first != null) // loaded
        {
            yield return null;
            RestartPlayerUI(player);
            Logger.LogInfo("Restarted PlayerUI.");
            DevkitEditorHUD.Close(false);
        }
        else if (editor)
            DevkitEditorHUD.Open();
    }
#endif
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

            NetFactory.SendGeneric(NetFactory.DevkitMessage.MovementRelay, sendBytes, list, reliable: false);
        }
#endif
    }
#if CLIENT
    private void FlushPackets()
    {
        if (packets.Count < 1) return;
        HandleFlushPackets(Writer);
        int len = Writer.Count;
        NetFactory.SendGeneric(NetFactory.DevkitMessage.MovementRelay, Writer.FinishWrite(), 0, len, _bufferHasStop);
        _bufferHasStop = false;
    }
    private void HandleFlushPackets(ByteWriter writer)
    {
        int c = Math.Min(byte.MaxValue, packets!.Count);
        writer.Write((byte)c);
        for (int i = 0; i < c; ++i)
            packets.Dequeue().Write(writer);
    }
#endif
    private void HandleReadPackets(ByteReader reader)
    {
        byte c = reader.ReadUInt8();
        for (int i = 0; i < c; ++i)
        {
            UserInputPacket p = new UserInputPacket();
            p.Read(reader);
            packets.Enqueue(p);
        }
        if (User.Player != null && User.Player.player != null)
            SetLastInputted?.Invoke(User.Player.player.input, Time.realtimeSinceStartup);
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        User = null!;
    }

    [UsedImplicitly]
    private void LateUpdate()
    {
        if (!User.IsOnline)
        {
            Destroy(this);
            return;
        }
        float t = Time.realtimeSinceStartup;
#if CLIENT
        if (IsOwner)
        {
            if (_movement == null)
                return;
            bool posDiff = this.transform.position != _lastPos;
            bool rotDiff = MainCamera.instance.transform.rotation != _lastRot;
            if (posDiff || rotDiff)
            {
                OnUserPositionUpdated?.Invoke(User);
                _hasStopped = false;
                Flags flags = Flags.None;
                if (posDiff)
                    flags |= Flags.Position;
                if (rotDiff)
                    flags |= Flags.Rotation;
                _lastPacket = new UserInputPacket
                {
                    Rotation = _lastRot = MainCamera.instance.transform.rotation,
                    Position = _lastPos = transform.position,
                    Flags = flags,
                    Speed = GetSpeed(_movement),
                    Input = GetInput(_movement) with
                    {
                        y = InputEx.GetKey(ControlsSettings.ascend)
                            ? 1f
                            : (InputEx.GetKey(ControlsSettings.descend) ? -1f : 0f)
                    },
                    DeltaTime = Time.deltaTime
                };
                (packets ??= new Queue<UserInputPacket>(1)).Enqueue(_lastPacket);
            }
            else if (!_hasStopped)
            {
                _hasStopped = true;
                _lastPacket = new UserInputPacket
                {
                    Flags = Flags.StopMsg,
                    DeltaTime = Time.deltaTime,
                    Rotation = _lastRot = MainCamera.instance.transform.rotation,
                    Position = _lastPos = transform.position
                };
                (packets ??= new Queue<UserInputPacket>(1)).Enqueue(_lastPacket);
                _bufferHasStop = true;
            }

            if (t - _lastFlush > Time.fixedDeltaTime * 16)
            {
                FlushPackets();
                _lastFlush = t;
            }
        }
        else
#endif
        while (packets is { Count: > 0 } && t >= _nextPacketApplyTime)
        {
            UserInputPacket packet = packets.Dequeue();
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
                _hasStopped = true;
                this.transform.SetPositionAndRotation(packet.Position, packet.Rotation);
                OnUserPositionUpdated?.Invoke(User);
                _lastPacket = packet;
            }
        }
    }
    private void ApplyPacket(in UserInputPacket packet)
    {
        float dt = packet.DeltaTime;
        Quaternion rot = (packet.Flags & Flags.Rotation) != 0 ? packet.Rotation : this.transform.rotation;
        Vector3 pos = this.transform.position + rot *
                                              packet.Input with { y = 0 } *
                                              packet.Speed *
                                              dt
                                              +
                                              Vector3.up *
                                              packet.Input.y *
                                              dt *
                                              packet.Speed;
        if ((packet.Flags & Flags.Position) != 0)
            pos = Vector3.Lerp(pos, packet.Position, 1f / (packets.Count + 1) * ((pos - packet.Position).sqrMagnitude / 49f));
        this.transform.position = pos;
        if ((packet.Flags & Flags.Rotation) != 0)
        {
            this.transform.rotation = packet.Rotation;
        }
        OnUserPositionUpdated?.Invoke(User);
        _lastPacket = packet;
    }

    private struct UserInputPacket
    {
        private const ushort DataVersion = 0;
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float Speed { get; set; }
        public float DeltaTime { get; set; }
        public Flags Flags { get; set; }

        // Y = ascend
        public Vector3 Input { get; set; }

        public void Read(ByteReader reader)
        {
            _ = reader.ReadUInt16();
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
                    Rotation = ReadLowPrecisionQuaternion(reader);
            }
            else
            {
                Position = reader.ReadVector3();
                Rotation = reader.ReadQuaternion();
            }
        }

        public void Write(ByteWriter writer)
        {
            writer.Write(DataVersion);
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
                    WriteLowPrecisionQuaternion(writer, Rotation);
            }
            else
            {
                writer.Write(Position);
                writer.Write(Rotation);
            }
        }
        
        private static Quaternion ReadLowPrecisionQuaternion(ByteReader reader) => new Quaternion(reader.ReadInt8() / 127f,
            reader.ReadInt8() / 127f, reader.ReadInt8() / 127f, reader.ReadInt8() / 127f);

        private static void WriteLowPrecisionQuaternion(ByteWriter writer, Quaternion rot)
        {
            writer.Write((sbyte)Mathf.Clamp(rot.x * 127f, sbyte.MinValue, sbyte.MaxValue));
            writer.Write((sbyte)Mathf.Clamp(rot.y * 127f, sbyte.MinValue, sbyte.MaxValue));
            writer.Write((sbyte)Mathf.Clamp(rot.z * 127f, sbyte.MinValue, sbyte.MaxValue));
            writer.Write((sbyte)Mathf.Clamp(rot.w * 127f, sbyte.MinValue, sbyte.MaxValue));
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

public enum CameraController
{
    Player = 1,
    Editor
}