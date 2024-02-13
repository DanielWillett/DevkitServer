using DevkitServer.Players;

namespace DevkitServer.Multiplayer.Movement;
public class UserMovement : MonoBehaviour
{
    public const int FixedUpdatesPerMovementSample = 8; // 8 samples / 50 tps = 6.25 times per second
    public const int MaxSamplesBeforeMakeup = 6;

    internal FastList<EditorInputPacket> Packets;
#if SERVER
    internal FastList<EditorInputPacket> SendPackets;
#endif
    private uint _lastFrame;
    private ulong _ticks;
    private bool _isCatchingUp;

    public EditorUser User { get; internal set; } = null!;

    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(nameof(UserMovement), "Invalid UserInput setup; EditorUser not found!");
            return;
        }

        if (!User.IsOwner)
            return;

        Destroy(this);
        Logger.DevkitServer.LogError(nameof(UserMovement), "Invalid UserInput setup; EditorUser is owner!");
    }

    internal void ReceivePacket(in EditorInputPacket packet)
    {
        Packets.Add(in packet);
#if SERVER
        SendPackets.Add(in packet);
        ServerUserMovement.HasPendingMovement = true;
#endif
    }

    [UsedImplicitly]
    private void FixedUpdate()
    {
        ++_ticks;

        if (_isCatchingUp)
            ++_ticks;

        if (Packets.Length == 0)
            return;

        _lastFrame = Packets[Packets.Length - 1].ClientInputFrame;

        InterpolatePosition();
    }

    private void InterpolatePosition()
    {
        ulong expectedFrame = FixedUpdatesPerMovementSample * (ulong)_lastFrame;

        _isCatchingUp = expectedFrame - MaxSamplesBeforeMakeup - 1 > _ticks;

        double frameProgress = _ticks / (double)FixedUpdatesPerMovementSample - 1;

        GetInterpolationPoints(frameProgress, out int lowerIndex, out int upperIndex, out float a);

        ref EditorInputPacket lower = ref Packets[lowerIndex];
        ref EditorInputPacket upper = ref Packets[upperIndex];
        Vector3 pos = Vector3.Lerp(lower.Position, upper.Position, a);
        Vector2 rot = Vector2.Lerp(lower.Rotation, upper.Rotation, a);
        Quaternion quaternion = Quaternion.Euler(new Vector3(rot.x, rot.y, 0f));
        
        User.EditorObject.transform.SetPositionAndRotation(pos, quaternion);

        if (lowerIndex > 0 && Packets[0].ClientInputFrame < frameProgress)
        {
            Packets.RemoveAt(0);
            --lowerIndex;
            --upperIndex;
        }
        if (lowerIndex == upperIndex && frameProgress - _lastFrame > MaxSamplesBeforeMakeup)
        {
            Packets.RemoveAt(lowerIndex);
        }
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
}