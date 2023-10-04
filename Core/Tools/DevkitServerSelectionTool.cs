#if CLIENT
using DevkitServer.Configuration;
using DevkitServer.Players;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Devkit.Transactions;
using SDG.Framework.Rendering;
using Unturned.SystemEx;

namespace DevkitServer.Core.Tools;

/// <summary>
/// Copy of <see cref="SelectionTool"/> adjusted to not require <see cref="IDevkitHierarchyItem"/>s.
/// </summary>
public abstract class DevkitServerSelectionTool : IDevkitTool
{
    protected readonly List<GameObject> IntlCopyBuffer = new List<GameObject>(2);
    protected Vector3 BeginAreaSelect;
    protected Vector3 EndAreaSelect;
    protected float AreaSelectStartTime;
    protected DevkitSelection PendingClickSelection = DevkitSelection.invalid;
    protected HashSet<DevkitSelection> TempAreaSelection = new HashSet<DevkitSelection>(16);
    private SelectionTool.ESelectionMode _handleMode;
    private bool _wantsBoundsEditor;
    private bool _canScale = true;
    private bool _canRotate = true;
    private bool _canTranslate = true;
    private bool _handleModeDirty;
    private Transform? _hoverHighlight;
    public IReadOnlyList<GameObject> CopyBuffer { get; }
    public TransformHandles Handles { get; } = new TransformHandles();
    public bool IsAreaSelecting { get; private set; }
    public bool IsDraggingHandles { get; private set; }
    public bool CanAreaSelect { get; set; } = true;
    public bool CanMiddleClickPick { get; set; } = true;
    public bool HighlightHover { get; set; } = true;
    public bool HasReferenceTransform { get; private set; }
    public Vector3 HandlePosition { get; private set; }
    public Vector3 ReferencePosition { get; private set; }
    public Quaternion ReferenceRotation { get; private set; }
    public Vector3 ReferenceScale { get; private set; }
    public bool HasReferenceScale { get; private set; }
    public Quaternion HandleRotation { get; private set; }
    public bool CanTranslate
    {
        get => _canRotate;
        set
        {
            _handleModeDirty |= value != _canTranslate;
            _canTranslate = value;
        }
    }

    public bool CanRotate
    {
        get => _canRotate;
        set
        {
            _handleModeDirty |= value != _canRotate;
            _canRotate = value;
        }
    }

