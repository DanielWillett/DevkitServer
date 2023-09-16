namespace DevkitServer.API;
/// <summary>
/// Tracks updating positions and rotations of a <see cref="UnityEngine.Transform"/>.
/// </summary>
public delegate void TransformUpdateTrackerUpdated(TransformUpdateTracker tracker, Vector3 oldPosition, Vector3 newPosition, Quaternion oldRotation, Quaternion newRotation);
public class TransformUpdateTracker
{
    private readonly CachedMulticastEvent<TransformUpdateTrackerUpdated> _eventOnTransformUpdated = new CachedMulticastEvent<TransformUpdateTrackerUpdated>(typeof(TransformUpdateTracker), nameof(OnTransformUpdated));
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    public Transform? Transform { get; }
    public Vector3 LastPosition => _lastPosition;
    public Quaternion LastRotation => _lastRotation;
    public bool HasPositionChanged { get; private set; } = true;
    public bool HasRotationChanged { get; private set; } = true;
    
    public event TransformUpdateTrackerUpdated OnTransformUpdated
    {
        add => _eventOnTransformUpdated.Add(value);
        remove => _eventOnTransformUpdated.Remove(value);
    }
    public TransformUpdateTracker(Transform? transform)
    {
        Transform = transform;
        if (transform != null)
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }
        else
        {
            HasPositionChanged = false;
            HasRotationChanged = false;
        }
    }

    public void OnUpdate()
    {
        if (Transform == null)
        {
            HasPositionChanged = false;
            HasRotationChanged = false;
            return;
        }
        Vector3 pos = Transform.position;
        Quaternion rot = Transform.rotation;
        HasPositionChanged = pos != _lastPosition;
        HasRotationChanged = rot.x != _lastRotation.x || rot.y != _lastRotation.y || rot.z != _lastRotation.z || rot.w != _lastRotation.w;

        if (HasPositionChanged || HasRotationChanged)
            _eventOnTransformUpdated.TryInvoke(this, _lastPosition, pos, _lastRotation, rot);
        
        _lastPosition = pos;
        _lastRotation = rot;
    }
    public void TransferEventsTo(TransformUpdateTracker tracker)
    {
        _eventOnTransformUpdated.TransferTo(tracker._eventOnTransformUpdated);
    }
}
