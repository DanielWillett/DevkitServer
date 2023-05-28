#if SERVER
using DevkitServer.API.Permissions;
#elif CLIENT
using DevkitServer.Patches;
#endif
using DevkitServer.API.Abstractions;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer.Actions;

public sealed class HierarchyActions
{
    public EditorActions EditorActions { get; }
    internal HierarchyActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnHierarchyObjectsDeleted += OnHierarchyObjectDeleted;
            ClientEvents.OnHierarchyObjectInstantiationRequested += OnHierarchyObjectInstantiationRequesting;
            ClientEvents.OnMovingHierarchyObjects += OnMovingHierarchyObjects;
            ClientEvents.OnMovedHierarchyObjects += OnMovedHierarchyObjects;
        }
#endif
    }
    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnHierarchyObjectsDeleted -= OnHierarchyObjectDeleted;
            ClientEvents.OnHierarchyObjectInstantiationRequested -= OnHierarchyObjectInstantiationRequesting;
            ClientEvents.OnMovingHierarchyObjects -= OnMovingHierarchyObjects;
            ClientEvents.OnMovedHierarchyObjects -= OnMovedHierarchyObjects;
        }
#endif
    }
#if CLIENT
    private void OnHierarchyObjectDeleted(uint[] instanceIds)
    {
        EditorActions.QueueAction(new DeleteHierarchyItemsAction
        {
            DeltaTime = Time.deltaTime,
            InstanceIds = instanceIds
        });
    }
    private static void OnHierarchyObjectInstantiationRequesting(IHierarchyItemTypeIdentifier type, Vector3 position)
    {
        HierarchyUtil.RequestInstantiation(type, position, Quaternion.identity, Vector3.one);
    }
    private void OnMovingHierarchyObjects(uint[] instanceIds, HierarchyObjectTransformation[] transformations, Vector3 pivotPoint)
    {
        const int samples = 4;
        if (DevkitServerModuleComponent.Ticks % samples != 0)
            return;

        EditorActions.QueueAction(new MovingHierarchyItemsAction
        {
            DeltaTime = Time.deltaTime * samples,
            InstanceIds = instanceIds,
            TransformDeltas = transformations,
            Pivot = pivotPoint
        });
    }
    private void OnMovedHierarchyObjects(uint[] instanceIds, HierarchyObjectTransformation[] transformations, Vector3[]? scales, Vector3[]? originalScales)
    {
        MovedHierarchyObjectsAction action = new MovedHierarchyObjectsAction
        {
            DeltaTime = Time.deltaTime,
            InstanceIds = instanceIds,
            TransformDeltas = transformations
        };
        if (scales != null && originalScales != null)
        {
            action.UseScale = true;
            action.Scales = scales;
            action.OriginalScales = originalScales;
        }
        EditorActions.QueueAction(action);
    }
#endif
}