    public bool CanScale
    {
        get => _canScale;
        set
        {
            _handleModeDirty |= value != _canScale;
            _canScale = value;
        }
    }
    public SelectionTool.ESelectionMode HandleMode
    {
        get => _handleMode;
        set => _handleMode = value;
    }
    public bool WantsBoundsEditor
    {
        get => _wantsBoundsEditor && _handleMode != SelectionTool.ESelectionMode.ROTATION;
        set => _wantsBoundsEditor = value;
    }
    protected DevkitServerSelectionTool()
    {
        CopyBuffer = IntlCopyBuffer.AsReadOnly();
    }
    protected virtual bool TryRaycastSelectableItems(in Ray ray, out RaycastHit hit)
    {
        hit = default;
        return false;
    }
    void IDevkitTool.update()
    {
        bool flying = EditorInteractEx.IsFlying;

        EarlyInputTick();

        if (!flying && Glazier.Get().ShouldGameProcessInput)
        {
            if (_handleModeDirty)
            {
                if (!CanScale && _handleMode == SelectionTool.ESelectionMode.SCALE)
                    _handleMode = SelectionTool.ESelectionMode.POSITION;
                else if (!CanRotate && _handleMode == SelectionTool.ESelectionMode.ROTATION)
                    _handleMode = SelectionTool.ESelectionMode.POSITION;
                if (!CanTranslate && _handleMode == SelectionTool.ESelectionMode.POSITION)
                {
                    if (CanRotate)
                        _handleMode = SelectionTool.ESelectionMode.ROTATION;
                    else if (CanScale)
                        _handleMode = SelectionTool.ESelectionMode.SCALE;
                }

                _handleModeDirty = false;
            }

            if (InputEx.GetKeyDown(KeyCode.Q) && CanTranslate)
            {
                if (_handleMode != SelectionTool.ESelectionMode.POSITION)
                {
                    _handleMode = SelectionTool.ESelectionMode.POSITION;
                    _wantsBoundsEditor = false;
                }
                else
                    _wantsBoundsEditor = !_wantsBoundsEditor;
            }
            else if (InputEx.GetKeyDown(KeyCode.W) && CanRotate)
            {
                _handleMode = SelectionTool.ESelectionMode.ROTATION;
                _wantsBoundsEditor = false;
            }
            else if (InputEx.GetKeyDown(KeyCode.R) && CanScale)
            {
                if (_handleMode != SelectionTool.ESelectionMode.SCALE)
                {
                    _handleMode = SelectionTool.ESelectionMode.SCALE;
                    _wantsBoundsEditor = false;
                }
                else
                    _wantsBoundsEditor = !_wantsBoundsEditor;
            }

            Ray ray = EditorInteractEx.Ray;
            bool isOverHandles = DevkitSelectionManager.selection.Count > 0 && Handles.Raycast(ray);
            if (DevkitSelectionManager.selection.Count > 0)
                Handles.Render(ray);

            if (InputEx.GetKeyDown(KeyCode.Mouse0))
            {
                RaycastHit hit = default;
                if (!isOverHandles)
                {
                    TryRaycastSelectableItems(in ray, out hit);
                    if (hit.transform != null)
                    {
                        IDevkitHierarchyItem? foundItem = hit.transform.GetComponentInParent<IDevkitHierarchyItem>();
                        if (foundItem is { CanBeSelected: false })
                            hit = default;
                    }
                }
                PendingClickSelection = new DevkitSelection(hit.transform == null ? null : hit.transform.gameObject, hit.collider);
                if (PendingClickSelection.collider != null)
                    DevkitSelectionManager.data.point = PendingClickSelection.gameObject.transform.position;
                
                IsDraggingHandles = isOverHandles;

                if (isOverHandles)
                {
                    Handles.MouseDown(ray);
                    if (!DevkitServerModule.IsEditing)
                    {
                        DevkitTransactionManager.beginTransaction("Transform");
                        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                            DevkitTransactionManager.recordTransaction(new DevkitServerTransformedItem(selection.transform));
                    }
                }
                else
                {
                    BeginAreaSelect = MainCamera.instance.ScreenToViewportPoint(Input.mousePosition);
                    AreaSelectStartTime = CachedTime.RealtimeSinceStartup;
                }
                
                CheckHover(in hit);
            }
            else if (!isOverHandles && CanMiddleClickPick && InputEx.GetKeyDown(KeyCode.Mouse2))
            {
                TryRaycastSelectableItems(in ray, out RaycastHit hit);
                if (hit.transform != null)
                    OnMiddleClickPicked(in hit);
                
                CheckHover(in hit);
            }
            else if (!DevkitServerConfig.Config.RemoveCosmeticImprovements || _hoverHighlight != null)
            {
                TryRaycastSelectableItems(in ray, out RaycastHit hit);
                CheckHover(in hit);
            }

            if (!IsDraggingHandles && !IsAreaSelecting && CanAreaSelect && InputEx.GetKey(KeyCode.Mouse0) && CachedTime.RealtimeSinceStartup - AreaSelectStartTime > 0.1f)
            {
                BeginAreaSelecting();
            }
            
            if (IsDraggingHandles)
            {
                Handles.snapPositionInterval = DevkitSelectionToolOptions.instance != null ? DevkitSelectionToolOptions.instance.snapPosition : 1.0f;
                Handles.snapRotationIntervalDegrees = DevkitSelectionToolOptions.instance != null ? DevkitSelectionToolOptions.instance.snapRotation : 1.0f;
                Handles.wantsToSnap = InputEx.GetKey(ControlsSettings.snap);
                Handles.MouseMove(ray);
            }
            else if (InputEx.GetKeyDown(KeyCode.E))
            {
                Physics.Raycast(ray, out RaycastHit hit, 8192f, (int)DevkitSelectionToolOptions.instance.selectionMask);
                if (hit.transform != null)
                {
                    if (DevkitSelectionManager.selection.Count > 0)
                        SimulateHandleMovement(hit.point, Quaternion.identity, Vector3.one, false, false);
                    else
                        RequestInstantiation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal), Vector3.one);
                }
            }

