#if CLIENT
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.Devkit.Transactions;

namespace DevkitServer.Core.Tools;
public class DevkitServerTransformedItem(Transform transform) : IDevkitTransaction
{
    private bool _posChanged;
    private bool _rotChanged;
    private bool _scaleChanged;
    private Vector3 _endPosition;
    private Quaternion _endRotation;
    private Vector3 _endScale;
    private Vector3 _beginPosition;
    private Quaternion _beginRotation;
    private Vector3 _beginScale;
    bool IDevkitTransaction.delta => _posChanged || _rotChanged || _scaleChanged;
    public Transform Transform { get; } = transform;
    public void undo()
    {
        Reun(_endPosition, _endRotation, _endScale, _beginPosition, _beginRotation, _beginScale);
    }
    public void redo()
    {
        Reun(_beginPosition, _beginRotation, _beginScale, _endPosition, _endRotation, _endScale);
    }
    private void Reun(Vector3 startPos, Quaternion startRot, Vector3 startScale, Vector3 endPos, Quaternion endRot, Vector3 endScale)
    {
        if (Transform.gameObject.TryGetComponent(out ITransformedHandler handler))
        {
            handler.OnTransformed(startPos, startRot, startScale, endPos, endRot, endScale, _rotChanged, _scaleChanged);
        }
        else
        {
            if (_rotChanged)
                Transform.SetPositionAndRotation(endPos, endRot);
            else if (_posChanged)
                Transform.position = endPos;

            if (_scaleChanged)
                Transform.localScale = endScale;
        }
        if (Transform.gameObject.TryGetComponent(out IDevkitSelectionTransformableHandler handler2))
            handler2.transformSelection();
    }

    public void begin()
    {
        _beginPosition = Transform.position;
        _beginRotation = Transform.rotation;
        _beginScale = Transform.localScale;
    }

    public void end()
    {
        _endPosition = Transform.position;
        _endRotation = Transform.rotation;
        _endScale = Transform.localScale;

        _posChanged = _endPosition != _beginPosition;
        _rotChanged = _endRotation != _beginRotation;
        _scaleChanged = _endScale != _beginScale;
    }

    public void forget() { }
}
#endif