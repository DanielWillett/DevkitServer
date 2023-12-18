using DevkitServer.API.Permissions;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer;
#if SERVER
using DevkitServer.Core.Permissions;
using DevkitServer.Util.Encoding;
#endif

namespace DevkitServer.Core.Commands.Subsystem;
public class DevkitServerPermissions : IUserPermissionHandler, IPermissionHandler
{
#if SERVER
    private const ushort DataVersion = 1;
#endif

    private const string Source = "PERMISSIONS";
    public static bool DebugLogging { get; set; } = true;

    protected readonly CachedMulticastEvent<Action<PermissionGroup>> EventPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(DevkitServerPermissions), nameof(PermissionGroupUpdated));
    protected readonly CachedMulticastEvent<Action<PermissionGroup>> EventPermissionGroupRegistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(DevkitServerPermissions), nameof(PermissionGroupRegistered));
    protected readonly CachedMulticastEvent<Action<PermissionGroup>> EventPermissionGroupDeregistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(DevkitServerPermissions), nameof(PermissionGroupDeregistered));

    public event Action<PermissionGroup> PermissionGroupUpdated
    {
        add => EventPermissionGroupUpdated.Add(value);
        remove => EventPermissionGroupUpdated.Remove(value);
    }
    public event Action<PermissionGroup> PermissionGroupRegistered
    {
        add => EventPermissionGroupRegistered.Add(value);
        remove => EventPermissionGroupRegistered.Remove(value);
    }
    public event Action<PermissionGroup> PermissionGroupDeregistered
    {
        add => EventPermissionGroupDeregistered.Add(value);
        remove => EventPermissionGroupDeregistered.Remove(value);
    }
#if SERVER
    protected readonly CachedMulticastEvent<Action<PermissionBranch, ulong, bool>> EventUserPermissionUpdated = new CachedMulticastEvent<Action<PermissionBranch, ulong, bool>>(typeof(DevkitServerPermissions), nameof(UserPermissionUpdated));
    protected readonly CachedMulticastEvent<Action<PermissionGroup, ulong, bool>> EventUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, ulong, bool>>(typeof(DevkitServerPermissions), nameof(UserPermissionGroupUpdated));

    public event Action<PermissionBranch, ulong, bool> UserPermissionUpdated
    {
        add => EventUserPermissionUpdated.Add(value);
        remove => EventUserPermissionUpdated.Remove(value);
    }
    public event Action<PermissionGroup, ulong, bool> UserPermissionGroupUpdated
    {
        add => EventUserPermissionGroupUpdated.Add(value);
        remove => EventUserPermissionGroupUpdated.Remove(value);
    }
#else
    protected readonly CachedMulticastEvent<Action<PermissionBranch, bool>> EventUserPermissionUpdated = new CachedMulticastEvent<Action<PermissionBranch, bool>>(typeof(DevkitServerPermissions), nameof(UserPermissionUpdated));
    protected readonly CachedMulticastEvent<Action<PermissionGroup, bool>> EventUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, bool>>(typeof(DevkitServerPermissions), nameof(UserPermissionGroupUpdated));

    public event Action<PermissionBranch, bool> UserPermissionUpdated
    {
        add => EventUserPermissionUpdated.Add(value);
        remove => EventUserPermissionUpdated.Remove(value);
    }
    public event Action<PermissionGroup, bool> UserPermissionGroupUpdated
    {
        add => EventUserPermissionGroupUpdated.Add(value);
        remove => EventUserPermissionGroupUpdated.Remove(value);
    }
#endif
    private List<PermissionGroup> _permissionGroups = null!;
    private IReadOnlyList<PermissionGroup> _roPermissionGroups = null!;
#if CLIENT
    private List<PermissionBranch> _clientPermissionBranches = null!;
    private IReadOnlyList<PermissionBranch> _roClientPermissionBranches = null!;
    private List<PermissionGroup> _clientPermissionGroups = null!;
    private IReadOnlyList<PermissionGroup> _roClientPermissionGroups = null!;
#endif
#if SERVER
    private PermissionGroupsConfig.ConfigHost _config = null!;
    private ByteWriter _writer = null!;
    private ByteReader _reader = null!;