[Action(ActionType.HierarchyItemsTransforming, 65 + HierarchyObjectTransformation.Capacity * 8, 4)]
[EarlyTypeInit]
public sealed class MovingHierarchyItemsAction : IAction
{
    public ActionType Type => ActionType.HierarchyItemsTransforming;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public uint[] InstanceIds { get; set; } = Array.Empty<uint>();
    public HierarchyObjectTransformation[] TransformDeltas { get; set; } = Array.Empty<HierarchyObjectTransformation>();
    public Vector3 Pivot { get; set; }
    public void Apply()
    {
        if (InstanceIds is not { Length: > 0 } || TransformDeltas is not { Length: > 0 })
            return;
        int len = Math.Min(InstanceIds.Length, TransformDeltas.Length);
        Vector3 pivot = default;
        for (int i = 0; i < len; ++i)
        {
            ref HierarchyObjectTransformation t = ref TransformDeltas[i];
            HierarchyObjectTransformation.TransformFlags flags = t.Flags;
            IDevkitHierarchyItem? item =
#if SERVER
                i == 0 ? _item : 
#endif
                null;
            item ??= HierarchyUtil.FindItem(InstanceIds[i]);
            if (item is not Component comp)
                continue;
            if (i == 0 && InstanceIds.Length == 1)
                pivot = comp.transform.position;
            else if (i == 1)
                pivot = Pivot;
            Vector3 newPos;
            bool modifyRotation = (flags & HierarchyObjectTransformation.TransformFlags.Rotation) != 0;
            if (modifyRotation)
            {
                newPos = t.OriginalPosition - pivot;
                newPos = newPos.IsNearlyZero() ? t.OriginalPosition + t.Position : pivot + t.Rotation * newPos + t.Position;
            }
            else newPos = t.OriginalPosition + t.Position;

            Quaternion newRot = t.Rotation * t.OriginalRotation;

            if (comp.gameObject.TryGetComponent(out ITransformedHandler handler))
            {
                handler.OnTransformed(
                    (flags & HierarchyObjectTransformation.TransformFlags.OriginalPosition) != 0 ? t.OriginalPosition : comp.gameObject.transform.position,
                    (flags & HierarchyObjectTransformation.TransformFlags.OriginalRotation) != 0 ? t.OriginalRotation : comp.gameObject.transform.rotation,
                    Vector3.zero,
                    newPos,
                    newRot,
                    Vector3.zero,
                    modifyRotation,
                    false
                );
            }
            else
            {
                bool eq = newPos.IsNearlyEqual(comp.transform.position);
                if (!eq && modifyRotation)
                    comp.transform.SetPositionAndRotation(newPos, newRot);
                else if (!eq)
                    comp.transform.position = newPos;
                else if (modifyRotation)
                    comp.transform.rotation = newRot;
            }
        }
    }
#if SERVER
    private IDevkitHierarchyItem? _item;
    public bool CheckCanApply()
    {
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        if (InstanceIds.Length < 1) return false;
        _item = HierarchyUtil.FindItem(InstanceIds[0]);
        return _item != null && HierarchyUtil.CheckMovePermission(_item, Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        byte flag = (byte)((InstanceIds.Length > ushort.MaxValue ? 2 : 0) | (InstanceIds.Length > byte.MaxValue ? 1 : 0));
        writer.Write(flag);
        if (InstanceIds.Length <= 1)
        {
            writer.Write((byte)0);
            return;
        }
        writer.Write(Pivot);
        if ((flag & 2) != 0)
            writer.Write(InstanceIds.Length);
        else if ((flag & 1) != 0)
            writer.Write((ushort)InstanceIds.Length);
        else
            writer.Write((byte)InstanceIds.Length);
        for (int i = 0; i < InstanceIds.Length; ++i)
            writer.Write(InstanceIds[i]);
        ref HierarchyObjectTransformation t = ref TransformDeltas[0];
        HierarchyObjectTransformation.TransformFlags flags = t.Flags;
        t.Write(writer);
        for (int i = 1; i < TransformDeltas.Length; ++i)
        {
            t = ref TransformDeltas[i];
            t.WritePartial(writer, flags);
        }
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        byte flag = reader.ReadUInt8();
        int len;
        if ((flag & 2) != 0)
            len = reader.ReadInt32();
        else if ((flag & 1) != 0)
            len = reader.ReadUInt16();
        else
            len = reader.ReadUInt8();
        if (len > 0)
        {
            Pivot = reader.ReadVector3();
            if (InstanceIds == null || InstanceIds.Length != len)
                InstanceIds = new uint[len];
            if (TransformDeltas == null || TransformDeltas.Length != len)
                TransformDeltas = new HierarchyObjectTransformation[len];
            for (int i = 0; i < len; ++i)
                InstanceIds[i] = reader.ReadUInt32();
            ref HierarchyObjectTransformation t = ref TransformDeltas[0];
            t = new HierarchyObjectTransformation(reader);
            HierarchyObjectTransformation.TransformFlags flags = t.Flags;
            Vector3 pos = t.Position;
            Quaternion rot = t.Rotation;
            for (int i = 1; i < TransformDeltas.Length; ++i)
            {
                t = ref TransformDeltas[i];
                t = new HierarchyObjectTransformation(reader, flags, pos, rot);
            }
        }
        else
        {
            Pivot = Vector3.zero;
            InstanceIds = Array.Empty<uint>();
            TransformDeltas = Array.Empty<HierarchyObjectTransformation>();
        }
    }
}
[Action(ActionType.HierarchyItemsTransform, 65 + HierarchyObjectTransformation.Capacity * 16 + sizeof(float) * 3 * 16 * 2, 0)]
[EarlyTypeInit]
public sealed class MovedHierarchyObjectsAction : IAction
{
    public ActionType Type => ActionType.HierarchyItemsTransform;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public uint[] InstanceIds { get; set; } = Array.Empty<uint>();
    public HierarchyObjectTransformation[] TransformDeltas { get; set; } = Array.Empty<HierarchyObjectTransformation>();
    public Vector3[] Scales { get; set; } = Array.Empty<Vector3>();
    public Vector3[] OriginalScales { get; set; } = Array.Empty<Vector3>();
    public bool UseScale { get; set; }

