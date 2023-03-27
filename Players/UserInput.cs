using DevkitServer.Multiplayer;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Players;

[EarlyTypeInit(-1)]
public class UserInput : MonoBehaviour
{
    public static event Action<EditorUser>? OnUserPositionUpdated;
    public EditorUser User { get; private set; } = null!;
    private int sim = 0;
    private int expected = 0;
    private bool _hasStopped = false;
    public static NetCallCustom SendInputPacket = new NetCallCustom((int)NetCalls.SendMovementPacket, 64);
    public bool IsOwner { get; private set; }
    private static readonly Func<IDevkitTool>? GetDevkitTool;
    private static readonly InstanceGetter<EditorMovement, float> GetSpeed = Accessor.GenerateInstanceGetter<EditorMovement, float>("speed", BindingFlags.NonPublic);
    private static readonly InstanceGetter<EditorMovement, Vector3> GetInput = Accessor.GenerateInstanceGetter<EditorMovement, Vector3>("input", BindingFlags.NonPublic);
    private EditorMovement _movement = null!;
    private Queue<UserInputPacket>? packets;
    private UserInputPacket _lastPacket;

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
        if (toolField == null || toolField.IsStatic || !typeof(IDevkitTool).IsAssignableFrom(instanceField.FieldType))
        {
            Logger.LogWarning("Unable to find field: EditorInteract.activeTool.");
            return;
        }
        const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
        DynamicMethod method = new DynamicMethod("get_instance", attr,
            CallingConventions.Standard, typeof(IDevkitTool),
            Array.Empty<Type>(), type, true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldfld, instanceField);
        il.Emit(OpCodes.Ldfld, toolField);
        il.Emit(OpCodes.Ret);
        GetDevkitTool = (Func<IDevkitTool>)method.CreateDelegate(typeof(Func<IDevkitTool>));
    }

    [UsedImplicitly]
    private void Start()
    {
        if (!TryGetComponent(out EditorUser u))
        {
            Destroy(this);
            Logger.LogError("Invalid UserInput setup; EditorUser not found!");
            return;
        }

        User = u;
#if CLIENT
        IsOwner = User == EditorUser.User;
        if (IsOwner && !Level.editing.TryGetComponent(out _movement))
        {
            Destroy(this);
            Logger.LogError("Invalid UserInput setup; EditorMovement not found!");
            return;
        }
#endif

        Logger.LogDebug("User input module created for " + User.SteamId.m_SteamID + " ( owner: " + IsOwner + " ).");
    }
    [NetCall(NetCallSource.FromServer, (int)NetCalls.SendMovementPacket)]
    private static void ReceiveInputPacket(MessageContext ctx, ByteReader reader)
    {
        if (UserManager.FromId(ctx.Overhead.Sender) is { } sender)
        {
            sender.Input.ReceiveInputPacket(reader);
        }
    }
    private void ReceiveInputPacket(ByteReader reader)
    {
        UserInputPacket packet = new UserInputPacket();
        packet.Read(reader);
        (packets ??= new Queue<UserInputPacket>(1)).Enqueue(packet);
    }

    [UsedImplicitly]
    private void FixedUpdate()
    {
        ++sim;
        if ((sim % PlayerInput.SAMPLES) == 0)
            ++expected;
    }

    [UsedImplicitly]
    private void LateUpdate()
    {
        if (!User.IsOnline)
        {
            Destroy(this);
            return;
        }
#if CLIENT
        if (IsOwner)
        {
            if (expected <= 0)
                return;
            --expected;
            if (EditorMovement.isMoving)
            {
                OnUserPositionUpdated?.Invoke(User);
                _hasStopped = false;
                _lastPacket = new UserInputPacket
                {
                    Rotation = MainCamera.instance.transform.rotation,
                    Position = transform.position,
                    Flags = Flags.None,
                    Speed = GetSpeed(_movement),
                    Input = GetInput(_movement) with
                    {
                        y = InputEx.GetKey(ControlsSettings.ascend)
                            ? 1f
                            : (InputEx.GetKey(ControlsSettings.descend) ? -1f : 0f)
                    }
                };
                MessageOverhead overhead = new MessageOverhead(MessageFlags.None, SendInputPacket.ID, 0);
                byte[] data = SendInputPacket.Write(ref overhead, _lastPacket.Write);
                NetFactory.SendRelay(data, false);
            }
            else if (!_hasStopped)
            {
                _hasStopped = true;
                _lastPacket = new UserInputPacket
                {
                    Flags = Flags.StopMsg
                };
                MessageOverhead overhead = new MessageOverhead(MessageFlags.None, SendInputPacket.ID, 0);
                byte[] data = SendInputPacket.Write(ref overhead, _lastPacket.Write);
                NetFactory.SendRelay(data);
            }
        }
        else
#endif
        if (expected > 0)
        {
            ApplyPacket();
            --expected;
        }
        else if (packets is { Count: > 0 })
        {
            UserInputPacket packet = packets.Dequeue();
            if ((packet.Flags & Flags.StopMsg) == 0)
            {
                _lastPacket = packet;
                ApplyPacket();
            }
            else if ((_lastPacket.Flags & Flags.StopMsg) == 0)
            {
                this.transform.position = _lastPacket.Position;
                OnUserPositionUpdated?.Invoke(User);
                _lastPacket = packet;
            }
        }
    }
    private void ApplyPacket()
    {
        float dt = (Time.fixedDeltaTime * PlayerInput.SAMPLES);
        Vector3 pos = this.transform.position + _lastPacket.Rotation *
                                   _lastPacket.Input with { y = 0 } *
                                   _lastPacket.Speed *
                                   dt
                                   +
                                   Vector3.up *
                                   _lastPacket.Input.y *
                                   dt *
                                   _lastPacket.Speed;
        if ((pos - _lastPacket.Position).sqrMagnitude > 25f)
        {
            pos = _lastPacket.Position;
        }
        this.transform.position = pos;
        OnUserPositionUpdated?.Invoke(User);
    }

    private struct UserInputPacket
    {
        private const ushort DataVersion = 0;
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float Speed { get; set; }
        public Flags Flags { get; set; }

        // Y = ascend
        public Vector3 Input { get; set; }

        public void Read(ByteReader reader)
        {
            _ = reader.ReadUInt16();
            Flags = reader.ReadEnum<Flags>();
            if ((Flags & Flags.StopMsg) != 0)
                return;
            Speed = reader.ReadFloat();
            byte inputFlag = reader.ReadUInt8();
            Input = new Vector3((sbyte)((byte)((inputFlag & 0b00001100) >> 2) - 1),
                (sbyte)((byte)(inputFlag & 0b00000011) - 1),
                (sbyte)((byte)((inputFlag & 0b00110000) >> 4) - 1));
            Position = ReadLowPrecisionVector3Pos(reader);
            Rotation = ReadLowPrecisionQuaternion(reader);
        }

        public void Write(ByteWriter writer)
        {
            writer.Write(DataVersion);
            writer.Write(Flags);
            if ((Flags & Flags.StopMsg) != 0)
                return;
            writer.Write(Speed);
            byte inputFlag = (byte)((byte)(Mathf.Clamp(Input.y, -1, 1) + 1) |
                                    (byte)((byte)(Mathf.Clamp(Input.x, -1, 1) + 1) << 2) |
                                    (byte)((byte)(Mathf.Clamp(Input.z, -1, 1) + 1) << 4));
            writer.Write(inputFlag);
            WriteLowPrecisionVector3Pos(writer, Position);
            WriteLowPrecisionQuaternion(writer, Rotation);
        }

        private static Vector3 ReadLowPrecisionVector3Pos(ByteReader reader) => new Vector3(reader.ReadInt16() / 5f,
            reader.ReadInt16() / 5f, reader.ReadInt16() / 5f);

        private static Quaternion ReadLowPrecisionQuaternion(ByteReader reader) => new Quaternion(reader.ReadInt8() / 127f,
            reader.ReadInt16() / 5f, reader.ReadInt16() / 5f, reader.ReadInt16() / 5f);

        private static void WriteLowPrecisionVector3Pos(ByteWriter writer, Vector3 pos)
        {
            writer.Write((short)Mathf.Clamp(pos.x * 5f, short.MinValue, short.MaxValue));
            writer.Write((short)Mathf.Clamp(pos.y * 5f, short.MinValue, short.MaxValue));
            writer.Write((short)Mathf.Clamp(pos.z * 5f, short.MinValue, short.MaxValue));
        }

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
        StopMsg = 2
    }
}