#endif
    IReadOnlyList<PermissionGroup> IPermissionHandler.PermissionGroups => _roPermissionGroups;

    [Obsolete("Don't call this.", error: true)]
    public virtual void Init()
    {
#if CLIENT
        _permissionGroups = new List<PermissionGroup>();
        _clientPermissionBranches = new List<PermissionBranch>(24);
        _clientPermissionGroups = new List<PermissionGroup>(4);
        _roClientPermissionBranches = _clientPermissionBranches.AsReadOnly();
        _roClientPermissionGroups = _clientPermissionGroups.AsReadOnly();
        _roPermissionGroups = _permissionGroups.AsReadOnly();
#else
        _config = new PermissionGroupsConfig.ConfigHost();
        ReloadIntl();
        for (int i = 0; i < _permissionGroups.Count; ++i)
            EventPermissionGroupRegistered.TryInvoke(_permissionGroups[i]);
        _writer = new ByteWriter(false, 512);
        _reader = new ByteReader();
#endif
    }
    public void Reload()
    {
#if SERVER
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            ReloadIntl();
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
#endif
    }
#if SERVER
    private void ReloadIntl()
    {
        _config.ReloadConfig();
        _permissionGroups = _config.Configuration.Groups ?? new List<PermissionGroup>(0);
        _roPermissionGroups = _permissionGroups.AsReadOnly();
    }
#endif
    public bool Register(PermissionGroup group)
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            for (int i = 0; i < _permissionGroups.Count; ++i)
            {
                if (_permissionGroups[i].Equals(group))
                    return false;
            }

            bool added = false;
            for (int i = 0; i < _permissionGroups.Count; ++i)
            {
                if (_permissionGroups[i].Priority >= group.Priority)
                    continue;
                _permissionGroups.Insert(i, group);
                added = true;
                break;
            }
            if (!added)
                _permissionGroups.Add(group);
            EventPermissionGroupRegistered.TryInvoke(group);
#if SERVER
            PermissionManager.ReplicateLatePermissionGroupRegistration(group);
            _config.SaveConfig();
#endif
            _permissionGroups.Add(group);
            if (DebugLogging)
                Logger.LogInfo($"[{Source}] Permission group registered: {group.Format()}.");
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }

        return true;
    }
    public bool Deregister(PermissionGroup group)
    {
        bool removed;
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            removed = _permissionGroups.RemoveAll(x => x.Id.Equals(group.Id, StringComparison.InvariantCultureIgnoreCase)) > 0;
            if (DebugLogging)
                Logger.LogInfo($"[{Source}] Permission group deregistered: {group.Format()}.");
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }

        return removed;
    }


