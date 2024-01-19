#if CLIENT
using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.Configuration;

namespace DevkitServer.Core.UI.Extensions;
internal abstract class BaseEditorSpawnsUIExtension<T> : ContainerUIExtension where T : class
{
    private bool _subbed;
    private bool _update;
    private bool _wasVisibleBeforeOpened;
    private bool _wasVisibleLastUpdate;

    protected SpawnType SpawnType;
    protected readonly Vector3 Offset;
    protected readonly float DistanceCurveMin;
    protected readonly float DistanceCurveLength;
    protected readonly Dictionary<T, Label> Labels = new Dictionary<T, Label>(16);

    [ExistingMember("addButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected readonly SleekButtonIcon? AddButton;

    [ExistingMember("removeButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected readonly SleekButtonIcon? RemoveButton;

    [ExistingMember("radiusSlider", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected readonly ISleekSlider? RadiusSlider;

    [ExistingMember("rotationSlider", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekSlider? RotationSlider;

    protected override SleekWindow Parent => EditorUI.window;
    protected abstract bool IsVisible { get; set; }
    protected BaseEditorSpawnsUIExtension(Vector3 offset, float distanceCurveMin, float distanceCurveMax, SpawnType spawnType)
    {
        Offset = offset;

        SpawnType = spawnType;

        DistanceCurveMin = distanceCurveMin;
        DistanceCurveLength = distanceCurveMax - distanceCurveMin;

        if (AddButton != null)
            AddButton.IsVisible = false;
        if (RemoveButton != null)
            RemoveButton.IsVisible = false;
        if (RadiusSlider != null)
            RadiusSlider.IsVisible = false;
        if (RotationSlider != null)
            RotationSlider.IsVisible = false;
    }
    protected override void OnShown()
    {
        _wasVisibleBeforeOpened = IsVisible;
        if (!IsVisible)
            IsVisible = true;
        if (!_subbed)
        {
            MovementUtil.OnMainCameraTransformUpdated += OnUpdated;
            MovementUtil.OnMainCameraRegionUpdated += OnRegionUpdated;
            CachedTime.OnLateUpdate += OnLateUpdate;
            _subbed = true;
        }
    }
    protected override void OnHidden()
    {
        if (IsVisible != _wasVisibleBeforeOpened)
            IsVisible = _wasVisibleBeforeOpened;
        _wasVisibleBeforeOpened = false;
        if (_subbed)
        {
            MovementUtil.OnMainCameraTransformUpdated -= OnUpdated;
            MovementUtil.OnMainCameraRegionUpdated -= OnRegionUpdated;
            CachedTime.OnLateUpdate -= OnLateUpdate;
            _subbed = false;
        }

        _update = false;
    }
    protected virtual bool ShouldShow(T spawn) => true;
    protected abstract void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion);
    private void OnUpdated(TransformUpdateTracker tracker, Vector3 oldPosition, Vector3 newPosition, Quaternion oldRotation, Quaternion newRotation)
    {
        _update = true;
    }
    private void OnLateUpdate()
    {
        if (_wasVisibleLastUpdate != IsVisible)
        {
            if (IsVisible)
                OnRegionUpdated(default, MovementUtil.MainCameraRegion, MovementUtil.MainCameraIsInRegion);
            else
                ClearLabels();

            _wasVisibleLastUpdate = IsVisible;
        }
        if (_update)
        {
            UpdateAllLabels();
            _update = false;
        }
    }
    protected override void OnDestroyed()
    {
        if (_subbed)
        {
            MovementUtil.OnMainCameraTransformUpdated -= OnUpdated;
            MovementUtil.OnMainCameraRegionUpdated -= OnRegionUpdated;
            CachedTime.OnLateUpdate -= OnLateUpdate;
            _subbed = false;
        }
    }
    protected abstract Vector3 GetPosition(T spawn);
    protected int CreateLabel(T spawn, string text)
    {
        if (Container == null)
            return -1;
        ISleekLabel label = Glazier.Get().CreateLabel();
        label.PositionOffset_X = -150;
        label.PositionOffset_Y = -15;
        label.SizeOffset_X = 300;
        label.SizeOffset_Y = 30;
        label.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        label.Text = text;
        Label lbl = new Label
        {
            Element = label,
            Region = new RegionCoord(GetPosition(spawn)),
            Spawn = spawn
        };

        if (Labels.TryGetValue(spawn, out Label lbl2))
            Container.TryRemoveChild(lbl2.Element);

        UpdateLabel(lbl, text);
        Container.AddChild(label);
        Labels[spawn] = lbl;
        return Labels.Count - 1;
    }
    protected void RemoveLabel(T spawn)
    {
        if (Container == null || !Labels.TryGetValue(spawn, out Label lbl))
            return;

        Container.TryRemoveChild(lbl.Element);
        Labels.Remove(spawn);
    }
    protected void ClearLabels()
    {
        if (Container == null)
            return;
        foreach (Label label in Labels.Values)
            Container.TryRemoveChild(label.Element);

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
        if (text != null)
            label.Element.Text = text;
        if (!show)
        {
            if (label.Element.IsVisible)
                label.Element.IsVisible = false;
            return;
        }
        Vector3 position = GetPosition(label.Spawn);
        Vector3 screenPos = MainCamera.instance.WorldToViewportPoint(position + Offset);
        if (screenPos.z <= 0.0)
        {
            if (label.Element.IsVisible)
                label.Element.IsVisible = false;
        }
        else
        {
            Vector2 adjScreenPos = Container.ViewportToNormalizedPosition(screenPos);
            label.Element.PositionScale_X = adjScreenPos.x;
            label.Element.PositionScale_Y = adjScreenPos.y;

            float dist = (position - MainCamera.instance.transform.position).sqrMagnitude;
            bool isClose = DevkitServerConfig.Config.RemoveCosmeticImprovements || dist < DistanceCurveMin * DistanceCurveMin;
            if (!isClose || isClose != label.IsClose || text != null)
            {
                if (isClose)
                {
                    label.Element.TextColor = GlazierConst.DefaultLabelForegroundColor;
                }
                else
                {
                    dist = Mathf.Sqrt(dist) - DistanceCurveMin;
                    float a = 1f - Mathf.Pow(dist / DistanceCurveLength, 2);
                    label.Element.TextColor = new SleekColor(ESleekTint.FONT, a);
                }
                label.IsClose = isClose;
            }

            if (!label.Element.IsVisible)
                label.Element.IsVisible = true;
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