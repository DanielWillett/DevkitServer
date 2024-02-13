using DevkitServer.API;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;

namespace DevkitServer.Multiplayer.Movement;
public class UserMovement : MonoBehaviour
{
    public const int FixedUpdatesPerMovementSample = 8; // 8 samples / 50 tps = 6.25 times per second
    public const int MaxSamplesBeforeMakeup = 6;
    public const ushort SaveDataVersion = 5;

    [UsedImplicitly]
    private static readonly InstanceSetter<PlayerInput, float>? SetLastInputted = Accessor.GenerateInstanceSetter<PlayerInput, float>("lastInputed");

    internal static readonly CachedMulticastEvent<Action<EditorUser>> EventOnUserMoved = new CachedMulticastEvent<Action<EditorUser>>(typeof(UserMovement), nameof(OnUserMoved));

    [UsedImplicitly]
    private static readonly NetCall<ulong, Vector3, Vector2, byte> SendTransform = new NetCall<ulong, Vector3, Vector2, byte>(DevkitServerNetCall.SendTransform);



    internal FastList<EditorInputPacket> Packets;
#if SERVER
    internal FastList<EditorInputPacket> SendPackets;
#endif
    private uint _lastFrame;
    private ulong _ticks;
    private bool _isCatchingUp;
    internal byte LastTeleportId;

    public EditorUser User { get; internal set; } = null!;

    /// <summary>
    /// Called whenever any user's position, rotation, or FOV (for the local user) is udpated.
    /// </summary>
    public static event Action<EditorUser> OnUserMoved
    {
        add => EventOnUserMoved.Add(value);
        remove => EventOnUserMoved.Remove(value);
    }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(nameof(UserMovement), "Invalid UserInput setup; EditorUser not found!");
            return;
        }

        if (User.IsOwner)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(nameof(UserMovement), "Invalid UserInput setup; EditorUser is owner!");
            return;
        }

#if SERVER
        Load(false);
#endif

        EventOnUserMoved.TryInvoke(User);

#if SERVER
        LastTeleportId = 1;
        for (int i = 0; i < UserManager.Users.Count; ++i)
        {
            EditorUser otherUser = UserManager.Users[i];

            if (otherUser != null && otherUser.EditorObject != null && otherUser.Movement != null)
                SendTransform.Invoke(User.Connection, otherUser.SteamId.m_SteamID, otherUser.EditorObject.transform.position, otherUser.EditorObject.transform.eulerAngles, otherUser.Movement.LastTeleportId);
        }

        SaveManager.onPostSave += Save;
#endif
    }

#if SERVER
    [UsedImplicitly]
    private void OnDestroy()
    {
        SaveManager.onPostSave -= Save;
    }
#endif

    internal void ReceivePacket(in EditorInputPacket packet)
    {
        if (packet.LastTeleportId != 0 && (packet.LastTeleportId < LastTeleportId || Math.Abs(packet.LastTeleportId - LastTeleportId) > 64) /* rolled over */)
        {
            Logger.DevkitServer.LogConditional(nameof(UserMovement), $"Received out of date teleport packet ({packet.ClientInputFrame.Format()}). Expected: {LastTeleportId.Format()}, Received: {packet.LastTeleportId.Format()}");
            return;
        }

        LastTeleportId = packet.LastTeleportId;

        Packets.Add(in packet);
#if SERVER
        SendPackets.Add(in packet);
        ServerUserMovement.HasPendingMovement = true;
#endif

        PlayerInput? input = User.Player?.player?.input;
        if (input != null)
            SetLastInputted?.Invoke(input, CachedTime.RealtimeSinceStartup);
    }

    [UsedImplicitly]
    private void FixedUpdate()
    {
        ++_ticks;

        if (_isCatchingUp)
            ++_ticks;

        if (Packets.Length == 0)
            return;

        ref EditorInputPacket lastPacket = ref Packets[Packets.Length - 1];
        _lastFrame = lastPacket.ClientInputFrame;

        // skip interpolation on teleports
        if (lastPacket.LastTeleportId == 0 || Packets.Length > 1 && Packets[Packets.Length - 2].LastTeleportId != lastPacket.LastTeleportId)
        {
            transform.SetPositionAndRotation(lastPacket.Position, Quaternion.Euler(lastPacket.Rotation));
            EventOnUserMoved.TryInvoke(User);
        }
        else
        {
            InterpolatePosition();
        }
    }
