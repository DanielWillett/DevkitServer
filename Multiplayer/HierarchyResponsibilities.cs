﻿using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer;
/// <summary>
/// Stores the user that's responsible for placing an <see cref="IDevkitHierarchyItem"/>. On the client it only stores a set of the ones you placed.
/// </summary>
public static class HierarchyResponsibilities
{
    private const ushort DataVersion = 0;
    private const string Source = "HIERARCHY RESPONSIBILITIES";
    public static readonly string SavePath = Path.Combine(DevkitServerConfig.LevelDirectory, "hierarchy-responsibilities.dat");
#if SERVER
    private static readonly Dictionary<uint, ulong> Responsibilities = new Dictionary<uint, ulong>(256);
#else
    private static readonly HashSet<uint> Responsibilities = new HashSet<uint>(32);
#endif
    private static readonly ByteWriter Writer = new ByteWriter(false);
    private static readonly ByteReader Reader = new ByteReader { LogOnError = false };

#if SERVER
    public static ulong GetPlacer(uint instanceId) => Responsibilities.TryGetValue(instanceId, out ulong placer) ? placer : 0ul;
    public static bool IsPlacer(uint instanceId, ulong user) => Responsibilities.TryGetValue(instanceId, out ulong placer) && placer == user;
#else
    public static bool IsPlacer(uint instanceId) => Responsibilities.Contains(instanceId);
#endif

    /// <summary>Reload from config file.</summary>
    public static void Reload(bool resaveIfNeeded = true)
    {
        ThreadUtil.assertIsGameThread();

        Responsibilities.Clear();
        if (File.Exists(SavePath))
        {
            bool save = false;
            using FileStream stream = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Reader.LoadNew(stream);
            Reader.Skip(sizeof(ushort)); // version
            bool client = Reader.ReadBool();
#if SERVER
            if (client)
#else
            if (!client)
#endif
            {
                Logger.LogWarning($"Invalid hierarchy responsiblities file provided: {SavePath.Format(false)}. This file was created on the wrong platform (client vs. server).", method:Source);
                return;
            }
            while (!Reader.HasFailed)
            {
                uint instanceId = Reader.ReadUInt32();
#if SERVER
                ulong steam64 = Reader.ReadUInt64();
#endif
                if (instanceId == 0
#if SERVER
                    || !steam64.UserSteam64()
#endif
                    )
                {
                    save = !Reader.HasFailed;
                    continue;
                }

                if (HierarchyUtil.FindItemIndex(instanceId) < 0)
                {
                    Logger.LogInfo($"[{Source}] Unable to find saved hierarchy item: {instanceId}.");
                    save = true;
                    continue;
                }
#if SERVER
                if (Responsibilities.ContainsKey(instanceId))
                {
                    save = true;
                    Responsibilities[instanceId] = steam64;
                }
                else Responsibilities.Add(instanceId, steam64);
#else
                if (Responsibilities.Contains(instanceId))
                    save = true;
                else Responsibilities.Add(instanceId);
#endif
            }
            Logger.LogInfo($"[{Source}] Read {Responsibilities.Count.Format()} hierarchy responsiblities in table stored at {SavePath.Format()}." + (save ? " Saving updates." : string.Empty));
            if (save && resaveIfNeeded)
                Save();
        }
        else
        {
            Logger.LogDebug($"[{Source}] Loaded new hierarchy responsiblities table.");
        }
    }
    /// <summary>Remove <paramref name="instanceId"/> from save.</summary>
    public static void Remove(uint instanceId, bool save = true)
    {
        ThreadUtil.assertIsGameThread();

        if (Responsibilities.Remove(instanceId) && save)
            Save();
    }
    /// <summary>Set <paramref name="instanceId"/> in save. Sets to owned on client and sets owner on server.</summary>
    public static void Set(uint instanceId
#if SERVER
        , ulong steam64
#endif
        , bool save = true)
    {
        ThreadUtil.assertIsGameThread();

#if SERVER
        if (!steam64.UserSteam64())
        {
            if (Responsibilities.Remove(instanceId) && save)
                Save();
            return;
        }
        if (Responsibilities.TryGetValue(instanceId, out ulong existing))
        {
            if (existing != steam64)
            {
                Responsibilities[instanceId] = steam64;
                if (save)
                    Save();
            }
            return;
        }

        Responsibilities.Add(instanceId, existing);
#else
        if (Responsibilities.Contains(instanceId))
            return;
        Responsibilities.Add(instanceId);
#endif
        if (!save) return;
        using FileStream stream = new FileStream(SavePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        Writer.Stream = stream;
        Writer.Write(instanceId);
#if SERVER
        Writer.Write(steam64);
#endif
        Writer.Flush();
        Writer.Stream = null;
    }
    /// <summary>Save all responsibilities.</summary>
    public static void Save()
    {
        ThreadUtil.assertIsGameThread();

        if (Responsibilities.Count > 0)
        {
            List<uint>? remove = null;
            DevkitServerUtility.CheckDirectory(false, Path.GetDirectoryName(SavePath)!);
            Thread.BeginCriticalRegion();
            try
            {
                using FileStream stream = new FileStream(SavePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                Writer.Stream = stream;
                Writer.Write(DataVersion);
#if SERVER
                foreach (KeyValuePair<uint, ulong> pair in Responsibilities)
                {
                    uint instanceId = pair.Key;
#else
                foreach (uint instanceId in Responsibilities)
                {
#endif
                    if (HierarchyUtil.FindItemIndex(instanceId) < 0)
                    {
                        (remove ??= new List<uint>()).Add(instanceId);
                        continue;
                    }
                    Writer.Write(instanceId);
#if SERVER
                    Writer.Write(pair.Value);
#endif
                }
                if (remove != null)
                {
                    for (int i = 0; i < remove.Count; ++i)
                        Responsibilities.Remove(remove[i]);

                    Logger.LogDebug($"[{Source}] Removed {remove.Count.Format()} expired hierarchy responsibilities: {string.Join(",", remove.Select(x => x.Format()))}");
                }
                Writer.Flush();
            }
            finally
            {
                Thread.EndCriticalRegion();
            }
        }
        else if (File.Exists(SavePath))
            File.Delete(SavePath);
    }
}