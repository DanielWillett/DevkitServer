using DevkitServer.Multiplayer.Networking;
using DevkitServer.Plugins;
using System.Reflection;
#if SERVER
using DevkitServer.Configuration;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Util.Encoding;
using System.Text.Json.Serialization;
#endif

namespace DevkitServer.API.Permissions;
public class UserPermissions : IPermissionHandler, IUserPermissionHandler
{
    private static readonly CachedMulticastEvent<Action<PermissionGroup>> EventGlobalPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(UserPermissions), nameof(GlobalPermissionGroupUpdated));
    private static readonly CachedMulticastEvent<Action<PermissionGroup>> EventGlobalPermissionGroupRegistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(UserPermissions), nameof(GlobalPermissionGroupRegistered));
    private static readonly CachedMulticastEvent<Action<PermissionGroup>> EventGlobalPermissionGroupDeregistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(UserPermissions), nameof(GlobalPermissionGroupDeregistered));
    private static readonly CachedMulticastEvent<Action<Permission>> EventGlobalPermissionRegistered = new CachedMulticastEvent<Action<Permission>>(typeof(UserPermissions), nameof(GlobalPermissionRegistered));
    private static readonly CachedMulticastEvent<Action<Permission>> EventGlobalPermissionDeregistered = new CachedMulticastEvent<Action<Permission>>(typeof(UserPermissions), nameof(GlobalPermissionDeregistered));

    public static event Action<PermissionGroup> GlobalPermissionGroupUpdated
    {
        add => EventGlobalPermissionGroupUpdated.Add(value);
        remove => EventGlobalPermissionGroupUpdated.Remove(value);
    }
    public static event Action<PermissionGroup> GlobalPermissionGroupRegistered
    {
        add => EventGlobalPermissionGroupRegistered.Add(value);
        remove => EventGlobalPermissionGroupRegistered.Remove(value);
    }
    public static event Action<PermissionGroup> GlobalPermissionGroupDeregistered
    {
        add => EventGlobalPermissionGroupDeregistered.Add(value);
        remove => EventGlobalPermissionGroupDeregistered.Remove(value);
    }
    public static event Action<Permission> GlobalPermissionRegistered
    {
        add => EventGlobalPermissionRegistered.Add(value);
        remove => EventGlobalPermissionRegistered.Remove(value);
    }
    public static event Action<Permission> GlobalPermissionDeregistered
    {
        add => EventGlobalPermissionDeregistered.Add(value);
        remove => EventGlobalPermissionDeregistered.Remove(value);
    }

#if SERVER
    protected static readonly CachedMulticastEvent<Action<Permission, ulong, bool>> EventGlobalUserPermissionUpdated = new CachedMulticastEvent<Action<Permission, ulong, bool>>(typeof(UserPermissions), nameof(GlobalUserPermissionUpdated));
    protected static readonly CachedMulticastEvent<Action<PermissionGroup, ulong, bool>> EventGlobalUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, ulong, bool>>(typeof(UserPermissions), nameof(GlobalUserPermissionGroupUpdated));

    public static event Action<Permission, ulong, bool> GlobalUserPermissionUpdated
    {
        add => EventGlobalUserPermissionUpdated.Add(value);
        remove => EventGlobalUserPermissionUpdated.Remove(value);
    }
    public static event Action<PermissionGroup, ulong, bool> GlobalUserPermissionGroupUpdated
    {
        add => EventGlobalUserPermissionGroupUpdated.Add(value);
        remove => EventGlobalUserPermissionGroupUpdated.Remove(value);
    }
#else
    protected static readonly CachedMulticastEvent<Action<Permission, bool>> EventGlobalUserPermissionUpdated = new CachedMulticastEvent<Action<Permission, bool>>(typeof(UserPermissions), nameof(GlobalUserPermissionUpdated));
    protected static readonly CachedMulticastEvent<Action<PermissionGroup, bool>> EventGlobalUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, bool>>(typeof(UserPermissions), nameof(GlobalUserPermissionGroupUpdated));

    public static event Action<Permission, bool> GlobalUserPermissionUpdated
    {
        add => EventGlobalUserPermissionUpdated.Add(value);
        remove => EventGlobalUserPermissionUpdated.Remove(value);
    }
    public static event Action<PermissionGroup, bool> GlobalUserPermissionGroupUpdated
    {
        add => EventGlobalUserPermissionGroupUpdated.Add(value);
        remove => EventGlobalUserPermissionGroupUpdated.Remove(value);
    }