    public void Apply()
    {
        if (InstanceIds == null)
            return;
        for (int i = 0; i < InstanceIds.Length; ++i)
        {
            IDevkitHierarchyItem? item =
#if SERVER
                i == 0 ? _item : 
#endif
                null;
            item ??= HierarchyUtil.FindItem(InstanceIds[i]);
            if (item is not Component comp)
                continue;
#if SERVER
            if (i != 0 && !HierarchyUtil.CheckMovePermission(item, Instigator.m_SteamID))
            {
                Logger.LogWarning($"No permission hierarchy transformation slipped by: {item.Format()}.", method: "EDITOR ACTIONS");
                continue;
            }
#endif
            ref HierarchyObjectTransformation t = ref TransformDeltas[i];
            HierarchyObjectTransformation.TransformFlags flags = t.Flags;
            bool modifyRotation = (flags & HierarchyObjectTransformation.TransformFlags.Rotation) != 0;

            if (comp.gameObject.TryGetComponent(out ITransformedHandler handler))
            {
                handler.OnTransformed(
                    (flags & HierarchyObjectTransformation.TransformFlags.OriginalPosition) != 0 ? t.OriginalPosition : comp.gameObject.transform.position,
                    (flags & HierarchyObjectTransformation.TransformFlags.OriginalRotation) != 0 ? t.OriginalRotation : comp.gameObject.transform.rotation,
                    UseScale ? OriginalScales[i] : Vector3.zero,
                    t.Position,
                    t.Rotation,
                    UseScale ? Scales[i] : Vector3.zero,
                    modifyRotation,
                    UseScale
                );
            }
            else
            {
                if (modifyRotation)
                    comp.transform.SetPositionAndRotation(t.Position, t.Rotation);
                else
                    comp.transform.position = t.Position;
                if (UseScale)
                    comp.transform.localScale = Scales[i];
            }
        }
    }
#if SERVER
    private IDevkitHierarchyItem? _item;
    public bool CheckCanApply()
    {
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        if (InstanceIds.Length < 1) return false;
        _item = HierarchyUtil.FindItem(InstanceIds[0]);
        return _item != null && HierarchyUtil.CheckMovePermission(_item, Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int wrtLen = Math.Min(InstanceIds.Length, TransformDeltas.Length);
        if (UseScale)
            wrtLen = Math.Min(wrtLen, Math.Min(Scales.Length, OriginalScales.Length));
        byte flag = (byte)((UseScale ? 4 : 0) | (wrtLen > ushort.MaxValue ? 2 : 0) | (wrtLen > byte.MaxValue ? 1 : 0));
        writer.Write(flag);
        
        if ((flag & 2) != 0)
            writer.Write(wrtLen);
        else if ((flag & 1) != 0)
            writer.Write((ushort)wrtLen);
        else
            writer.Write((byte)wrtLen);

        for (int i = 0; i < wrtLen; ++i)
        {
            writer.Write(InstanceIds[i]);
            ref HierarchyObjectTransformation t = ref TransformDeltas[i];
            t.Write(writer);
            if (UseScale)
            {
                writer.Write(Scales[i]);
                writer.Write(OriginalScales[i]);
            }
        }
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        byte flag = reader.ReadUInt8();
        UseScale = (flag & 4) != 0;
        int len;
        if ((flag & 2) != 0)
            len = reader.ReadInt32();
        else if ((flag & 1) != 0)
            len = reader.ReadUInt16();
        else
            len = reader.ReadUInt8();
        InstanceIds = len == 0 ? Array.Empty<uint>() : new uint[len];
        TransformDeltas = len == 0 ? Array.Empty<HierarchyObjectTransformation>() : new HierarchyObjectTransformation[len];
        if (UseScale)
        {
            Scales = len == 0 ? Array.Empty<Vector3>() : new Vector3[len];
            OriginalScales = len == 0 ? Array.Empty<Vector3>() : new Vector3[len];
        }
        for (int i = 0; i < len; ++i)
        {
            InstanceIds[i] = reader.ReadUInt32();
            TransformDeltas[i] = new HierarchyObjectTransformation(reader);
            if (UseScale)
            {
                Scales[i] = reader.ReadVector3();
                OriginalScales[i] = reader.ReadVector3();
            }
        }
    }
}
[Action(ActionType.HierarchyItemsDelete, 68, 0)]
[EarlyTypeInit]
public sealed class DeleteHierarchyItemsAction : IAction
{
    public ActionType Type => ActionType.HierarchyItemsDelete;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public uint[] InstanceIds { get; set; } = Array.Empty<uint>();

