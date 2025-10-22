using DevkitServer.API.Logging;
using DevkitServer.Configuration;
using DevkitServer.Util;
using Newtonsoft.Json;
using System;
using System.Text.Json;

namespace DanielWillett.LevelObjectIcons.Configuration;
public class JsonConfigurationFile<TConfig> where TConfig : class, new()
{
    private readonly DevkitServer.API.JsonConfigurationFile<TConfig> _implementation;

    public event Action? OnRead
    {
        add => _implementation.OnRead += value;
        remove => _implementation.OnRead -= value;
    }
    public virtual TConfig? Default => null;
    public TConfig Configuration { get => _implementation.Configuration; set => _implementation.Configuration = value; }
    public JsonSerializerSettings? SerializerOptions
    {
        get => JsonHelper.ToNewtonsoft(_implementation.SerializerOptions);
        set => _implementation.SerializerOptions = JsonHelper.FromNewtonsoft(value) ?? DevkitServerConfig.SerializerSettings;
    }
    public string File { get => _implementation.File; set => _implementation.File = value; }
    public bool ReadOnlyReloading { get => _implementation.ReadOnlyReloading; set => _implementation.ReadOnlyReloading = value; }
    public JsonConfigurationFile(string file)
    {
        _implementation = new InternalJsonConfigWrapper<TConfig>(this, file);
    }
    internal void CallOnReload() => OnReload();
    protected virtual void OnReload() { }
    public void ReloadConfig() => _implementation.ReloadConfig();
    public void SaveConfig() => _implementation.SaveConfig();
}

internal class InternalJsonConfigWrapper<TConfig> : DevkitServer.API.JsonConfigurationFile<TConfig> where TConfig : class, new()
{
    private readonly JsonConfigurationFile<TConfig> _owner;
    public override TConfig? Default => _owner.Default;
    public InternalJsonConfigWrapper(JsonConfigurationFile<TConfig> owner, string file) : base(file)
    {
        _owner = owner;
        OnRead += ReadHandler;
    }

    private void ReadHandler()
    {
        try
        {
            _owner.CallOnReload();
        }
        catch (Exception ex)
        {
            LOILogger.Logger.LogError("JSON CONFIG", $"Exception in {nameof(OnReload).Colorize(ConsoleColor.White)} after reading {typeof(TConfig).Format()} config at {File.Format(false)}.");
            LOILogger.Logger.LogError("JSON CONFIG", ex);
        }
    }
}

file static class JsonHelper
{
    public static JsonSerializerSettings? ToNewtonsoft(JsonSerializerOptions? stj)
    {
        if (stj == null) return null;
        return new JsonSerializerSettings
        {
            Formatting = stj.WriteIndented ? Formatting.Indented : Formatting.None,
            MaxDepth = stj.MaxDepth == 0 ? null : stj.MaxDepth
        };
    }
    public static JsonSerializerOptions? FromNewtonsoft(JsonSerializerSettings? newtonsoft)
    {
        if (newtonsoft == null) return null;
        return new JsonSerializerOptions
        {
            WriteIndented = newtonsoft.Formatting == Formatting.Indented,
            MaxDepth = newtonsoft.MaxDepth.GetValueOrDefault()
        };
    }
}