#endif

    internal static readonly NetCallRaw<Permission, bool> SendPermissionState = new NetCallRaw<Permission, bool>((ushort)DevkitServerNetCall.SendPermissionState,
        Permission.ReadPermission, null, Permission.WritePermission, null);
    internal static readonly NetCallRaw<PermissionGroup, bool> SendPermissionGroupState = new NetCallRaw<PermissionGroup, bool>((ushort)DevkitServerNetCall.SendPermissionGroupState,
        PermissionGroup.ReadPermissionGroup, null, PermissionGroup.WritePermissionGroup, null);
    internal static readonly NetCall SendClearPermissions = new NetCall((ushort)DevkitServerNetCall.SendClearPermissions);
    internal static readonly NetCall SendClearPermissionGroups = new NetCall((ushort)DevkitServerNetCall.SendClearPermissionGroups);
#if SERVER
    public static readonly string DefaultFileLocation = Path.Combine(DevkitServerConfig.Directory, "permissions.json");
    public const ushort DataVersion = 0;
#endif
    private static IPermissionHandler _handler = new UserPermissions();
    private static IUserPermissionHandler _userHandler = (IUserPermissionHandler)_handler;
    private static bool _inited;
#if SERVER
    protected static readonly ByteWriter Writer = new ByteWriter(false, 1024);
    protected static readonly ByteReader Reader = new ByteReader();
    private PermissionGroupsConfig.ConfigHost _config = null!;
