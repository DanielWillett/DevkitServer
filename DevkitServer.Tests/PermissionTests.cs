using System;
using System.IO;
using System.Linq;
using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Logging.Loggers;
using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevkitServer.Tests;

[TestClass]
public class PermissionTests
{
    private static IDevkitServerLogger _logger;
    
    private const string LongPluginId = "testtesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttesttest";
    [ClassInitialize]
    public static void Setup(TestContext context)
    {
        try
        {
            Logger.InitLogger();
            _logger = new CoreLogger(nameof(PermissionTests));

            TestHelpers.SetupMainThread();
            TestHelpers.SetupFormatProvider();

            PluginLoader.RegisterPlugin(new TestPlugin("a", "A"));
            PluginLoader.RegisterPlugin(new TestPlugin("plugin1", "Plugin1"));
            PluginLoader.RegisterPlugin(new TestPlugin("plugin2", "Plugin2"));
            PluginLoader.RegisterPlugin(new TestPlugin("plugin3", "Plugin3"));
            PluginLoader.RegisterPlugin(new TestPlugin("plugin4", "Plugin4"));
            PluginLoader.RegisterPlugin(new TestPlugin("plugin:5", "Plugin5"));
            PluginLoader.RegisterPlugin(new TestPlugin(LongPluginId, "Test Long Plugin"));
        }
        catch (Exception ex)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            path = Path.Combine(path, "log.txt");
            File.WriteAllText(path, ex.ToString());
            context.WriteLine(ex.ToString());
            throw;
        }
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        foreach (IDevkitServerPlugin plugin in PluginLoader.Plugins.ToList())
            PluginLoader.DeregisterPlugin(plugin);
    }

    [TestMethod]
    [DataRow("devkitserver::context.test")]
    [DataRow("plugin2::context.test.test2")]
    [DataRow("unturned::context.test.test2")]
    [DataRow(LongPluginId + "::context.test.test2")]
    [DataRow("plugin4::context.test:test2")]
    [DataRow("a::b")]
    public void ParseBasicPermissionLeaf(string leafStr)
    {
        PermissionLeaf leaf = PermissionLeaf.Parse(leafStr);

        Assert.AreEqual(leafStr, leaf.ToString());
    }

    [TestMethod]
    [DataRow("context.test")]
    [DataRow("plugin2::*")]
    [DataRow("plugin5::context.test")]
    [DataRow("devkitserver::context.*")]
    [DataRow("plugin4::context.test::test2")]
    [DataRow("devkitserver::*")]
    [DataRow("devkitserver::")]
    [DataRow("::context.test")]
    [DataRow("")]
    [DataRow("*")]
    [DataRow(null)]
    [DataRow("::")]
    public void FailParseBasicPermissionLeaf(string leafStr)
    {
        Assert.ThrowsException<FormatException>(() =>
        {
            PermissionLeaf leaf = PermissionLeaf.Parse(leafStr);

            _logger.LogInfo(nameof(FailParseBasicPermissionLeaf), leaf.Format());
        });
    }

    [TestMethod]
    [DataRow("devkitserver::context.test")]
    [DataRow("plugin2::context.test.test2")]
    [DataRow("unturned::context.test.test2")]
    [DataRow(LongPluginId + "::context.test.test2")]
    [DataRow("a::b")]
    [DataRow("devkitserver::context.test")]
    [DataRow("*")]
    [DataRow("plugin2::context.test.test2.*")]
    [DataRow("unturned::*")]
    [DataRow("a::b.*")]
    [DataRow("plugin4::context.test:test2")]
    [DataRow("plugin:5::context.test:test2")]
    [DataRow("+devkitserver::context.test")]
    [DataRow("+plugin2::context.test.test2")]
    [DataRow("+unturned::context.test.test2")]
    [DataRow("+" + LongPluginId + "::context.test.test2")]
    [DataRow("+a::b")]
    [DataRow("+devkitserver::context.test")]
    [DataRow("+*")]
    [DataRow("+plugin2::context.test.test2.*")]
    [DataRow("+unturned::*")]
    [DataRow("+a::b.*")]
    [DataRow("+plugin4::context.test:test2")]
    [DataRow("+plugin:5::context.test:test2")]
    [DataRow("-devkitserver::context.test")]
    [DataRow("-plugin2::context.test.test2")]
    [DataRow("-unturned::context.test.test2")]
    [DataRow("-" + LongPluginId + "::context.test.test2")]
    [DataRow("-a::b")]
    [DataRow("-devkitserver::context.test")]
    [DataRow("-*")]
    [DataRow("-plugin2::context.test.test2.*")]
    [DataRow("-unturned::*")]
    [DataRow("-a::b.*")]
    [DataRow("-plugin4::context.test:test2")]
    [DataRow("+plugin:5::context.test:test2")]
    public void ParseBasicPermissionBranch(string branchStr)
    {
        PermissionBranch branch = PermissionBranch.Parse(branchStr);
        _logger.LogInfo(nameof(ParseBasicPermissionBranch), $"{branchStr.Format()} -> {branch.Format()} (lvl {branch.WildcardLevel.Format()}).");

        if (branchStr.Length > 0 && branchStr[0] == '+')
            branchStr = branchStr[1..];

        Assert.AreEqual(branchStr, branch.ToString());
    }

    [TestMethod]
    [DataRow("context.test")]
    [DataRow("plugin5::context.test")]
    [DataRow("plugin4::context.test::test2")]
    [DataRow("plugin::5::context.test:test2")]
    [DataRow("devkitserver::")]
    [DataRow("::context.test")]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("::")]
    public void FailParseBasicPermissionBranch(string branchStr)
    {
        Assert.ThrowsException<FormatException>(() =>
        {
            PermissionBranch branch = PermissionBranch.Parse(branchStr);

            _logger.LogInfo(nameof(FailParseBasicPermissionBranch), branch.Format());
        });
    }

    [TestMethod]
    [DataRow("devkitserver::context.test", "devkitserver::context.test")]
    [DataRow("devkitserver::context.*", "devkitserver::context.test")]
    [DataRow("devkitserver::*", "devkitserver::context.test")]
    [DataRow("*", "devkitserver::context.test")]
    [DataRow("unturned::context.test.test.test.test.test.test.*", "unturned::context.test.test.test.test.test.test.leaf")]
    [DataRow("unturned::context.test", "unturned::context.test")]
    [DataRow("unturned::context.*", "unturned::context.test")]
    [DataRow("unturned::*", "unturned::context.test")]
    [DataRow("*", "unturned::context.test")]
    [DataRow("plugin2::context.test", "plugin2::context.test")]
    [DataRow("plugin2::context.*", "plugin2::context.test")]
    [DataRow("plugin2::*", "plugin2::context.test")]
    [DataRow("*", "plugin2::context.test")]
    [DataRow(LongPluginId + "::context.*", LongPluginId + "::context.test")]
    public void ContainsLeafTests(string branchStr, string leafStr)
    {
        PermissionBranch branch = PermissionBranch.Parse(branchStr);
        PermissionLeaf leaf = PermissionLeaf.Parse(leafStr);

        Assert.IsTrue(branch.Valid);
        Assert.IsTrue(leaf.Valid);

        Assert.IsTrue(branch.Contains(leaf));
    }

    [TestMethod]
    [DataRow("devkitserver::context.test", "unturned::context.test")]
    [DataRow("devkitserver::context.test", "devkitserver::context.test.test2")]
    [DataRow("devkitserver::context.test", "plugin1::context.test.test2")]
    [DataRow("unturned::context.test", "devkitserver::context.test")]
    [DataRow("unturned::context.test", "unturned::context.test.test2")]
    [DataRow("unturned::context.test", "plugin1::context.test.test2")]
    [DataRow("plugin1::context.test", "devkitserver::context.test")]
    [DataRow("plugin1::context.test", "plugin1::context.test.test2")]
    [DataRow("plugin1::context.test", "unturned::context.test.test2")]
    [DataRow("plugin1::*", "plugin2::context.test.test2")]
    [DataRow("unturned::*", "devkitserver::context.test.test2")]
    [DataRow(LongPluginId + "::context.*", LongPluginId + "::ctx.test")]
    public void NotContainsLeafTests(string branchStr, string leafStr)
    {
        PermissionBranch branch = PermissionBranch.Parse(branchStr);
        PermissionLeaf leaf = PermissionLeaf.Parse(leafStr);

        Assert.IsTrue(branch.Valid);
        Assert.IsTrue(leaf.Valid);

        Assert.IsFalse(branch.Contains(leaf));
    }

    [TestMethod]
    [DataRow("devkitserver::context.test")]
    [DataRow("unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow(LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow("plugin1::a")]
    [DataRow("plugin2::test.plugin")]
    [DataRow(LongPluginId + "::test.plugin")]
    public void TestLeafIO(string leafStr)
    {
        PermissionLeaf leaf = PermissionLeaf.Parse(leafStr);
        _logger.LogInfo(nameof(TestLeafIO), leaf.Format());
        Assert.IsTrue(leaf.Valid);

        ByteWriter writer = new ByteWriter(64);
        ByteReader reader = new ByteReader();

        PermissionLeaf.Write(writer, leaf);

        reader.LoadNew(writer.ToArray());
        _logger.LogDebug(nameof(TestLeafIO), Environment.NewLine + FormattingUtil.GetBytesDec(reader.InternalBuffer!, 8));

        Assert.AreEqual(leaf, PermissionLeaf.Read(reader));
    }

    [TestMethod]
    [DataRow("devkitserver::context.test")]
    [DataRow("unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow(LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow("plugin1::a")]
    [DataRow("plugin2::test.plugin")]
    [DataRow(LongPluginId + "::test.plugin")]
    [DataRow("devkitserver::context.*")]
    [DataRow("unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.*")]
    [DataRow(LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.*")]
    [DataRow("plugin1::*")]
    [DataRow("plugin2::test.*")]
    [DataRow(LongPluginId + "::test.*")]
    [DataRow("*")]
    [DataRow("unturned::*")]
    [DataRow("-devkitserver::context.test")]
    [DataRow("-unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow("-" + LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow("-plugin1::a")]
    [DataRow("-plugin2::test.plugin")]
    [DataRow("-" + LongPluginId + "::test.plugin")]
    [DataRow("-devkitserver::context.*")]
    [DataRow("-unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.*")]
    [DataRow("-" + LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.*")]
    [DataRow("-plugin1::*")]
    [DataRow("-plugin2::test.*")]
    [DataRow("-" + LongPluginId + "::test.*")]
    [DataRow("-*")]
    [DataRow("-unturned::*")]
    [DataRow("+devkitserver::context.test")]
    [DataRow("+unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow("+" + LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test")]
    [DataRow("+plugin1::a")]
    [DataRow("+plugin2::test.plugin")]
    [DataRow("+" + LongPluginId + "::test.plugin")]
    [DataRow("+devkitserver::context.*")]
    [DataRow("+unturned::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.*")]
    [DataRow("+" + LongPluginId + "::context.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.test.*")]
    [DataRow("+plugin1::*")]
    [DataRow("+plugin2::test.*")]
    [DataRow("+" + LongPluginId + "::test.*")]
    [DataRow("+*")]
    [DataRow("+unturned::*")]
    public void TestBranchIO(string branchStr)
    {
        PermissionBranch branch = PermissionBranch.Parse(branchStr);
        _logger.LogInfo(nameof(TestBranchIO), branch.Format());
        Assert.IsTrue(branch.Valid);

        ByteWriter writer = new ByteWriter(64);
        ByteReader reader = new ByteReader();

        PermissionBranch.Write(writer, branch);

        reader.LoadNew(writer.ToArray());
        _logger.LogDebug(nameof(TestBranchIO), Environment.NewLine + FormattingUtil.GetBytesDec(reader.InternalBuffer!, 8));

        Assert.AreEqual(branch, PermissionBranch.Read(reader));
    }

    [API.Ignore]
    private class TestPlugin : CoreLogger, IDevkitServerPlugin
    {
        public string PermissionPrefix { get; set; }
        public string Name { get; }
        public string MenuName => Name;
        public string DataDirectory => throw new NotImplementedException();
        public string LocalizationDirectory => throw new NotImplementedException();
        public string CommandLocalizationDirectory => throw new NotImplementedException();
        public PluginAssembly Assembly { get; set; } = null!;
        public Local Translations => throw new NotImplementedException();
        public bool DeveloperMode => true;
        public TestPlugin(string permissionPrefix, string name) : base(name)
        {
            PermissionPrefix = permissionPrefix;
            Name = name;
        }

        public void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray)
        {
            Console.WriteLine(message);
        }
        public void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan)
        {
            Console.WriteLine(message);
        }
        public void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow)
        {
            Console.WriteLine(message);
        }
        public void LogError(string message, ConsoleColor color = ConsoleColor.Red)
        {
            Console.WriteLine(message);
        }
        public void LogError(Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        public void Load() { }
        public void Unload() { }

        public string Source => string.Empty;
    }
}