#if SERVER
    public void SetEditorPosition(Vector3 position, Vector2 rotation)
    {
        transform.SetPositionAndRotation(position, Quaternion.Euler(rotation));
        EventOnUserMoved?.TryInvoke(User);
        if (LastTeleportId == byte.MaxValue)
            LastTeleportId = 0;

        SendTransform.Invoke(Provider.GatherRemoteClientConnections(), User.SteamId.m_SteamID, position, rotation, ++LastTeleportId);
    }
#endif
    private void InterpolatePosition()
    {
        ulong expectedFrame = FixedUpdatesPerMovementSample * (ulong)_lastFrame;

        _isCatchingUp = expectedFrame - MaxSamplesBeforeMakeup - 1 > _ticks;

        double frameProgress = _ticks / (double)FixedUpdatesPerMovementSample - 1;

        GetInterpolationPoints(frameProgress, out int lowerIndex, out int upperIndex, out float a);

        ref EditorInputPacket lower = ref Packets[lowerIndex];
        ref EditorInputPacket upper = ref Packets[upperIndex];

        Vector3 pos = Vector3.Lerp(lower.Position, upper.Position, a);

        Quaternion quaternion = Quaternion.Lerp(Quaternion.Euler(lower.Rotation), Quaternion.Euler(upper.Rotation), a);

        Transform transform = User.EditorObject.transform;
        bool moved = false;
        if (!transform.position.IsNearlyEqual(pos) || !transform.rotation.IsNearlyEqual(quaternion))
        {
            transform.SetPositionAndRotation(pos, quaternion);
            moved = true;
        }

        while (lowerIndex > 0 && Packets[0].ClientInputFrame < frameProgress)
        {
            Packets.RemoveAt(0);
            --lowerIndex;
            --upperIndex;
        }
        if (lowerIndex == upperIndex && frameProgress - _lastFrame > MaxSamplesBeforeMakeup)
        {
            Packets.RemoveAt(lowerIndex);
        }

        if (moved)
            EventOnUserMoved.TryInvoke(User);
    }

    private void GetInterpolationPoints(double frame, out int lowerIndex, out int upperIndex, out float lerpAlpha)
    {
        uint lowerFrame = (uint)Math.Floor(frame);
        uint startFrame = Packets[0].ClientInputFrame;
        int lowerTestIndex = (int)(lowerFrame - startFrame);

        if (lowerTestIndex >= Packets.Length || lowerTestIndex < 0 || Packets[lowerTestIndex].ClientInputFrame != lowerFrame)
        {
            lowerTestIndex = -1;
            for (int i = 0; i < Packets.Length; ++i)
            {
                if (Packets[i].ClientInputFrame != lowerFrame)
                    continue;

                lowerTestIndex = i;
                break;
            }

            if (lowerTestIndex == -1)
                lowerTestIndex = Packets.Length - 1;
        }

        lowerIndex = lowerTestIndex;

        int higherTestIndex = -1;
        for (uint j = 1; j < MaxSamplesBeforeMakeup; ++j)
        {
            uint higherFrame = startFrame + j;
            higherTestIndex = (int)(higherFrame - startFrame);
            if (higherTestIndex >= Packets.Length || higherTestIndex < 0 || Packets[higherTestIndex].ClientInputFrame != higherFrame)
            {
                higherTestIndex = -1;
                for (int i = 0; i < Packets.Length; ++i)
                {
                    if (Packets[i].ClientInputFrame != higherFrame)
                        continue;

                    higherTestIndex = i;
                    break;
                }
            }

            if (higherTestIndex != -1)
                break;
        }

        if (higherTestIndex == -1)
            higherTestIndex = Packets.Length - 1;

        upperIndex = higherTestIndex;

        lerpAlpha = upperIndex == lowerIndex ? 0 : ((float)((frame - lowerFrame) / (upperIndex - lowerIndex)));
    }