#endif
    protected UserPermissions() { }
    internal static void InitHandlers()
    {
        Logger.LogDebug("Loading permissions.");
        _inited = true;
        IPermissionHandler handler = _handler;
        IUserPermissionHandler userHandler = _userHandler;
        handler.Init();
        if (!ReferenceEquals(userHandler, handler))
            userHandler.Init();
        Logger.LogDebug("Found: " + _handler.Permissions.Count + " permissions.");
    }
    /// <exception cref="NotSupportedException">Called setter on non-game thread.</exception>
    /// <exception cref="ArgumentNullException"/>
    public static IPermissionHandler Handler
    {
        get => _handler;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            ThreadUtil.assertIsGameThread();

            IPermissionHandler old = Interlocked.Exchange(ref _handler, value);
            IUserPermissionHandler other = UserHandler;
            if (old is IDisposable disp && !ReferenceEquals(other, old))
                disp.Dispose();
            if (value != other && _inited)
                _handler.Init();

            if (old != null)
            {
                old.PermissionDeregistered -= EventGlobalPermissionDeregistered.TryInvoke;
                old.PermissionGroupDeregistered -= EventGlobalPermissionGroupDeregistered.TryInvoke;
                old.PermissionGroupRegistered -= EventGlobalPermissionGroupRegistered.TryInvoke;
                old.PermissionGroupUpdated -= EventGlobalPermissionGroupUpdated.TryInvoke;
                old.PermissionRegistered -= EventGlobalPermissionRegistered.TryInvoke;
            }

            value.PermissionDeregistered += EventGlobalPermissionDeregistered.TryInvoke;
            value.PermissionGroupDeregistered += EventGlobalPermissionGroupDeregistered.TryInvoke;
            value.PermissionGroupRegistered += EventGlobalPermissionGroupRegistered.TryInvoke;
            value.PermissionGroupUpdated += EventGlobalPermissionGroupUpdated.TryInvoke;
            value.PermissionRegistered += EventGlobalPermissionRegistered.TryInvoke;
        }
    }
    /// <exception cref="NotSupportedException">Called setter on non-game thread.</exception>
    /// <exception cref="ArgumentNullException"/>
    public static IUserPermissionHandler UserHandler
    {
        get => _userHandler;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            ThreadUtil.assertIsGameThread();

            IUserPermissionHandler old = Interlocked.Exchange(ref _userHandler, value);
            IPermissionHandler other = Handler;
            if (old is IDisposable disp && !ReferenceEquals(other, old))
                disp.Dispose();
            if (value != other && _inited)
                _userHandler.Init();

            if (old != null)
            {
                old.UserPermissionGroupUpdated -= EventGlobalUserPermissionGroupUpdated.TryInvoke;
                old.UserPermissionUpdated -= EventGlobalUserPermissionUpdated.TryInvoke;
            }

            value.UserPermissionGroupUpdated += EventGlobalUserPermissionGroupUpdated.TryInvoke;
            value.UserPermissionUpdated += EventGlobalUserPermissionUpdated.TryInvoke;
        }
    }
    public static List<Permission> GetDefaultPermissionsFromLoaded()
    {
        List<Permission> perms = new List<Permission>(64) { Permission.SuperuserPermission };
        Logger.LogDebug("Found superuser permission: " + perms[0].Format() + ".");
        Assembly asm = Assembly.GetExecutingAssembly();
        foreach (Assembly assembly in new Assembly[] { asm }.Concat(PluginLoader.Plugins.Select(x => x.Assembly.Assembly).Distinct()))
        {
            bool thisAssembly = asm == assembly;

            foreach (Type type in Accessor.GetTypesSafe(assembly))
            {
                if (type.IsIgnored())
                    continue;

                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (FieldInfo field in fields)
                {
                    if (!field.IsStatic || !typeof(Permission).IsAssignableFrom(field.FieldType))
                        continue;

                    if (!field.TryGetAttributeSafe(out PermissionAttribute attribute) || field.IsIgnored())
                        continue;

                    Permission? perm = (Permission?)field.GetValue(null);
                    if (perm == null) continue;
                    if (!thisAssembly && (perm.Plugin == null || perm.Plugin.Assembly.Assembly != assembly))
                    {
                        IDevkitServerPlugin? plugin2 = null;
                        if (attribute.PluginType != null)
                        {
                            plugin2 = PluginLoader.Plugins.FirstOrDefault(attribute.PluginType.IsInstanceOfType);
                        }

                        plugin2 ??= PluginLoader.FindPluginForMember(field);

                        if (plugin2 == null)
                        {
                            Logger.LogWarning("Permission ignored because of a plugin type error in " +
                                              field.Format() +
                                              ". Either the plugin type is not from the same assembly, " +
                                              "it was not explicitly defined in a plugin with more than one plugin types, " +
                                              "or it defined a type that isn't a loaded plugin type. Try setting the "
                                              + nameof(PermissionAttribute.PluginType).Format() + " field in the "
                                              + typeof(PermissionAttribute).Format() + ".");
                            perm.Plugin = null;
                            continue;
                        }

                        perm.Plugin = plugin2;
                    }
                    else if (thisAssembly)
                    {
                        if (perm is { DevkitServer: false, Core: false })
                            Logger.LogWarning("DevkitServer or Core flag not set on permission at " +
                                              field.Format() + ".");
                        if (perm.Plugin != null)
                            Logger.LogWarning("Plugin property set on permission at " + field.Format() + ".");
                    }

                    Logger.LogDebug("Found permission: " + perm.Format() + ".");
                    perms.Add(perm);
                }
            }
        }

        return perms;
    }
    private static List<Permission> _perms = null!;
    private static IReadOnlyList<Permission> _roPerms = null!;
    private static List<PermissionGroup> _permGrps = null!;
    private static IReadOnlyList<PermissionGroup> _roPermGrps = null!;
#if CLIENT
    private static List<Permission> _clientPerms = null!;
    private static IReadOnlyList<Permission> _roClientPerms = null!;
    private static List<PermissionGroup> _clientPermGrps = null!;
    private static IReadOnlyList<PermissionGroup> _roClientPermGrps = null!;
    IReadOnlyList<Permission> IUserPermissionHandler.Permissions => _roClientPerms;
    IReadOnlyList<PermissionGroup> IUserPermissionHandler.PermissionGroups => _roClientPermGrps;
