#if CLIENT
using DevkitServer.API;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using SDG.Framework.Utilities;
using SDG.NetPak;
using Version = System.Version;

namespace DevkitServer.Multiplayer.Movement;
internal static class ClientUserMovement
{
    internal static readonly StaticSetter<float>? SetPitch = Accessor.GenerateStaticSetter<EditorLook, float>("_pitch");
    internal static readonly StaticSetter<float>? SetYaw = Accessor.GenerateStaticSetter<EditorLook, float>("_yaw");

    private static bool _init;
    private static bool _subbed;
    private static uint _simulationFrame;
    private static int _frame;
    // private static double _lastTime;
    private static FastList<EditorInputPacket> _packets;
    private static Vector3 _lastPos;
    private static Vector2 _lastRot;
    private static float _lastFov;
    internal static bool LastWasTeleport;
    internal static byte LastTeleportId;
    internal static void StartPlayingOnEditorServer()
    {
        if (_init)
            return;

        _simulationFrame = 0;
        _frame = 0;
        _init = true;
        _packets.Clear();

        if (_subbed)
            return;

        TimeUtility.physicsUpdated += OnFixedUpdate;
        TimeUtility.updated += OnUpdate;
        _subbed = true;
    }
    internal static void StopPlayingOnEditorServer()
    {
        if (!_init)
            return;

        _init = false;
        _simulationFrame = 0;
        _frame = 0;
        _packets = default;

        if (!_subbed)
            return;

        TimeUtility.physicsUpdated -= OnFixedUpdate;
        TimeUtility.updated -= OnUpdate;
        _subbed = false;
    }
    private static void OnUpdate()
    {
        EditorUser? myUser = EditorUser.User;
        if (myUser == null)
            return;

        Transform? editorObject = myUser.EditorObject.transform;
        if (editorObject == null)
            return;

        Vector3 position = editorObject.position;
        Vector2 rotation = new Vector2(MainCamera.instance.transform.localRotation.x, editorObject.eulerAngles.y);
        float fov = MainCamera.instance.fieldOfView;
        if (_lastPos == position && rotation == _lastRot && fov == _lastFov)
            return;

        _lastPos = position;
        _lastRot = rotation;
        _lastFov = fov;

        UserMovement.EventOnUserMoved.TryInvoke(myUser);
    }
    private static void OnFixedUpdate()
    {
        ++_frame;
        if (_frame % UserMovement.FixedUpdatesPerMovementSample != 0)
            return;
        
        _frame = 0;
        ++_simulationFrame;
        SimulateLocalMovement(_simulationFrame);

        for (int i = 0; i < _packets.Length; ++i)
        {
            if (_simulationFrame - _packets[i].ClientInputFrame > UserMovement.MaxSamplesBeforeMakeup)
                _packets.RemoveAt(ref i);
        }
    }
    private static void SimulateLocalMovement(uint frame)
    {
        EditorUser? myUser = EditorUser.User;
        if (myUser == null || myUser.EditorObject == null)
            return;

        Transform? editorObject = myUser.EditorObject.transform;
        if (editorObject == null)
            return;

        Vector3 position = editorObject.position;
        Vector2 rotation = new Vector2(MainCamera.instance.transform.localEulerAngles.x, editorObject.eulerAngles.y);
        if (rotation.x > 180f)
            rotation.x -= 360f;
        if (_packets.Length > 0)
        {
            ref EditorInputPacket packet = ref _packets[_packets.Length - 1];
            if (UserControl.LocalController != CameraController.Editor && packet.LastFrameBeforeChangingController)
                return;
        }

        EditorInputPacket newPacket = default;
        newPacket.Position = position;
        newPacket.Rotation = rotation;
        newPacket.ClientInputFrame = frame;
        newPacket.LastFrameBeforeChangingController = UserControl.LocalController != CameraController.Editor;
        newPacket.LastTeleportId = LastWasTeleport ? (byte)0 : LastTeleportId;
        LastWasTeleport = false;
        NetFactory.SendGeneric(DevkitServerMessage.MovementRelay, newPacket.WriteVersioned, false);
        _packets.Add(newPacket);
    }
    private static void ReceiveLocalPacket(in EditorInputPacket packet)
    {
        for (int i = 0; i < _packets.Length; ++i)
        {
            ref EditorInputPacket oldPacket = ref _packets[i];
            if (oldPacket.ClientInputFrame < packet.ClientInputFrame)
                _packets.RemoveAt(ref i);
        }
    }
    private static void ReceiveRemotePacket(CSteamID userId, in EditorInputPacket packet)
    {
        if (UserManager.FromId(userId) is not { Movement: { } movement })
            return;

        movement.ReceivePacket(in packet);
    }
    internal static void ReceiveRemoteMovementPackets(NetPakReader reader)
    {
        int sizeStart = reader.readByteIndex;
        if (!reader.ReadUInt8(out byte playerCount))
            goto fail;

        ulong clientSteamId = Provider.client.m_SteamID;
        if (!reader.ReadUInt32(out uint vNum))
            goto fail;
        Version version = new Version((int)((vNum >> 24) & 0xFF), (int)((vNum >> 16) & 0xFF), (int)((vNum >> 8) & 0xFF), (int)(vNum & 0xFF));

        for (int i = 0; i < playerCount; ++i)
        {
            if (!reader.ReadUInt8(out byte packetCount))
                goto fail;
            if (!reader.ReadUInt32(out uint accountId))
                goto fail;

            CSteamID steamId = new CSteamID(new AccountID_t(accountId), EUniverse.k_EUniversePublic, EAccountType.k_EAccountTypeIndividual);
            for (int p = 0; p < packetCount; ++p)
            {
                if (!EditorInputPacket.Read(reader, version, out EditorInputPacket packet))
                    goto fail;

                if (steamId.m_SteamID == clientSteamId)
                {
                    ReceiveLocalPacket(in packet);
                }
                else
                {
                    ReceiveRemotePacket(steamId, in packet);
                }
            }
        }


        NetFactory.IncrementByteCount(DevkitServerMessage.MovementRelay, false, reader.readByteIndex - sizeStart);
        return;

        fail:
        NetFactory.IncrementByteCount(DevkitServerMessage.MovementRelay, false, reader.readByteIndex - sizeStart);
        Logger.DevkitServer.LogError(nameof(ReceiveRemoteMovementPackets), "Reading failed while receiving remote input packets.");
    }
}
#endif