            if (IsAreaSelecting)
            {
                if (CanAreaSelect && !(InputEx.GetKeyDown(KeyCode.Escape) || InputEx.GetKeyDown(KeyCode.Mouse1)))
                {
                    EndAreaSelect = MainCamera.instance.ScreenToViewportPoint(Input.mousePosition);

                    Vector2 topLeft = new Vector2(Mathf.Min(EndAreaSelect.x, BeginAreaSelect.x), Mathf.Min(EndAreaSelect.y, BeginAreaSelect.y));
                    Vector2 bottomRight = new Vector2(Mathf.Max(EndAreaSelect.x, BeginAreaSelect.x), Mathf.Max(EndAreaSelect.y, BeginAreaSelect.y));

                    foreach (GameObject obj in EnumerateAreaSelectableObjects())
                    {
                        if (obj == null)
                            continue;

                        Vector3 objViewPos = MainCamera.instance.WorldToViewportPoint(obj.transform.position);
                        DevkitSelection selection = new DevkitSelection(obj, null);
                        if (objViewPos.z > 0f && objViewPos.x > topLeft.x && objViewPos.x < bottomRight.x && objViewPos.y > topLeft.y && objViewPos.y < bottomRight.y)
                        {
                            if (TempAreaSelection.Add(selection))
                                DevkitSelectionManager.add(selection);
                        }
                        else if (TempAreaSelection.Remove(selection))
                            DevkitSelectionManager.remove(selection);
                    }
                }
                else
                {
                    IsAreaSelecting = false;
                    foreach (DevkitSelection selection in TempAreaSelection)
                        DevkitSelectionManager.remove(selection);
                    TempAreaSelection.Clear();
                }
            }