#endif
    protected readonly CachedMulticastEvent<Action<PermissionGroup>> EventPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(UserPermissions), nameof(PermissionGroupUpdated));
    protected readonly CachedMulticastEvent<Action<PermissionGroup>> EventPermissionGroupRegistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(UserPermissions), nameof(PermissionGroupRegistered));
    protected readonly CachedMulticastEvent<Action<PermissionGroup>> EventPermissionGroupDeregistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(UserPermissions), nameof(PermissionGroupDeregistered));
    protected readonly CachedMulticastEvent<Action<Permission>> EventPermissionRegistered = new CachedMulticastEvent<Action<Permission>>(typeof(UserPermissions), nameof(PermissionRegistered));
    protected readonly CachedMulticastEvent<Action<Permission>> EventPermissionDeregistered = new CachedMulticastEvent<Action<Permission>>(typeof(UserPermissions), nameof(PermissionDeregistered));

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
    public event Action<Permission> PermissionRegistered
    {
        add => EventPermissionRegistered.Add(value);
        remove => EventPermissionRegistered.Remove(value);
    }
    public event Action<Permission> PermissionDeregistered
    {
        add => EventPermissionDeregistered.Add(value);
        remove => EventPermissionDeregistered.Remove(value);
    }
#if SERVER
    protected readonly CachedMulticastEvent<Action<Permission, ulong, bool>> EventUserPermissionUpdated = new CachedMulticastEvent<Action<Permission, ulong, bool>>(typeof(UserPermissions), nameof(UserPermissionUpdated));
    protected readonly CachedMulticastEvent<Action<PermissionGroup, ulong, bool>> EventUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, ulong, bool>>(typeof(UserPermissions), nameof(UserPermissionGroupUpdated));

    public event Action<Permission, ulong, bool> UserPermissionUpdated
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
    protected readonly CachedMulticastEvent<Action<Permission, bool>> EventUserPermissionUpdated = new CachedMulticastEvent<Action<Permission, bool>>(typeof(UserPermissions), nameof(UserPermissionUpdated));
    protected readonly CachedMulticastEvent<Action<PermissionGroup, bool>> EventUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, bool>>(typeof(UserPermissions), nameof(UserPermissionGroupUpdated));

    public event Action<Permission, bool> UserPermissionUpdated
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

    IReadOnlyList<Permission> IPermissionHandler.Permissions => _roPerms;
    IReadOnlyList<PermissionGroup> IPermissionHandler.PermissionGroups => _roPermGrps;
    public virtual void Init()
    {
        _perms = GetDefaultPermissionsFromLoaded();
        _roPerms = _perms.AsReadOnly();
#if CLIENT
        _permGrps = new List<PermissionGroup>();
        _clientPerms = new List<Permission>(24);
        _clientPermGrps = new List<PermissionGroup>(4);
        _roClientPermGrps = _clientPermGrps.AsReadOnly();
        _roClientPerms = _clientPerms.AsReadOnly();
        _roPermGrps = _permGrps.AsReadOnly();
#else
        _config = new PermissionGroupsConfig.ConfigHost();
        Reload();
#endif
        for (int i = 0; i < _perms.Count; ++i)
            EventPermissionRegistered.TryInvoke(_perms[i]);
    }

    public virtual void Reload()
    {
#if SERVER
        _config.ReloadConfig();
        _permGrps = _config.Configuration.Groups ?? new List<PermissionGroup>(0);
        _permGrps.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        _roPermGrps = _permGrps.AsReadOnly();
#endif
    }
    public virtual bool Register(Permission permission)
    {
        for (int i = 0; i < _perms.Count; ++i)
        {
            if (_perms[i].Equals(permission))
                return false;
        }
        EventPermissionRegistered.TryInvoke(permission);
        _perms.Add(permission);
#if SERVER
        PermissionsEx.ReplicateLatePermissionRegistration(permission);
#endif
        Logger.LogInfo("Permission registered: " + permission.Format() + ".");
        return true;
    }

    public virtual bool Register(PermissionGroup group)
    {
        for (int i = 0; i < _permGrps.Count; ++i)
        {
            if (_permGrps[i].Equals(group))
                return false;
        }
        bool added = false;
        for (int i = 0; i < _permGrps.Count; ++i)
        {
            if (_permGrps[i].Priority >= group.Priority)
                continue;
            _permGrps.Insert(i, group);
            added = true;
            break;
        }
        if (!added)
            _permGrps.Add(group);
        EventPermissionGroupRegistered.TryInvoke(group);
#if SERVER
        PermissionsEx.ReplicateLatePermissionGroupRegistration(group);
        _config.SaveConfig();
#endif
        Logger.LogInfo("Permission group registered: " + group.Format() + ".");
        return true;
    }
