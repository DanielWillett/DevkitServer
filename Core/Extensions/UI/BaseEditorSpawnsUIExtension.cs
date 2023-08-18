#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI;
using DevkitServer.Configuration;

namespace DevkitServer.Core.Extensions.UI;
internal abstract class BaseEditorSpawnsUIExtension<T> : ContainerUIExtension where T : class
{
    private bool _subbed;
    private bool _update;

    protected readonly Vector3 Offset;
    protected readonly float DistanceCurveMin;
    protected readonly float DistanceCurveLength;
    protected readonly Dictionary<T, Label> Labels = new Dictionary<T, Label>(16);
    protected override SleekWindow Parent => EditorUI.window;
    protected BaseEditorSpawnsUIExtension(Vector3 offset, float distanceCurveMin, float distanceCurveMax)
    {
        Offset = offset;
        
        DistanceCurveMin = distanceCurveMin;
        DistanceCurveLength = distanceCurveMax - distanceCurveMin;
    }

    protected override void Opened()
    {
        base.Opened();
        if (!_subbed)
        {
            MovementUtil.OnMainCameraTransformUpdated += OnUpdated;
            MovementUtil.OnMainCameraRegionUpdated += OnRegionUpdated;
            CachedTime.OnLateUpdate += OnLateUpdate;
            _subbed = true;
        }
    }

    protected override void Closed()
    {
        if (_subbed)
        {
            MovementUtil.OnMainCameraTransformUpdated -= OnUpdated;
            MovementUtil.OnMainCameraRegionUpdated -= OnRegionUpdated;
            CachedTime.OnLateUpdate -= OnLateUpdate;
            _subbed = false;
        }

        _update = false;
        base.Closed();
    }

    protected virtual bool ShouldShow(T spawn) => true;
    protected abstract void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion);
    private void OnUpdated(TransformUpdateTracker tracker, Vector3 oldPosition, Vector3 newPosition, Quaternion oldRotation, Quaternion newRotation)
    {
        _update = true;
    }
    private void OnLateUpdate()
    {
        if (_update)
        {
            UpdateAllLabels();
            _update = false;
        }
    }

    public override void Dispose()
    {
        if (_subbed)
        {
            MovementUtil.OnMainCameraTransformUpdated -= OnUpdated;
            MovementUtil.OnMainCameraRegionUpdated -= OnRegionUpdated;
            CachedTime.OnLateUpdate -= OnLateUpdate;
            _subbed = false;
        }
        base.Dispose();
    }

    protected abstract Vector3 GetPosition(T spawn);
    protected int CreateLabel(T spawn, string text)
    {
        if (Container == null)
            return -1;
        ISleekLabel label = Glazier.Get().CreateLabel();
        label.positionOffset_X = -150;
        label.positionOffset_Y = -15;
        label.sizeOffset_X = 300;
        label.sizeOffset_Y = 30;
        label.shadowStyle = ETextContrastContext.ColorfulBackdrop;
        label.text = text;
        Label lbl = new Label
        {
            Element = label,
            Region = new RegionCoord(GetPosition(spawn)),
            Spawn = spawn
        };

        if (Labels.TryGetValue(spawn, out Label lbl2))
            Container.RemoveChild(lbl2.Element);

        UpdateLabel(lbl, text);
        Container.AddChild(label);
        Labels[spawn] = lbl;
        return Labels.Count - 1;
    }
    protected void RemoveLabel(T spawn)
    {
        if (Container == null || !Labels.TryGetValue(spawn, out Label lbl))
            return;

        Container.RemoveChild(lbl.Element);
        Labels.Remove(spawn);
    }
    protected void ClearLabels()
    {
        if (Container == null)
            return;
        foreach (Label label in Labels.Values)
            Container.RemoveChild(label.Element);

        Labels.Clear();
    }
    protected void UpdateLabel(T spawn, string? text = null)
    {
        if (!Labels.TryGetValue(spawn, out Label lbl))
            return;

        UpdateLabel(lbl, text);
    }
    protected void UpdateLabel(Label label, string? text = null)
    {
        if (Container == null)
            return;
        bool show = ShouldShow(label.Spawn);
        if (!show)
        {
            if (label.Element.isVisible)
                label.Element.isVisible = false;
            return;
        }
        Vector3 position = GetPosition(label.Spawn);
        Vector3 screenPos = MainCamera.instance.WorldToViewportPoint(position + Offset);
        if (screenPos.z <= 0.0)
        {
            if (label.Element.isVisible)
                label.Element.isVisible = false;
        }
        else
        {
            Vector2 adjScreenPos = Container.ViewportToNormalizedPosition(screenPos);
            label.Element.positionScale_X = adjScreenPos.x;
            label.Element.positionScale_Y = adjScreenPos.y;
            if (text != null && !text.Equals(label.Element.text, StringComparison.Ordinal))
                label.Element.text = text;

            float dist = (position - MainCamera.instance.transform.position).sqrMagnitude;
            bool isClose = DevkitServerConfig.Config.RemoveCosmeticImprovements || dist < DistanceCurveMin * DistanceCurveMin;
            if (!isClose || isClose != label.IsClose || text != null)
            {
                if (isClose)
                {
                    label.Element.textColor = GlazierConst.DefaultLabelForegroundColor;
                }
                else
                {
                    dist = Mathf.Sqrt(dist) - DistanceCurveMin;
                    float a = 1f - Mathf.Pow(dist / DistanceCurveLength, 2);
                    label.Element.textColor = new SleekColor(ESleekTint.FONT, a);
                }
                label.IsClose = isClose;
            }

            if (!label.Element.isVisible)
                label.Element.isVisible = true;
        }
    }
    internal void UpdateAllLabels()
    {
        foreach (Label label in Labels.Values)
            UpdateLabel(label);
    }
    protected class Label
    {
        public T Spawn { get; set; } = null!;
        public ISleekLabel Element { get; set; } = null!;
        public RegionCoord Region { get; set; }
        public bool IsClose { get; set; }
    }
}
#endif