            if (InputEx.GetKeyUp(KeyCode.Mouse0))
            {
                if (IsDraggingHandles)
                    EndDragHandles(true);
                else if (IsAreaSelecting)
                    IsAreaSelecting = false;
                else
                    DevkitSelectionManager.select(PendingClickSelection);
            }
            else if (IsDraggingHandles && (InputEx.GetKeyDown(KeyCode.Mouse1) || InputEx.GetKeyDown(KeyCode.Escape)))
            {
                EndDragHandles(false);
            }
        }
        else
        {
            if (IsAreaSelecting)
            {
                IsAreaSelecting = false;
            }

            if (_hoverHighlight != null)
            {
                HighlighterUtil.Unhighlight(_hoverHighlight, 0.1f);
                _hoverHighlight = null;
            }
        }

        LateInputTick();

        if (DevkitSelectionManager.selection.Count == 0)
        {
            return;
        }

        if (HandleMode == SelectionTool.ESelectionMode.POSITION)
            Handles.SetPreferredMode(_wantsBoundsEditor ? TransformHandles.EMode.PositionBounds : TransformHandles.EMode.Position);
        else if (HandleMode == SelectionTool.ESelectionMode.SCALE)
            Handles.SetPreferredMode(_wantsBoundsEditor ? TransformHandles.EMode.ScaleBounds : TransformHandles.EMode.Scale);
        else
            Handles.SetPreferredMode(TransformHandles.EMode.Rotation);

        bool globalSpace = !(HandleMode == SelectionTool.ESelectionMode.SCALE || _wantsBoundsEditor) &&
                           (DevkitSelectionToolOptions.instance == null || !DevkitSelectionToolOptions.instance.localSpace);

        Vector3 handlePosition = default;
        Quaternion handleRotation = Quaternion.identity;

        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == null)
                continue;
            handlePosition += selection.gameObject.transform.position;
            if (!globalSpace)
            {
                handleRotation = selection.gameObject.transform.rotation;
                globalSpace = true;
            }
        }

        handlePosition /= DevkitSelectionManager.selection.Count;
        HandlePosition = handlePosition;
        HandleRotation = handleRotation;
        Handles.SetPreferredPivot(handlePosition, handleRotation);

        if (_wantsBoundsEditor)
        {
            Handles.UpdateBoundsFromSelection(DevkitSelectionManager.selection.Where(x => x.gameObject != null).Select(x => x.gameObject));
        }

        if (InputEx.GetKeyDown(KeyCode.C))
        {
            IntlCopyBuffer.Clear();
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                IntlCopyBuffer.Add(selection.gameObject);
        }
        
        if (InputEx.GetKeyDown(KeyCode.V) && IntlCopyBuffer.Count > 0)
        {
            DevkitSelectionManager.clear();
            if (!DevkitServerModule.IsEditing)
                DevkitTransactionManager.beginTransaction("Paste");
            foreach (GameObject go in IntlCopyBuffer)
            {
                if (go == null)
                    continue;
                IDevkitSelectionCopyableHandler? handler = go.GetComponent<IDevkitSelectionCopyableHandler>();
                GameObject copy = handler == null ? Object.Instantiate(go) : handler.copySelection();
                if (!DevkitServerModule.IsEditing)
                    DevkitTransactionUtility.recordInstantiation(copy);
                else copy.SetActive(true);
                DevkitSelectionManager.add(new DevkitSelection(copy, null));
            }
            if (!DevkitServerModule.IsEditing)
                DevkitTransactionManager.endTransaction();
        }
        else if (InputEx.GetKeyDown(KeyCode.Delete))
        {
            if (!DevkitServerModule.IsEditing)
            {
                DevkitTransactionManager.beginTransaction("Delete");

                foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                    DevkitTransactionUtility.recordDestruction(selection.gameObject);

                DevkitTransactionManager.endTransaction();
            }
            else
            {
                foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                    Object.Destroy(selection.gameObject);
            }

            DevkitSelectionManager.clear();
        }
        else if (InputEx.GetKeyDown(KeyCode.B))
        {
            HasReferenceTransform = true;
            ReferencePosition = handlePosition;
            ReferenceRotation = handleRotation;
            ReferenceScale = Vector3.one;
            HasReferenceScale = false;
            if (DevkitSelectionManager.selection.Count == 1)
            {
                DevkitSelection selection = DevkitSelectionManager.selection.EnumerateFirst();
                if (selection.gameObject != null)
                {
                    ReferenceScale = selection.gameObject.transform.localScale;
                    HasReferenceScale = true;
                }
            }
        }
        if (InputEx.GetKeyDown(KeyCode.N) && HasReferenceTransform)
        {
            SimulateHandleMovement(ReferencePosition, ReferenceRotation, ReferenceScale, true, HasReferenceScale);
        }
        if (InputEx.GetKeyDown(ControlsSettings.focus))
        {
            UserInput.SetEditorTransform(HandlePosition - 15f * MainCamera.instance.transform.forward, MainCamera.instance.transform.rotation);
        }
    }
    protected void BeginAreaSelecting()
    {
        // start area selecting
        IsAreaSelecting = true;
        TempAreaSelection.Clear();
        if (!InputEx.GetKey(KeyCode.LeftShift) && !InputEx.GetKey(KeyCode.LeftControl))
            DevkitSelectionManager.clear();
    }
    protected void EndDragHandles(bool apply)
    {
        Handles.MouseUp();
        PendingClickSelection = DevkitSelection.invalid;
        IsDraggingHandles = false;
        if (apply)
            TransformSelection();
        else
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (selection.gameObject == null)
                    continue;
                selection.gameObject.transform.SetPositionAndRotation(selection.preTransformPosition, selection.preTransformRotation);
                selection.gameObject.transform.localScale = selection.preTransformLocalScale;
            }
        }
        if (!DevkitServerModule.IsEditing)
        {
            DevkitTransactionManager.endTransaction();

            if (!apply)
                DevkitTransactionManager.undo();
        }
    }
    void IDevkitTool.equip()
    {
        GLRenderer.render += OnGLRenderIntl;
        Handles.OnPreTransform += OnHandlesPreTransformed;
        Handles.OnTranslatedAndRotated += OnHandlesTranslatedAndRotated;
        Handles.OnTransformed += OnHandleTransformed;
        DevkitSelectionManager.clear();
    }
    void IDevkitTool.dequip()
    {
        if (_hoverHighlight != null)
        {
            HighlighterUtil.Unhighlight(_hoverHighlight, 0.1f);
            _hoverHighlight = null;
        }
        GLRenderer.render -= OnGLRenderIntl;
        Handles.OnPreTransform -= OnHandlesPreTransformed;
        Handles.OnTranslatedAndRotated -= OnHandlesTranslatedAndRotated;
        Handles.OnTransformed -= OnHandleTransformed;
        DevkitSelectionManager.clear();
        HasReferenceTransform = false;
    }
    private void CheckHover(in RaycastHit hit)
    {
        if (!DevkitServerConfig.Config.RemoveCosmeticImprovements && HighlightHover && hit.transform != null)
        {
            bool dif = _hoverHighlight != hit.transform;
            if (_hoverHighlight is not null && dif)
            {
                if (_hoverHighlight != null && !LevelObjectUtil.IsSelected(_hoverHighlight))
                    HighlighterUtil.Unhighlight(_hoverHighlight, 0.1f);

                _hoverHighlight = null;
            }

            if (dif)
            {
                if (!LevelObjectUtil.IsSelected(hit.transform))
                    HighlighterUtil.Highlight(hit.transform, Color.black, 0.1f);

                _hoverHighlight = hit.transform;
            }
        }
        else if (_hoverHighlight != null)
        {
            HighlighterUtil.Unhighlight(_hoverHighlight, 0.1f);
            _hoverHighlight = null;
        }
    }
    public abstract void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale);
    protected abstract IEnumerable<GameObject> EnumerateAreaSelectableObjects();
    protected virtual void EarlyInputTick() { }
    protected virtual void LateInputTick() { }
    protected virtual void OnMiddleClickPicked(in RaycastHit hit) { }
    protected virtual void OnGLRender()
    {
        if (IsAreaSelecting)
            DevkitServerGLUtility.DrawSelectBox(BeginAreaSelect, EndAreaSelect);
    }
    public void SimulateHandleMovement(Vector3 newPosition, Quaternion newRotation, Vector3 newScale, bool hasRotation, bool hasScale)
    {
        if (!DevkitServerModule.IsEditing)
        {
            DevkitTransactionManager.beginTransaction("Transform");
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (selection.gameObject != null)
                    DevkitTransactionManager.recordTransaction(new DevkitServerTransformedItem(selection.transform));
            }
        }
        if (DevkitSelectionManager.selection.Count == 1)
        {
            DevkitSelection selection = DevkitSelectionManager.selection.EnumerateFirst();
            if (selection != null && selection.gameObject != null)
            {
                if (selection.gameObject.TryGetComponent(out ITransformedHandler handler))
                {
                    handler.OnTransformed(selection.preTransformPosition, selection.preTransformRotation,
                        selection.preTransformLocalScale, newPosition, newRotation, newScale, hasRotation, hasScale);
                }
                else
                {
                    if (hasRotation)
                        selection.transform.SetPositionAndRotation(newPosition, newRotation);
                    else
                        selection.transform.position = newPosition;

                    if (hasScale)
                        selection.transform.localScale = newScale;
                }
            }
        }
        else Handles.ExternallyTransformPivot(newPosition, newRotation, hasRotation);

        TransformSelection();

        if (!DevkitServerModule.IsEditing)
            DevkitTransactionManager.endTransaction();
    }
    protected static void TransformSelection()
    {
        foreach (DevkitSelection devkitSelection in DevkitSelectionManager.selection)
        {
            if (devkitSelection.gameObject != null && devkitSelection.gameObject.TryGetComponent(out IDevkitSelectionTransformableHandler handler))
                handler.transformSelection();
        }
    }

    private void OnGLRenderIntl() => OnGLRender();
    private static void OnHandlesPreTransformed(Matrix4x4 worldToPivot)
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == null)
                continue;

            Transform transform = selection.gameObject.transform;

            selection.preTransformPosition = transform.position;
            selection.preTransformRotation = transform.rotation;
            selection.preTransformLocalScale = transform.localScale;
            selection.localToWorld = transform.localToWorldMatrix;
            selection.relativeToPivot = worldToPivot * selection.localToWorld;
        }
    }
    private static void OnHandlesTranslatedAndRotated(Vector3 worldPositionDelta, Quaternion worldRotationDelta, Vector3 pivotPosition, bool modifyRotation)
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == null)
                continue;

            Vector3 newPosition;
            if (modifyRotation)
            {
                newPosition = selection.preTransformPosition - pivotPosition;
                newPosition = newPosition.IsNearlyZero()
                    ? (selection.preTransformPosition + worldPositionDelta)
                    : (pivotPosition + worldRotationDelta * newPosition + worldPositionDelta);
            }
            else
                newPosition = selection.preTransformPosition + worldPositionDelta;

            if (selection.gameObject.TryGetComponent(out ITransformedHandler handler))
            {
                handler.OnTransformed(selection.preTransformPosition, selection.preTransformRotation,
                    default, newPosition, worldRotationDelta * selection.preTransformRotation, default,
                    modifyRotation, false);
            }
            else
            {
                bool samePos = newPosition.IsNearlyEqual(selection.gameObject.transform.position);

                if (modifyRotation && !samePos)
                    selection.gameObject.transform.SetPositionAndRotation(newPosition, worldRotationDelta * selection.preTransformRotation);
                else if (modifyRotation)
                    selection.gameObject.transform.rotation = worldRotationDelta * selection.preTransformRotation;
                else if (!samePos)
                    selection.gameObject.transform.position = newPosition;
            }
        }
    }
    private static void OnHandleTransformed(Matrix4x4 pivotToWorld)
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == null)
                continue;

            Matrix4x4 matrix = pivotToWorld * selection.relativeToPivot;
            if (selection.gameObject.TryGetComponent(out ITransformedHandler handler))
            {
                handler.OnTransformed(selection.preTransformPosition, selection.preTransformRotation,
                    selection.preTransformLocalScale, matrix.GetPosition(), matrix.GetRotation(), matrix.lossyScale,
                    true, true);
            }
            else
            {
                selection.gameObject.transform.SetPositionAndRotation(matrix.GetPosition(), matrix.GetRotation().GetRoundedIfNearlyAxisAligned());
                selection.gameObject.transform.SetLocalScale_RoundIfNearlyEqualToOne(matrix.lossyScale);
            }
        }
    }
}
#endif