#if CLIENT
    public virtual bool ReceivePermissionGroupUpdate(PermissionGroup group)
    {
        for (int i = 0; i < _permGrps.Count; ++i)
        {
            if (_permGrps[i].Equals(group))
            {
                PermissionGroup grp = _permGrps[i];
                if (grp.UpdateFrom(group))
                {
                    // re-sort after priority change
                    _permGrps.RemoveAt(i);
                    bool added = false;
                    for (int j = 0; j < _permGrps.Count; ++j)
                    {
                        if (_permGrps[j].Priority >= grp.Priority)
                            continue;
                        _permGrps.Insert(j, grp);
                        added = true;
                        break;
                    }
                    if (!added)
                        _permGrps.Add(grp);
                }

                Logger.LogInfo("Permission group updated: " + grp.Format() + ".");
                EventPermissionGroupUpdated.TryInvoke(grp);
                return true;
            }
        }

        _permGrps.Add(group);
        Logger.LogInfo("Permission group added during update: " + group.Format() + ".");
        EventPermissionGroupUpdated.TryInvoke(group);
        return false;
    }
#endif
    public virtual bool Deregister(Permission permission)
    {
        for (int i = 0; i < _perms.Count; i++)
        {
            if (_perms[i].Equals(permission))
            {
                _perms.RemoveAt(i);
#if SERVER
                PermissionsEx.ReplicatePermissionDeregistration(permission);
#endif
                Logger.LogInfo("Permission deregistered: " + permission.Format() + ".");
                EventPermissionDeregistered.TryInvoke(permission);
                return true;
            }
        }

        return false;
    }

    public virtual bool Deregister(PermissionGroup group)
    {
        for (int i = 0; i < _permGrps.Count; i++)
        {
            if (_permGrps[i].Equals(group))
            {
                _permGrps.RemoveAt(i);
#if SERVER
                PermissionsEx.ReplicatePermissionGroupDeregistration(group);
                _config.SaveConfig();
#endif
                Logger.LogInfo("Permission group deregistered: " + group.Format() + ".");
                EventPermissionGroupDeregistered.TryInvoke(group);
                return true;
            }
        }

        return false;
    }