#if CLIENT
    IReadOnlyList<PermissionBranch> IUserPermissionHandler.Permissions => _roClientPermissionBranches;
    IReadOnlyList<PermissionGroup> IUserPermissionHandler.PermissionGroups => _roClientPermissionGroups;
    public void ReceivePermissions(IReadOnlyList<PermissionBranch> permissions, IReadOnlyList<PermissionGroup> groups)
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (!EventUserPermissionUpdated.IsEmpty)
            {
                foreach (PermissionBranch permission in _clientPermissionBranches)
                    EventUserPermissionUpdated.TryInvoke(permission, false);
            }
            if (!EventUserPermissionGroupUpdated.IsEmpty)
            {
                foreach (PermissionGroup group in _clientPermissionGroups)
                    EventUserPermissionGroupUpdated.TryInvoke(group, false);
            }

            _clientPermissionBranches.Clear();
            _clientPermissionGroups.Clear();

            _clientPermissionBranches.AddRange(permissions);
            _clientPermissionGroups.AddRange(groups);

            if (!EventUserPermissionUpdated.IsEmpty)
            {
                foreach (PermissionBranch permission in _clientPermissionBranches)
                    EventUserPermissionUpdated.TryInvoke(permission, true);
            }
            if (!EventUserPermissionGroupUpdated.IsEmpty)
            {
                foreach (PermissionGroup group in _clientPermissionGroups)
                    EventUserPermissionGroupUpdated.TryInvoke(group, true);
            }
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void ReceivePermissionState(PermissionBranch permission, bool state)
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            for (int i = 0; i < _clientPermissionBranches.Count; ++i)
            {
                PermissionBranch perm = _clientPermissionBranches[i];
                if (!perm.Equals(permission))
                    continue;

                if (!state)
                {
                    _clientPermissionBranches.RemoveAt(i);
                    EventUserPermissionUpdated.TryInvoke(perm, false);
                }

                return;
            }

            if (!state)
                return;

            _clientPermissionBranches.Add(permission);
            EventUserPermissionUpdated.TryInvoke(permission, true);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void ReceivePermissionGroupState(PermissionGroup group, bool state)
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            for (int i = 0; i < _clientPermissionGroups.Count; ++i)
            {
                PermissionGroup perm = _clientPermissionGroups[i];
                if (!perm.Equals(group))
                    continue;

                if (!state)
                {
                    _clientPermissionGroups.RemoveAt(i);
                    EventUserPermissionGroupUpdated.TryInvoke(perm, false);
                }

                return;
            }

            if (!state)
                return;

            bool added = false;
            for (int i = 0; i < _clientPermissionGroups.Count; ++i)
            {
                if (_clientPermissionGroups[i].Priority <= group.Priority)
                    continue;

                _clientPermissionGroups.Insert(i, group);
                added = true;
                break;
            }

            if (!added)
                _clientPermissionGroups.Add(group);

            EventUserPermissionGroupUpdated.TryInvoke(group, true);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void ReceiveClearPermissions()
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (!EventUserPermissionUpdated.IsEmpty)
            {
                foreach (PermissionBranch permission in _clientPermissionBranches)
                    EventUserPermissionUpdated.TryInvoke(permission, false);
            }

            _clientPermissionBranches.Clear();
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void ReceiveClearPermissionGroups()
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (!EventUserPermissionGroupUpdated.IsEmpty)
            {
                foreach (PermissionGroup permission in _clientPermissionGroups)
                    EventUserPermissionGroupUpdated.TryInvoke(permission, false);
            }

            _clientPermissionGroups.Clear();
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public bool ReceivePermissionGroupUpdate(PermissionGroup group)
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            for (int i = 0; i < _permissionGroups.Count; ++i)
            {
                if (!_permissionGroups[i].Equals(group))
                    continue;

                PermissionGroup grp = _permissionGroups[i];
                int oldPriority = grp.Priority;
                grp.UpdateFrom(group);
                if (oldPriority != grp.Priority)
                {
                    // re-sort after priority change
                    _permissionGroups.RemoveAt(i);
                    bool added = false;
                    for (int j = 0; j < _permissionGroups.Count; ++j)
                    {
                        if (_permissionGroups[j].Priority >= grp.Priority)
                            continue;
                        _permissionGroups.Insert(j, grp);
                        added = true;
                        break;
                    }
                    if (!added)
                        _permissionGroups.Add(grp);
                }

                Logger.LogInfo("Permission group updated: " + grp.Format() + ".");
                EventPermissionGroupUpdated.TryInvoke(grp);
                return true;
            }

            _permissionGroups.Add(group);
            Logger.LogInfo("Permission group added during update: " + group.Format() + ".");
            EventPermissionGroupUpdated.TryInvoke(group);
            return false;
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }
#endif
#if SERVER
    public void SavePermissionGroup(PermissionGroup group)
    {
        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            for (int i = 0; i < _permissionGroups.Count; ++i)
            {
                if (!_permissionGroups[i].Equals(group))
                    continue;

                PermissionGroup grp = _permissionGroups[i];
                if (i != 0 && _permissionGroups[i - 1].Priority < group.Priority ||
                    i != _permissionGroups.Count - 1 && _permissionGroups[i + 1].Priority > group.Priority)
                {
                    // re-sort after priority change
                    _permissionGroups.RemoveAt(i);
                    bool added = false;
                    for (int j = 0; j < _permissionGroups.Count; ++j)
                    {
                        if (_permissionGroups[j].Priority >= grp.Priority)
                            continue;
                        _permissionGroups.Insert(j, grp);
                        added = true;
                        break;
                    }

                    if (!added)
                        _permissionGroups.Add(grp);
                }

                break;
            }

            _config.SaveConfig();
            PermissionManager.ReplicatePermissionGroupUpdate(group);
            EventPermissionGroupUpdated.TryInvoke(group);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void AddPermission(ulong user, PermissionBranch permission)
    {
        ThreadUtil.assertIsGameThread();

        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (UserManager.FromId(user) is { Permissions: { } perms } user2)
            {
                user2.AddPermission(permission);
                SavePermissions(user2.SteamId.m_SteamID, perms);
                EventUserPermissionUpdated.TryInvoke(permission, user, true);
                return;
            }

            perms = GetPermissions(user, true);
            for (int i = 0; i < perms.Count; ++i)
            {
                if (perms[i].Equals(permission))
                    return;
            }
            List<PermissionBranch> perms2 = new List<PermissionBranch>(perms.Count + 1);
            perms2.AddRange(perms);
            perms2.Add(permission);
            SavePermissions(user, perms2);
            EventUserPermissionUpdated.TryInvoke(permission, user, true);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void RemovePermission(ulong user, PermissionBranch permission)
    {
        ThreadUtil.assertIsGameThread();

        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (UserManager.FromId(user) is { Permissions: { } perms } user2)
            {
                user2.RemovePermission(permission);
                SavePermissions(user2.SteamId.m_SteamID, perms);
                EventUserPermissionUpdated.TryInvoke(permission, user, false);
                return;
            }

            perms = GetPermissions(user, true);
            List<PermissionBranch> perms2 = new List<PermissionBranch>(perms.Count);
            perms2.AddRange(perms);
            for (int i = perms2.Count - 1; i >= 0; --i)
            {
                if (perms2[i].Equals(permission))
                    perms2.RemoveAt(i);
            }
            SavePermissions(user, perms2);
            EventUserPermissionUpdated.TryInvoke(permission, user, false);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void AddPermissionGroup(ulong user, PermissionGroup group)
    {
        ThreadUtil.assertIsGameThread();

        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (UserManager.FromId(user) is { PermissionGroups: { } groups } user2)
            {
                user2.AddPermissionGroup(group);
                SavePermissionGroups(user2.SteamId.m_SteamID, groups);
                EventUserPermissionGroupUpdated.TryInvoke(group, user, true);
                return;
            }

            groups = GetPermissionGroups(user, true);
            for (int i = 0; i < groups.Count; ++i)
            {
                if (groups[i].Equals(group))
                    return;
            }
            List<PermissionGroup> perms2 = new List<PermissionGroup>(groups.Count + 1);
            perms2.AddRange(groups);
            bool added = false;
            for (int i = 0; i < perms2.Count; ++i)
            {
                if (perms2[i].Priority >= group.Priority)
                    continue;
                perms2.Insert(i, group);
                added = true;
                break;
            }
            if (!added)
                perms2.Add(group);
            SavePermissionGroups(user, perms2);
            EventUserPermissionGroupUpdated.TryInvoke(group, user, true);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void RemovePermissionGroup(ulong user, PermissionGroup group)
    {
        ThreadUtil.assertIsGameThread();

        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (UserManager.FromId(user) is { PermissionGroups: { } groups } user2)
            {
                user2.RemovePermissionGroup(group);
                SavePermissionGroups(user2.SteamId.m_SteamID, groups);
                EventUserPermissionGroupUpdated.TryInvoke(group, user, false);
                return;
            }

            groups = GetPermissionGroups(user, true);
            List<PermissionGroup> perms2 = new List<PermissionGroup>(groups.Count);
            perms2.AddRange(groups);
            for (int i = perms2.Count - 1; i >= 0; --i)
            {
                if (perms2[i].Equals(group))
                    perms2.RemoveAt(i);
            }
            SavePermissionGroups(user, perms2);
            EventUserPermissionGroupUpdated.TryInvoke(group, user, false);
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void ClearPermissions(ulong user)
    {
        ThreadUtil.assertIsGameThread();

        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (UserManager.FromId(user) is { Permissions: not null } user2)
            {
                if (!EventUserPermissionUpdated.IsEmpty)
                {
                    foreach (PermissionBranch perm in user2.Permissions)
                        EventUserPermissionUpdated.TryInvoke(perm, user, false);
                }
                user2.ClearPermissions();
                SavePermissions(user2.SteamId.m_SteamID, Array.Empty<PermissionBranch>());
                return;
            }

            if (!EventUserPermissionUpdated.IsEmpty)
            {
                IReadOnlyList<PermissionBranch> perms = GetPermissions(user, true);
                foreach (PermissionBranch perm in perms)
                    EventUserPermissionUpdated.TryInvoke(perm, user, false);
            }
            SavePermissions(user, Array.Empty<PermissionBranch>());
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }

    public void ClearPermissionGroups(ulong user)
    {
        ThreadUtil.assertIsGameThread();

        PermissionManager.PermissionLockingSemaphore.Wait();
        try
        {
            if (UserManager.FromId(user) is { Permissions: not null } user2)
            {
                if (!EventUserPermissionGroupUpdated.IsEmpty)
                {
                    foreach (PermissionGroup grp in user2.PermissionGroups)
                        EventUserPermissionGroupUpdated.TryInvoke(grp, user, false);
                }
                user2.ClearPermissionGroups();
                SavePermissionGroups(user2.SteamId.m_SteamID, Array.Empty<PermissionGroup>());
                return;
            }

            if (!EventUserPermissionGroupUpdated.IsEmpty)
            {
                IReadOnlyList<PermissionGroup> perms = GetPermissionGroups(user, true);
                foreach (PermissionGroup grp in perms)
                    EventUserPermissionGroupUpdated.TryInvoke(grp, user, false);
            }
            SavePermissionGroups(user, Array.Empty<PermissionGroup>());
        }
        finally
        {
            PermissionManager.PermissionLockingSemaphore.Release();
        }
    }
    public IReadOnlyList<PermissionBranch> GetPermissions(ulong user, bool forceReload = false)
    {
        lock (_writer)
        {
            if (!forceReload && UserManager.FromId(user) is { Permissions: { } perms })
                return perms;
            string path = FileUtil.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "Permissions.dat"));
            if (File.Exists(path))
            {
                using FileStream str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader.LoadNew(str);
                int version = _reader.ReadUInt16();
                int ct = _reader.ReadInt32();
                List<PermissionBranch> list = new List<PermissionBranch>(ct);
                for (int i = 0; i < ct; ++i)
                {
                    if (version < 1)
                    {
                        string id = _reader.ReadString();
                        if (_reader.HasFailed)
                            break;
                        if (PermissionBranch.TryParse(id, out PermissionBranch branch))
                            list.Add(branch);
                        else if (branch.Path != null)
                            Logger.LogDebug($"Invalid permission skipped: {branch.Format()}.");
                    }
                    else
                    {
                        PermissionBranch branch = PermissionBranch.Read(_reader);
                        if (_reader.HasFailed)
                            break;
                        if (branch.Valid)
                            list.Add(branch);
                        else
                            Logger.LogDebug($"Invalid permission skipped: {branch.Format()}.");
                    }
                }
                return list.AsReadOnly();
            }

            PermissionBranch[] def = DevkitServerConfig.Config.DefaultUserPermissions ?? Array.Empty<PermissionBranch>();
            SavePermissions(user, def);
            return Array.AsReadOnly(def);
        }
    }
    public virtual IReadOnlyList<PermissionGroup> GetPermissionGroups(ulong user, bool forceReload = false)
    {
        lock (_writer)
        {
            if (!forceReload && UserManager.FromId(user) is { PermissionGroups: { } perms })
                return perms;
            string path = FileUtil.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "PermissionGroups.dat"));
            if (File.Exists(path))
            {
                using FileStream str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader.LoadNew(str);
                _ = _reader.ReadUInt16();
                int ct = _reader.ReadInt32();
                List<PermissionGroup> list = new List<PermissionGroup>(ct);
                for (int i = 0; i < ct; ++i)
                {
                    bool temp = _reader.ReadBool();
                    string id = _reader.ReadString();
                    if (this.TryFindPermissionGroup(id, out PermissionGroup group))
                        list.Add(group);
                    else
                    {
                        if (!temp)
                            Logger.LogWarning("Unknown permission group in " + user + "'s permission group save, ignoring.");
                        list.Add(new PermissionGroup(id, id, Color.white, int.MinValue, Array.Empty<PermissionBranch>()));
                    }
                    if (_reader.HasFailed)
                        break;
                }
                return list.AsReadOnly();
            }
            else
            {
                string[] def = DevkitServerConfig.Config.DefaultUserPermissionGroups ?? Array.Empty<string>();
                List<PermissionGroup> list = new List<PermissionGroup>(def.Length);
                for (int i = 0; i < def.Length; ++i)
                {
                    if (this.TryFindPermissionGroup(def[i], out PermissionGroup group))
                        list.Add(group);
                    else
                        Logger.LogWarning("Unknown default permission group: " + def[i].Format() + ".");
                }

                SavePermissionGroups(user, list);
                return list.AsReadOnly();
            }
        }
    }

    protected virtual void SavePermissions(ulong user, IReadOnlyCollection<PermissionBranch> permissions)
    {
        lock (_writer)
        {
            string path = FileUtil.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "Permissions.dat"));
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !FileUtil.CheckDirectory(false, dir))
            {
                CommonErrors.LogPlayerSavedataAccessError(dir);
                return;
            }
            using FileStream str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer.Stream = str;
            _writer.Write(DataVersion);
            _writer.Write(permissions.Count);
            foreach (PermissionBranch permission in permissions)
                PermissionBranch.Write(_writer, permission);
            _writer.Flush();
            str.Flush();
            _writer.Stream = null;
        }
    }
    protected virtual void SavePermissionGroups(ulong user, IReadOnlyCollection<PermissionGroup> groups)
    {
        lock (_writer)
        {
            string path = FileUtil.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "PermissionGroups.dat"));
            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);
            using FileStream str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer.Stream = str;
            _writer.Write(DataVersion);
            _writer.Write(groups.Count);
            foreach (PermissionGroup group in groups)
            {
                _writer.Write(group.Priority == int.MinValue);
                _writer.Write(group.Id);
            }
            _writer.Flush();
            str.Flush();
            _writer.Stream = null;
        }
    }
#endif
}