    public void Apply()
    {
        if (InstanceIds == null)
            return;

        for (int i = 0; i < InstanceIds.Length; ++i)
        {
            IDevkitHierarchyItem? item = HierarchyUtil.FindItem(InstanceIds[i]);
            if (item != null && item is Component comp)
                Object.Destroy(comp.gameObject);
        }
    }
#if SERVER
    private IDevkitHierarchyItem? _item;
    public bool CheckCanApply()
    {
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        if (InstanceIds.Length < 1) return false;
        _item = HierarchyUtil.FindItem(InstanceIds[0]);
        return _item != null && HierarchyUtil.CheckMovePermission(_item, Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int len = InstanceIds.Length;
        writer.Write(len);
        for (int i = 0; i < len; ++i)
            writer.Write(InstanceIds[i]);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int len = reader.ReadInt32();
        if (InstanceIds == null || InstanceIds.Length != len)
            InstanceIds = new uint[len];
        for (int i = 0; i < len; ++i)
            InstanceIds[i] = reader.ReadUInt32();
    }
}
[Action(ActionType.HierarchyItemInstantiate, 32, 0)]
[EarlyTypeInit]
public sealed class InstantiateHierarchyItemsAction : IAction
{
    public ActionType Type => ActionType.HierarchyItemInstantiate;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public IHierarchyItemTypeIdentifier? Identifier { get; set; }
    public Vector3 Position { get; set; }

    public void Apply()
    {
        if (Identifier == null)
            return;

        Identifier.Instantiate(Position);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (Identifier == null)
            return false;
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        return HierarchyUtil.CheckPlacePermission(Identifier, Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Position);
        HierarchyItemTypeIdentifierEx.WriteIdentifier(writer, Identifier);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Position = reader.ReadVector3();
        Identifier = HierarchyItemTypeIdentifierEx.ReadIdentifier(reader);
    }
}