#if SERVER
    public virtual void AddPermission(ulong user, Permission permission)
    {
        ThreadUtil.assertIsGameThread();

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
        List<Permission> perms2 = new List<Permission>(perms.Count + 1);
        perms2.AddRange(perms);
        perms2.Add(permission);
        SavePermissions(user, perms2);
        EventUserPermissionUpdated.TryInvoke(permission, user, true);
    }

    public virtual void RemovePermission(ulong user, Permission permission)
    {
        ThreadUtil.assertIsGameThread();

        if (UserManager.FromId(user) is { Permissions: { } perms } user2)
        {
            user2.RemovePermission(permission);
            SavePermissions(user2.SteamId.m_SteamID, perms);
            EventUserPermissionUpdated.TryInvoke(permission, user, false);
            return;
        }

        perms = GetPermissions(user, true);
        List<Permission> perms2 = new List<Permission>(perms.Count);
        perms2.AddRange(perms);
        for (int i = perms2.Count - 1; i >= 0; --i)
        {
            if (perms2[i].Equals(permission))
                perms2.RemoveAt(i);
        }
        SavePermissions(user, perms2);
        EventUserPermissionUpdated.TryInvoke(permission, user, false);
    }

    public virtual void ClearPermissions(ulong user)
    {
        ThreadUtil.assertIsGameThread();

        if (UserManager.FromId(user) is { Permissions: not null } user2)
        {
            if (!EventUserPermissionUpdated.IsEmpty)
            {
                foreach (Permission perm in user2.Permissions)
                    EventUserPermissionUpdated.TryInvoke(perm, user, false);
            }
            user2.ClearPermissions();
            SavePermissions(user2.SteamId.m_SteamID, Array.Empty<Permission>());
            return;
        }

        if (!EventUserPermissionUpdated.IsEmpty)
        {
            IReadOnlyList<Permission> perms = GetPermissions(user, true);
            foreach (Permission perm in perms)
                EventUserPermissionUpdated.TryInvoke(perm, user, false);
        }
        SavePermissions(user, Array.Empty<Permission>());
    }

    public virtual IReadOnlyList<Permission> GetPermissions(ulong user, bool forceReload = false)
    {
        lock (Writer)
        {
            if (!forceReload && UserManager.FromId(user) is { Permissions: { } perms })
                return perms;
            string path = DevkitServerUtility.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "Permissions.dat"));
            if (File.Exists(path))
            {
                using FileStream str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Reader.LoadNew(str);
                _ = Reader.ReadUInt16();
                int ct = Reader.ReadInt32();
                List<Permission> list = new List<Permission>(ct);
                for (int i = 0; i < ct; ++i)
                {
                    if (Permission.TryParse(Reader.ReadString(), out Permission perm))
                        list.Add(Handler.TryFindEqualPermission(perm));
                    if (Reader.HasFailed)
                        break;
                }
                return list.AsReadOnly();
            }
            else
            {
                string[] def = DevkitServerConfig.Config.DefaultUserPermissions ?? Array.Empty<string>();
                List<Permission> list = new List<Permission>(def.Length);
                for (int i = 0; i < def.Length; ++i)
                {
                    if (Permission.TryParse(def[i], out Permission permission))
                        list.Add(Handler.TryFindEqualPermission(permission));
                    else
                        Logger.LogWarning("Unknown default permission: " + def[i].Format() + ".");
                }

                SavePermissions(user, list);
                return list.AsReadOnly();
            }
        }
    }
    public virtual void AddPermissionGroup(ulong user, PermissionGroup group)
    {
        ThreadUtil.assertIsGameThread();

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

    public virtual void RemovePermissionGroup(ulong user, PermissionGroup group)
    {
        ThreadUtil.assertIsGameThread();

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

    public virtual void ClearPermissionGroups(ulong user)
    {
        ThreadUtil.assertIsGameThread();

        if (UserManager.FromId(user) is { Permissions: { } } user2)
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

    public virtual IReadOnlyList<PermissionGroup> GetPermissionGroups(ulong user, bool forceReload = false)
    {
        lock (Writer)
        {
            if (!forceReload && UserManager.FromId(user) is { PermissionGroups: { } perms })
                return perms;
            string path = DevkitServerUtility.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "PermissionGroups.dat"));
            if (File.Exists(path))
            {
                using FileStream str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Reader.LoadNew(str);
                _ = Reader.ReadUInt16();
                int ct = Reader.ReadInt32();
                List<PermissionGroup> list = new List<PermissionGroup>(ct);
                for (int i = 0; i < ct; ++i)
                {
                    bool temp = Reader.ReadBool();
                    string id = Reader.ReadString();
                    if (Handler.TryFindPermissionGroup(id, out PermissionGroup group))
                        list.Add(group);
                    else
                    {
                        if (!temp)
                            Logger.LogWarning("Unknown permission group in " + user + "'s permission group save, ignoring.");
                        list.Add(new PermissionGroup(id, id, Color.white, int.MinValue, Array.Empty<GroupPermission>()));
                    }
                    if (Reader.HasFailed)
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
                    if (Handler.TryFindPermissionGroup(def[i], out PermissionGroup group))
                        list.Add(group);
                    else
                        Logger.LogWarning("Unknown default permission group: " + def[i].Format() + ".");
                }

                SavePermissionGroups(user, list);
                return list.AsReadOnly();
            }
        }
    }
    public void SavePermissionGroup(PermissionGroup group, bool priorityChanged)
    {
        if (priorityChanged)
        {
            for (int i = 0; i < _permGrps.Count; ++i)
            {
                if (!_permGrps[i].Equals(group))
                    continue;

                PermissionGroup grp = _permGrps[i];
                // re-sort after priority change
                _permGrps.RemoveAt(i);
                bool added = false;
                for (int j = 0; j < _permGrps.Count; ++j)
                {
                    if (_permGrps[j].Priority >= grp.Priority)
                        continue;
                    _permGrps.Insert(j, grp);
                    added = true;
                    break;
                }

                if (!added)
                    _permGrps.Add(grp);

                break;
            }
        }
        _config.SaveConfig();
        PermissionsEx.ReplicatePermissionGroupUpdate(group);
        EventPermissionGroupUpdated.TryInvoke(group);
    }
    private static void SavePermissions(ulong user, IReadOnlyCollection<Permission> permissions)
    {
        lock (Writer)
        {
            string path = DevkitServerUtility.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "Permissions.dat"));
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !DevkitServerUtility.CheckDirectory(false, dir))
            {
                CommonErrors.LogPlayerSavedataAccessError(dir);
                return;
            }
            using FileStream str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            Writer.Stream = str;
            Writer.Write(DataVersion);
            Writer.Write(permissions.Count);
            foreach (Permission permission in permissions)
                Writer.Write(permission.ToString());
            Writer.Flush();
            str.Flush();
            Writer.Stream = null;
        }
    }
    private static void SavePermissionGroups(ulong user, IReadOnlyCollection<PermissionGroup> groups)
    {
        lock (Writer)
        {
            string path = DevkitServerUtility.GetUserSavedataLocation(user, Path.Combine("DevkitServer", "PermissionGroups.dat"));
            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);
            using FileStream str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            Writer.Stream = str;
            Writer.Write(DataVersion);
            Writer.Write(groups.Count);
            foreach (PermissionGroup group in groups)
            {
                Writer.Write(group.Priority == int.MinValue);
                Writer.Write(group.Id);
            }
            Writer.Flush();
            str.Flush();
            Writer.Stream = null;
        }
    }
