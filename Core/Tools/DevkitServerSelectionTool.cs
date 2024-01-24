#if CLIENT
using DevkitServer.API.Devkit;
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
    private bool _justCancelledDragging;
    private bool _hasLetGoOfCtrlSinceCopyTransform = true;
    private bool _detectedMouseDown;
    private GameObject? _hoverHighlight;
    public IReadOnlyList<GameObject> CopyBuffer { get; }
    public TransformHandles Handles { get; } = new TransformHandles();
    public bool IsAreaSelecting { get; private set; }
    public bool IsDraggingHandles { get; private set; }
    public bool CanAreaSelect { get; set; } = true;
    public bool CanMiddleClickPick { get; set; } = true;
    public bool HighlightHover { get; set; } = true;
    public bool RootSelections { get; set; } = true;
    public bool HasReferenceTransform { get; private set; }
    public Vector3 HandlePosition { get; private set; }
    public Vector3 ReferencePosition { get; private set; }
    public Quaternion ReferenceRotation { get; private set; }
    public Vector3 ReferenceScale { get; private set; }
    public bool HasReferenceScale { get; private set; }
    public Quaternion HandleRotation { get; private set; }
    public bool CanTranslate
    {
        get => _canTranslate;
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
    private static List<RaycastHit>? _hits;
    protected virtual void RaycastAllSelectableItems(ref Ray ray, ICollection<RaycastHit> hits) { }
    protected virtual bool TryRaycastSelectableItems(ref Ray ray, out RaycastHit hit)
    {
        _hits ??= new List<RaycastHit>(16);
        try
        {
            RaycastAllSelectableItems(ref ray, _hits);
            if (_hits.Count == 0)
            {
                hit = default;
                return false;
            }

            Vector3 point = ray.origin;

            float minDist = float.MaxValue;
            int minDistIndex = -1;
            for (int i = 0; i < _hits.Count; ++i)
            {
                float dist = (_hits[i].point - point).sqrMagnitude;

                if (dist >= minDist)
                    continue;

                minDist = dist;
                minDistIndex = i;
            }

            hit = _hits[minDistIndex];
            return true;
        }
        finally
        {
            _hits.Clear();
        }

        hit = default;
        return false;
    }
    void IDevkitTool.update()
    {
        bool flying = EditorInteractEx.IsFlying;

        EarlyInputTick();

        bool ctrl = InputUtil.IsHoldingControl();
        if (!ctrl)
        {
            _hasLetGoOfCtrlSinceCopyTransform = true;
        }

        if (!flying && Glazier.Get().ShouldGameProcessInput)
        {
            // handle mode change keybinds

            if (_handleModeDirty)
            {
                if (!(CanScale || DevkitSelectionManager.selection.Count > 1) && _handleMode == SelectionTool.ESelectionMode.SCALE)
                    _handleMode = SelectionTool.ESelectionMode.POSITION;
                else if (!(CanRotate || DevkitSelectionManager.selection.Count > 1) && _handleMode == SelectionTool.ESelectionMode.ROTATION)
                    _handleMode = SelectionTool.ESelectionMode.POSITION;
                if (!CanTranslate && _handleMode == SelectionTool.ESelectionMode.POSITION)
                {
                    if (CanRotate || DevkitSelectionManager.selection.Count > 1)
                        _handleMode = SelectionTool.ESelectionMode.ROTATION;
                    else if (CanScale || DevkitSelectionManager.selection.Count > 1)
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
            else if (InputEx.GetKeyDown(KeyCode.W) && (CanRotate || DevkitSelectionManager.selection.Count > 1))
            {
                _handleMode = SelectionTool.ESelectionMode.ROTATION;
                _wantsBoundsEditor = false;
            }
            else if (InputEx.GetKeyDown(KeyCode.R) && (CanScale || DevkitSelectionManager.selection.Count > 1))
            {
                if (_handleMode != SelectionTool.ESelectionMode.SCALE)
                {
                    _handleMode = SelectionTool.ESelectionMode.SCALE;
                    _wantsBoundsEditor = false;
                }
                else
                    _wantsBoundsEditor = !_wantsBoundsEditor;
            }

            if (_handleMode == SelectionTool.ESelectionMode.SCALE && !CanScale && (CanTranslate || CanRotate))
                _wantsBoundsEditor = true;

            // handle hover check

            Ray ray = EditorInteractEx.Ray;
            bool isOverHandles = DevkitSelectionManager.selection.Count > 0 && Handles.Raycast(ray);

            if (DevkitSelectionManager.selection.Count > 0)
                Handles.Render(ray);

            if (InputEx.GetKeyDown(KeyCode.Mouse0))
            {
                _detectedMouseDown = true;
                // selecting an item by clicking

                RaycastHit hit = default;
                if (!isOverHandles)
                {
                    TryRaycastSelectableItems(ref ray, out hit);
                    if (hit.transform != null)
                    {
                        IDevkitHierarchyItem? foundItem = hit.transform.GetComponentInParent<IDevkitHierarchyItem>();
                        if (foundItem is { CanBeSelected: false })
                            hit = default;
                    }
                }
                PendingClickSelection = new DevkitSelection(hit.transform != null ? RootSelections ? hit.transform.root.gameObject : hit.transform.gameObject : null, hit.collider);
                if (PendingClickSelection.gameObject != null)
                    DevkitSelectionManager.data.point = PendingClickSelection.gameObject.transform.position;
                
                IsDraggingHandles = isOverHandles;

                if (isOverHandles)
                {
                    Handles.MouseDown(ray);
                    if (!DevkitServerModule.IsEditing)
                    {
                        DevkitTransactionManager.beginTransaction("Transform(" + DevkitSelectionManager.selection.Count + ")");
                        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                            DevkitTransactionManager.recordTransaction(new DevkitServerTransformedItem(selection.transform));
                    }
                }
                else
                {
                    BeginAreaSelect = MainCamera.instance.ScreenToViewportPoint(Input.mousePosition);
                    AreaSelectStartTime = CachedTime.RealtimeSinceStartup;
                }
                
                CheckHover(ref hit);
            }
            else if (CanMiddleClickPick && InputEx.GetKeyDown(KeyCode.Mouse2))
            {
                // middle click picking

                TryRaycastSelectableItems(ref ray, out RaycastHit hit);
                if (hit.transform != null)
                    OnMiddleClickPicked(ref hit);
                
                CheckHover(ref hit);
            }
            else if (!DevkitServerConfig.Config.RemoveCosmeticImprovements || _hoverHighlight != null)
            {
                TryRaycastSelectableItems(ref ray, out RaycastHit hit);
                CheckHover(ref hit);
            }

            if (_detectedMouseDown && !_justCancelledDragging && !IsDraggingHandles && !IsAreaSelecting && CanAreaSelect && CachedTime.RealtimeSinceStartup - AreaSelectStartTime > 0.1f && InputEx.GetKey(KeyCode.Mouse0))
            {
                BeginAreaSelecting();
            }
            
            if (IsDraggingHandles)
            {
                Handles.snapPositionInterval = DevkitSelectionToolOptions.instance != null ? DevkitSelectionToolOptions.instance.snapPosition : 1.0f;
                Handles.snapRotationIntervalDegrees = DevkitSelectionToolOptions.instance != null ? DevkitSelectionToolOptions.instance.snapRotation : 1.0f;
                Handles.wantsToSnap = InputEx.GetKey(ControlsSettings.snap);
                Handles.MouseMove(ray);
                if (DevkitSelectionManager.selection.Count > 0)
                    OnTempMoved();
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
                if (CanAreaSelect && !InputEx.ConsumeKeyDown(KeyCode.Escape))
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
                            {
                                _handleModeDirty = true;
                                DevkitSelectionManager.add(selection);
                            }
                        }
                        else if (TempAreaSelection.Remove(selection))
                        {
                            _handleModeDirty = true;
                            DevkitSelectionManager.remove(selection);
                        }
                    }
                }
                else
                {
                    _justCancelledDragging = true;
                    IsAreaSelecting = false;
                    foreach (DevkitSelection selection in TempAreaSelection)
                        DevkitSelectionManager.remove(selection);
                    TempAreaSelection.Clear();
                    _handleModeDirty = true;
                }
            }

            if (InputEx.GetKeyUp(KeyCode.Mouse0))
            {
                _detectedMouseDown = false;
                _justCancelledDragging = false;
                if (IsDraggingHandles)
                    EndDragHandles(true);
                else if (IsAreaSelecting)
                    IsAreaSelecting = false;
                else
                {
                    // for Ctrl + B -> Ctrl + N
                    if (!_hasLetGoOfCtrlSinceCopyTransform && ctrl)
                        DevkitSelectionManager.clear();

                    DevkitSelectionManager.select(PendingClickSelection);
                    _handleModeDirty = true;
                }
            }
            else if (IsDraggingHandles && InputEx.ConsumeKeyDown(KeyCode.Escape))
            {
                EndDragHandles(false);
                _justCancelledDragging = true;
                BeginAreaSelect = MainCamera.instance.ScreenToViewportPoint(Input.mousePosition);
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
                if (!IsSelected(_hoverHighlight))
                    HighlighterUtil.Unhighlight(_hoverHighlight.transform, 0.1f);
                _hoverHighlight = null;
            }

            _detectedMouseDown = false;
        }

        LateInputTick();

        if (DevkitSelectionManager.selection.Count == 0)
            return;

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

            if (globalSpace)
                continue;

            handleRotation = selection.gameObject.transform.rotation;
            globalSpace = true;
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
            Paste(IntlCopyBuffer);
        }
        else if (InputEx.GetKeyDown(KeyCode.Delete))
        {
            if (!DevkitServerModule.IsEditing)
            {
                DevkitTransactionManager.beginTransaction("Delete(" + DevkitSelectionManager.selection.Count + ")");

                foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                    Delete(selection);

                DevkitTransactionManager.endTransaction();
            }
            else
            {
                foreach (DevkitSelection selection in DevkitSelectionManager.selection)
                    Delete(selection);
            }

            DevkitSelectionManager.clear();
            _handleModeDirty = true;
        }
        else if (InputEx.GetKeyDown(KeyCode.B))
        {
            HasReferenceTransform = true;
            ReferencePosition = handlePosition;
            ReferenceRotation = handleRotation;
            ReferenceScale = Vector3.one;
            HasReferenceScale = false;
            _hasLetGoOfCtrlSinceCopyTransform = !ctrl;
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
        if (InputEx.GetKey(KeyCode.LeftShift) || InputUtil.IsHoldingControl())
            return;

        DevkitSelectionManager.clear();
        _handleModeDirty = true;
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
        _handleModeDirty = true;
    }
    void IDevkitTool.dequip()
    {
        if (_hoverHighlight != null)
        {
            HighlighterUtil.Unhighlight(_hoverHighlight.transform, 0.1f);
            _hoverHighlight = null;
        }
        GLRenderer.render -= OnGLRenderIntl;
        Handles.OnPreTransform -= OnHandlesPreTransformed;
        Handles.OnTranslatedAndRotated -= OnHandlesTranslatedAndRotated;
        Handles.OnTransformed -= OnHandleTransformed;
        DevkitSelectionManager.clear();
        _handleModeDirty = true;
        HasReferenceTransform = false;
    }
    private void CheckHover(ref RaycastHit hit)
    {
        if (!DevkitServerConfig.Config.RemoveCosmeticImprovements && HighlightHover && hit.colliderInstanceID != 0)
        {
            Collider? c = hit.collider;
            if (c != null)
            {
                GameObject hitGo = c.transform.root.gameObject;
                bool dif = _hoverHighlight != hitGo;
                if (_hoverHighlight is not null && dif)
                {
                    if (_hoverHighlight != null && !IsSelected(_hoverHighlight))
                        HighlighterUtil.Unhighlight(_hoverHighlight.transform, 0.1f);

                    _hoverHighlight = null;
                }

                if (dif)
                {
                    if (!IsSelected(hitGo))
                        HighlighterUtil.Highlight(hitGo.transform, Color.black, 0.1f);

                    _hoverHighlight = hitGo;
                }

                return;
            }
        }

        if (_hoverHighlight == null)
            return;

        if (!IsSelected(_hoverHighlight))
            HighlighterUtil.Unhighlight(_hoverHighlight.transform, 0.1f);
        _hoverHighlight = null;
    }
    private static bool IsSelected(GameObject gameObject)
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == gameObject)
                return true;
        }

        return false;
    }
    public abstract void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale);
    protected abstract IEnumerable<GameObject> EnumerateAreaSelectableObjects();
    protected virtual void EarlyInputTick() { }
    protected virtual void LateInputTick() { }
    protected virtual void OnTempMoved() { }
    protected virtual void OnMiddleClickPicked(ref RaycastHit hit) { }
    protected virtual void OnGLRender()
    {
        if (IsAreaSelecting)
            DevkitServerGLUtility.DrawSelectBox(BeginAreaSelect, EndAreaSelect);
    }
    protected virtual void Delete(DevkitSelection selection)
    {
        bool destroy = true;

        if (selection.gameObject.TryGetComponent(out IDevkitSelectionDeletableHandler deletable))
            deletable.Delete(ref destroy);

        if (!destroy)
            return;

        if (!DevkitServerModule.IsEditing)
            DevkitTransactionUtility.recordDestruction(selection.gameObject);
        else
            Object.Destroy(selection.gameObject);
    }
    protected virtual void Paste(IReadOnlyList<GameObject> copyBuffer)
    {
        DevkitSelectionManager.clear();
        
        if (!DevkitServerModule.IsEditing)
            DevkitTransactionManager.beginTransaction("Paste(" + IntlCopyBuffer.Count + ")");

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
        _handleModeDirty = true;
    }
    public void SimulateHandleMovement(Vector3 newPosition, Quaternion newRotation, Vector3 newScale, bool hasRotation, bool hasScale)
    {
        if (!DevkitServerModule.IsEditing)
        {
            DevkitTransactionManager.beginTransaction("Transform(" + DevkitSelectionManager.selection.Count + ")");
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
    private void OnHandlesTranslatedAndRotated(Vector3 worldPositionDelta, Quaternion worldRotationDelta, Vector3 pivotPosition, bool modifyRotation)
    {
        if (!modifyRotation && !CanTranslate)
            return;
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == null)
                continue;

            Vector3 newPosition = selection.preTransformPosition;
            if (CanTranslate)
            {
                if (modifyRotation)
                {
                    newPosition = selection.preTransformPosition - pivotPosition;
                    newPosition = newPosition.IsNearlyZero()
                        ? (selection.preTransformPosition + worldPositionDelta)
                        : (pivotPosition + worldRotationDelta * newPosition + worldPositionDelta);
                }
                else
                    newPosition = selection.preTransformPosition + worldPositionDelta;
            }

            if (selection.gameObject.TryGetComponent(out ITransformedHandler handler))
            {
                handler.OnTransformed(selection.preTransformPosition, selection.preTransformRotation,
                    default, newPosition, worldRotationDelta * selection.preTransformRotation, default,
                    modifyRotation && CanRotate, false);
            }
            else
            {
                bool samePos = !CanTranslate || newPosition.IsNearlyEqual(selection.gameObject.transform.position);

                if (modifyRotation && CanRotate && !samePos)
                    selection.gameObject.transform.SetPositionAndRotation(newPosition, worldRotationDelta * selection.preTransformRotation);
                else if (modifyRotation && CanRotate)
                    selection.gameObject.transform.rotation = worldRotationDelta * selection.preTransformRotation;
                else if (!samePos)
                    selection.gameObject.transform.position = newPosition;
            }
        }
    }
    private void OnHandleTransformed(Matrix4x4 pivotToWorld)
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.gameObject == null)
                continue;

            Matrix4x4 matrix = pivotToWorld * selection.relativeToPivot;
            if (selection.gameObject.TryGetComponent(out ITransformedHandler handler))
            {
                handler.OnTransformed(selection.preTransformPosition, selection.preTransformRotation,
                    selection.preTransformLocalScale, CanTranslate ? matrix.GetPosition() : selection.preTransformPosition,
                    CanRotate ? matrix.GetRotation() : selection.preTransformRotation,
                    CanScale ? matrix.lossyScale : selection.preTransformLocalScale, CanRotate, CanScale);
            }
            else
            {
                if (CanRotate)
                {
                    if (CanTranslate)
                        selection.gameObject.transform.SetPositionAndRotation(matrix.GetPosition(), matrix.GetRotation().GetRoundedIfNearlyAxisAligned());
                    else
                        selection.gameObject.transform.rotation = matrix.GetRotation().GetRoundedIfNearlyAxisAligned();
                }
                else if (CanTranslate)
                    selection.gameObject.transform.position = matrix.GetPosition();

                if (CanScale)
                    selection.gameObject.transform.SetLocalScale_RoundIfNearlyEqualToOne(matrix.lossyScale);
            }
        }
    }
}
#endif