#if SERVER
    public void Save()
    {
        if (User?.Player == null)
            return;
        Block block = new Block();
        block.writeUInt16(SaveDataVersion);

        Vector3 pos = transform.position;
        Vector2 rot = transform.eulerAngles;

        block.writeSingleVector3(pos);
        block.writeSingle(rot.x);
        block.writeSingle(rot.y);
        
        Logger.DevkitServer.LogDebug(nameof(UserMovement), $" Saved position: {pos.Format()}, ({rot.x.Format()}, {rot.y.Format()}).");
        PlayerSavedata.writeBlock(User.Player.playerID, "/DevkitServer/Input.dat", block);
    }
    private void Load(bool apply)
    {
        if (User?.Player == null) return;
        Vector3 pos;
        if (PlayerSavedata.fileExists(User.Player.playerID, "/DevkitServer/Input.dat"))
        {
            Block block = PlayerSavedata.readBlock(User.Player.playerID, "/DevkitServer/Input.dat", 0);
            ushort v = block.readUInt16();
            pos = block.readSingleVector3();
            float pitch = 0, yaw = 0;
            if (v < 2)
            {
                block.readSingleQuaternion();
            }
            else if (v is 2 or > 4)
            {
                pitch = block.readSingle();
                yaw = block.readSingle();
            }
            if (pos.IsFinite() && Mathf.Abs(pos.x) <= ushort.MaxValue && Mathf.Abs(pos.y) <= ushort.MaxValue)
            {
                if (v is > 0 and < 4)
                    block.readByte();

                Logger.DevkitServer.LogDebug(nameof(UserMovement), $" Loaded position: {pos.Format()}, yaw: {yaw.Format()}°, pitch: {pitch.Format()}°.");
                if (apply)
                    SetEditorPosition(pos, new Vector3(pitch, yaw));
                else
                {
                    transform.SetPositionAndRotation(pos, Quaternion.Euler(new Vector3(pitch, yaw)));
                    EventOnUserMoved?.TryInvoke(User);
                }
                return;
            }
        }

        PlayerSpawnpoint spawn = LevelPlayers.getSpawn(false);
        pos = spawn.point + new Vector3(0.0f, 2f, 0.0f);
        Logger.DevkitServer.LogDebug(nameof(UserMovement), $" Loaded random position: {pos.Format()}, {spawn.angle.Format()}°.");
        if (apply)
            SetEditorPosition(pos, new Vector2(0f, spawn.angle));
        else
        {
            transform.SetPositionAndRotation(pos, Quaternion.Euler(new Vector3(0f, spawn.angle)));
            EventOnUserMoved?.TryInvoke(User);
        }

    }
#endif

#if CLIENT
    /// <summary>
    /// Teleports the local editor's camera to the specified position and rotation
    /// </summary>
    public static void SetEditorTransform(Vector3 position, Quaternion rotation)
    {
        UserControl? input = EditorUser.User?.Control;
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
            ClientUserMovement.LastWasTeleport = true;
        }
        ClientUserMovement.SetPitch?.Invoke(Mathf.Clamp(euler.x, -90f, 90f));
        ClientUserMovement.SetYaw?.Invoke(euler.y);
        Logger.DevkitServer.LogDebug(nameof(SetEditorTransform), $"Set editor transform: {position.Format()}, {euler.Format()}.");
    }

    [UsedImplicitly]
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendTransform)]
    private static void ReceiveTransform(MessageContext ctx, ulong player, Vector3 pos, Vector2 eulerRotation, byte teleportId)
    {
        EditorUser? user = UserManager.FromId(player);
        if (user == null || user.Control == null)
            return;

        UserMovement? movement = user.Movement;
        if (movement == null || user.IsOwner)
        {
            user.EditorObject.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, eulerRotation.y, 0f));
            MainCamera.instance.transform.localRotation = Quaternion.Euler(eulerRotation.x, 0f, 0f);
            ClientUserMovement.SetPitch?.Invoke(Mathf.Clamp(eulerRotation.x > 90f ? eulerRotation.x - 360f : eulerRotation.x, -90f, 90f));
            ClientUserMovement.SetYaw?.Invoke(eulerRotation.y);
            ClientUserMovement.LastTeleportId = teleportId;
        }
        else
        {
            movement.transform.SetPositionAndRotation(pos, Quaternion.Euler(eulerRotation));
            movement.LastTeleportId = teleportId;
        }
        Logger.DevkitServer.LogDebug(nameof(ClientUserMovement), $"Received initial transform {user.Format()}: {pos.Format()}, {eulerRotation.Format()}.");
        ctx.Acknowledge(StandardErrorCode.Success);
        UserMovement.EventOnUserMoved.TryInvoke(user);
    }
#endif
}