#else
    public virtual void ReceivePermissions(IReadOnlyList<Permission> permissions, IReadOnlyList<PermissionGroup> groups)
    {
        if (!EventUserPermissionUpdated.IsEmpty)
        {
            foreach (Permission permission in _clientPerms)
                EventUserPermissionUpdated.TryInvoke(permission, false);
        }
        if (EventUserPermissionGroupUpdated.IsEmpty)
        {
            foreach (PermissionGroup group in _clientPermGrps)
                EventUserPermissionGroupUpdated.TryInvoke(group, false);
        }
        _clientPerms.Clear();
        _clientPerms.AddRange(permissions);
        _clientPermGrps.Clear();
        _clientPermGrps.AddRange(groups);
        if (!EventUserPermissionUpdated.IsEmpty)
        {
            foreach (Permission permission in _clientPerms)
                EventUserPermissionUpdated.TryInvoke(permission, true);
        }
        if (!EventUserPermissionGroupUpdated.IsEmpty)
        {
            foreach (PermissionGroup group in _clientPermGrps)
                EventUserPermissionGroupUpdated.TryInvoke(group, true);
        }
    }

    public virtual void ReceivePermissionState(Permission permission, bool state)
    {
        for (int i = 0; i < _clientPerms.Count; ++i)
        {
            Permission p2 = _clientPerms[i];
            if (p2.Equals(permission))
            {
                if (!state)
                {
                    _clientPerms.RemoveAt(i);
                    EventUserPermissionUpdated.TryInvoke(p2, false);
                }
                return;
            }
        }

        if (!state) return;
        _clientPerms.Add(permission);
        EventUserPermissionUpdated.TryInvoke(permission, true);
    }

    public virtual void ReceivePermissionGroupState(PermissionGroup group, bool state)
    {
        for (int i = 0; i < _clientPermGrps.Count; ++i)
        {
            PermissionGroup p2 = _clientPermGrps[i];
            if (p2.Equals(group))
            {
                if (!state)
                {
                    _clientPermGrps.RemoveAt(i);
                    EventUserPermissionGroupUpdated.TryInvoke(p2, false);
                }
                return;
            }
        }

        if (!state) return;
        bool added = false;
        for (int i = 0; i < _clientPermGrps.Count; ++i)
        {
            if (_clientPermGrps[i].Priority <= group.Priority)
                continue;
            _clientPermGrps.Insert(i, group);
            added = true;
            break;
        }
        if (!added)
            _clientPermGrps.Add(group);
        EventUserPermissionGroupUpdated.TryInvoke(group, true);
    }

    public virtual void ReceiveClearPermissions()
    {
        if (!EventUserPermissionUpdated.IsEmpty)
        {
            foreach (Permission permission in _clientPerms)
                EventUserPermissionUpdated.TryInvoke(permission, false);
        }
        _clientPerms.Clear();
    }
    public virtual void ReceiveClearPermissionGroups()
    {
        if (!EventUserPermissionGroupUpdated.IsEmpty)
        {
            foreach (PermissionGroup group in _clientPermGrps)
                EventUserPermissionGroupUpdated.TryInvoke(group, false);
        }
        _clientPermGrps.Clear();
    }
