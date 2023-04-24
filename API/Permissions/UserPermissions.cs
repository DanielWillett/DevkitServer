using DevkitServer.Multiplayer.Networking;
using DevkitServer.Plugins;
using System.Reflection;
#if SERVER
using DevkitServer.Configuration;
using DevkitServer.Multiplayer;
using DevkitServer.Util.Encoding;
#endif

namespace DevkitServer.API.Permissions;
public static class UserPermissions
{
    public static readonly NetCallRaw<Permission, bool> SendPermissionState = new NetCallRaw<Permission, bool>((ushort)NetCalls.SendPermissionState,
        Permission.ReadPermission, null, Permission.WritePermission, null);
    public static readonly NetCall SendClearPermissions = new NetCall((ushort)NetCalls.SendClearPermissions);
#if SERVER
    public static readonly string DefaultFileLocation = Path.Combine(DevkitServerConfig.FilePath, "permissions.json");
    public const ushort DataVersion = 0;
#endif
    private static IPermissionHandler _handler = new DefaultPermissionHandler();
    private static IPlayerPermissionHandler _playerHandler = (IPlayerPermissionHandler)_handler;
    private static bool _inited;
    internal static void InitHandlers()
    {
        Logger.LogDebug("Loading permissions.");
        _inited = true;
        IPermissionHandler handler = _handler;
        IPlayerPermissionHandler playerHandler = _playerHandler;
        handler.Init();
        if (!ReferenceEquals(playerHandler, handler))
            playerHandler.Init();
        Logger.LogDebug("Found: " + _handler.Permissions.Count + " permissions.");
    }
    /// <exception cref="NotSupportedException">Called setter on non-game thread.</exception>
    public static IPermissionHandler Handler
    {
        get => _handler;
        set
        {
            ThreadUtil.assertIsGameThread();

            IPermissionHandler old = Interlocked.Exchange(ref _handler, value);
            IPlayerPermissionHandler other = PlayerHandler;
            if (old is IDisposable disp && !ReferenceEquals(other, old))
                disp.Dispose();
            if (value != other && _inited)
                _handler.Init();
        }
    }
    /// <exception cref="NotSupportedException">Called setter on non-game thread.</exception>
    public static IPlayerPermissionHandler PlayerHandler
    {
        get => _playerHandler;
        set
        {
            ThreadUtil.assertIsGameThread();

            IPlayerPermissionHandler old = Interlocked.Exchange(ref _playerHandler, value);
            IPermissionHandler other = Handler;
            if (old is IDisposable disp && !ReferenceEquals(other, old))
                disp.Dispose();
            if (value != other && _inited)
                _playerHandler.Init();
        }
    }
    public static List<Permission> GetDefaultPermissionsFromLoaded()
    {
        List<Permission> perms = new List<Permission>();

        Assembly asm = Assembly.GetExecutingAssembly();
        foreach (Assembly assembly in new Assembly[] { asm }.Concat(PluginLoader.Plugins.Select(x => x.Assembly).Distinct()))
        {
            bool thisAssembly = asm == assembly;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            foreach (Type type in types)
            {
                if (Attribute.IsDefined(type, typeof(IgnoreAttribute)))
                    continue;
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                PermissionAttribute? typeAttr = Attribute.GetCustomAttribute(type, typeof(PermissionAttribute)) as PermissionAttribute;
                foreach (FieldInfo field in fields)
                {
                    if (field.IsStatic && typeof(Permission).IsAssignableFrom(field.FieldType))
                    {
                        if (Attribute.GetCustomAttribute(field, typeof(PermissionAttribute)) is PermissionAttribute && !Attribute.IsDefined(field, typeof(IgnoreAttribute)))
                        {
                            Permission? perm = (Permission?)field.GetValue(null);
                            if (perm == null) continue;
                            if (!thisAssembly && (perm.Plugin == null || perm.Plugin.Assembly != assembly))
                            {
                                IDevkitServerPlugin? plugin2 = PluginLoader.FindPluginForMember(field);

                                if (plugin2 == null)
                                {
                                    Logger.LogWarning("Permission ignored because of a plugin type error in " + field.Format() +
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
                                if (!perm.DevkitServer)
                                    Logger.LogWarning("DevkitServer flag not set on permission at " + field.Format() + ".");
                                if (perm.Core)
                                    Logger.LogWarning("Core flag set on permission at " + field.Format() + ".");
                                if (perm.Plugin != null)
                                    Logger.LogWarning("Plugin property set on permission at " + field.Format() + ".");
                            }

                            Logger.LogDebug("Found permission: " + perm.Format() + ".");
                            perms.Add(perm);
                        }
                    }
                }
            }
        }

        return perms;
    }
    public static void RegisterPermission(Permission permission)
    {
        Handler.LateRegister(permission);
    }
    private class DefaultPermissionHandler : IPermissionHandler, IPlayerPermissionHandler
    {
#if SERVER
        protected static readonly ByteWriter Writer = new ByteWriter(false, 1024);
        protected static readonly ByteReader Reader = new ByteReader();
#endif
        private static List<Permission> _perms = null!;
        private static IReadOnlyList<Permission> _roPerms = null!;
#if CLIENT
        private static List<Permission> _clientPerms = null!;
        private static IReadOnlyList<Permission> _roClientPerms = null!;
        IReadOnlyList<Permission> IPlayerPermissionHandler.Permissions => _clientPerms;
#endif
#if SERVER
        public event Action<Permission, ulong, bool>? PermissionUpdated;
#else
        public event Action<Permission, bool>? PermissionUpdated;
#endif
        IReadOnlyList<Permission> IPermissionHandler.Permissions => _roPerms;
        public void Init()
        {
            _perms = GetDefaultPermissionsFromLoaded();
            _roPerms = _perms.AsReadOnly();
#if CLIENT
            _clientPerms = new List<Permission>(24);
            _roClientPerms = _clientPerms;
#endif
        }

        public void Reload() { }
        public void LateRegister(Permission permission) => _perms.Add(permission);

#if SERVER
        public void AddPermission(ulong player, Permission permission)
        {
            ThreadUtil.assertIsGameThread();

            if (UserManager.FromId(player) is { Permissions: { } perms } user)
            {
                user.AddPermission(permission);
                SavePermissions(user.SteamId.m_SteamID, perms);
                PermissionUpdated?.Invoke(permission, player, true);
                return;
            }

            perms = GetPermissions(player, true);
            for (int i = 0; i < perms.Count; ++i)
            {
                if (perms[i].Equals(permission))
                    return;
            }
            List<Permission> perms2 = new List<Permission>(perms.Count + 1);
            perms2.AddRange(perms);
            perms2.Add(permission);
            SavePermissions(player, perms2);
            PermissionUpdated?.Invoke(permission, player, true);
        }

        public void RemovePermission(ulong player, Permission permission)
        {
            ThreadUtil.assertIsGameThread();

            if (UserManager.FromId(player) is { Permissions: { } perms } user)
            {
                user.RemovePermission(permission);
                SavePermissions(user.SteamId.m_SteamID, perms);
                PermissionUpdated?.Invoke(permission, player, false);
                return;
            }

            perms = GetPermissions(player, true);
            List<Permission> perms2 = new List<Permission>(perms.Count);
            perms2.AddRange(perms);
            for (int i = perms2.Count - 1; i >= 0; --i)
            {
                if (perms2[i].Equals(permission))
                    perms2.RemoveAt(i);
            }
            SavePermissions(player, perms2);
            PermissionUpdated?.Invoke(permission, player, false);
        }

        public void ClearPermissions(ulong player)
        {
            ThreadUtil.assertIsGameThread();

            if (UserManager.FromId(player) is { Permissions: { } } user)
            {
                if (PermissionUpdated != null)
                {
                    foreach (Permission perm in user.Permissions)
                        PermissionUpdated.Invoke(perm, player, false);
                }
                user.ClearPermissions();
                SavePermissions(user.SteamId.m_SteamID, Array.Empty<Permission>());
                return;
            }

            if (PermissionUpdated != null)
            {
                IReadOnlyList<Permission> perms = GetPermissions(player, true);
                foreach (Permission perm in perms)
                    PermissionUpdated.Invoke(perm, player, false);
            }
            SavePermissions(player, Array.Empty<Permission>());
        }

        public IReadOnlyList<Permission> GetPermissions(ulong player, bool forceReload = false)
        {
            lock (Writer)
            {
                if (!forceReload && UserManager.FromId(player) is { Permissions: { } perms })
                    return perms;
                string path = DevkitServerUtility.GetPlayerSavedataLocation(player, Path.Combine("DevkitServer", "Permissions.dat"));
                if (File.Exists(path))
                {
                    using FileStream str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    ByteReader reader = new ByteReader();
                    reader.LoadNew(str);
                    _ = reader.ReadUInt16();
                    int ct = reader.ReadInt32();
                    List<Permission> list = new List<Permission>(ct);
                    for (int i = 0; i < ct; ++i)
                    {
                        if (Permission.TryParse(reader.ReadString(), out Permission perm))
                            list.Add(perm);
                        if (reader.HasFailed)
                            break;
                    }
                    return list.AsReadOnly();
                }

                return new List<Permission>(0).AsReadOnly();
            }
        }
        private static void SavePermissions(ulong player, IReadOnlyCollection<Permission> permissions)
        {
            lock (Reader)
            {
                string path = DevkitServerUtility.GetPlayerSavedataLocation(player, Path.Combine("DevkitServer", "Permissions.dat"));
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
#else
        public void ReceivePermissions(IReadOnlyList<Permission> permissions)
        {
            if (PermissionUpdated != null)
            {
                foreach (Permission permission in _clientPerms)
                    PermissionUpdated.Invoke(permission, false);
            }
            _clientPerms.Clear();
            _clientPerms.AddRange(permissions);
            if (PermissionUpdated != null)
            {
                foreach (Permission permission in _clientPerms)
                    PermissionUpdated.Invoke(permission, true);
            }
        }

        public void ReceivePermissionState(Permission permission, bool state)
        {
            for (int i = 0; i < _clientPerms.Count; ++i)
            {
                if (_clientPerms[i].Equals(permission))
                {
                    if (!state)
                    {
                        _clientPerms.RemoveAt(i);
                        PermissionUpdated?.Invoke(permission, false);
                    }
                    return;
                }
            }

            _clientPerms.Add(permission);
            PermissionUpdated?.Invoke(permission, true);
        }
        public void ReceiveClearPermissions()
        {
            if (PermissionUpdated != null)
            {
                foreach (Permission permission in _clientPerms)
                    PermissionUpdated.Invoke(permission, false);
            }
            _clientPerms.Clear();
        }
#endif
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class PermissionAttribute : PluginIdentifierAttribute { }