#endif
}
#if SERVER
public class PermissionGroupsConfig : SchemaConfiguration
{
    public override string SchemaURI => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/permission_groups_schema.json", true);

    [JsonIgnore]
    private static PermissionGroupsConfig? _default;

    [JsonPropertyName("groups")]
    public List<PermissionGroup> Groups { get; set; } = null!;

    public static PermissionGroupsConfig Default => _default ??= new PermissionGroupsConfig
    {
        Groups = new List<PermissionGroup>(4)
        {
            new PermissionGroup("viewer", "Viewer", new Color32(255, 204, 102, 255), 0, Array.Empty<GroupPermission>()),
            new PermissionGroup("terrain_editor", "Terrain Editor", new Color32(51, 204, 51, 255), 1, new GroupPermission[]
            {
                VanillaPermissions.EditHeightmap,
                VanillaPermissions.EditSplatmap,
                VanillaPermissions.EditHoles,
                VanillaPermissions.EditFoliage,
                VanillaPermissions.BakeFoliage,
                VanillaPermissions.BakeFoliageNearby,
                VanillaPermissions.EditObjects,
                VanillaPermissions.EditVolumes,
                VanillaPermissions.EditNodes,
                VanillaPermissions.EditCartographyVolumes,
                VanillaPermissions.BakeCartography,
                VanillaPermissions.EditRoads,
                VanillaPermissions.EditRoadMaterials,
                VanillaPermissions.EditLighting
            }),
            new PermissionGroup("location_builder", "Location Builder", new Color32(255, 255, 153, 255), 1, new GroupPermission[]
            {
                VanillaPermissions.EditHeightmap,
                VanillaPermissions.EditSplatmap,
                VanillaPermissions.EditHoles,
                VanillaPermissions.EditFoliage,
                VanillaPermissions.BakeFoliageNearby,
                VanillaPermissions.EditObjects,
                VanillaPermissions.EditVolumes,
                VanillaPermissions.EditNodes,
                VanillaPermissions.EditCartographyVolumes,
                VanillaPermissions.BakeCartography,
                VanillaPermissions.EditRoads
            }),
            new PermissionGroup("director", "Director", new Color32(51, 204, 255, 255), 2, new GroupPermission[]
            {
                Permission.SuperuserPermission
            })
        }
    };
    internal sealed class ConfigHost : JsonConfigurationFile<PermissionGroupsConfig>
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public override PermissionGroupsConfig Default => PermissionGroupsConfig.Default;
        public ConfigHost() : base(DevkitServerConfig.PermissionGroupsPath) { }
    }
}

#endif
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class PermissionAttribute : PluginIdentifierAttribute { }