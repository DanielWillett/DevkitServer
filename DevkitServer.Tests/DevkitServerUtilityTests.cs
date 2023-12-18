using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DevkitServer.Tests;
/// <summary>Unit tests for <see cref="DevkitServerUtility"/>.</summary>

[TestClass]
public class DevkitServerUtilityTests
{
    [TestMethod]
    [DataRow("STEAM_0:1:153830640")]
    [DataRow("[U:1:307661281]")]
    [DataRow("76561198267927009")]
    [DataRow("1100001125689e1")]
    [DataRow("307661281")]
    public void TestParseCSteamID(string steamIdInput)
    {
        if (!DevkitServerUtility.TryParseSteamId(steamIdInput, out CSteamID steamId))
            Assert.Fail();

        Assert.AreEqual(steamId.m_SteamID, 76561198267927009ul);
    }
    [TestMethod]
    [DataRow("#0066ff99")]
    [DataRow("0066ff99")]
    [DataRow("rgb(0, 102, 255, 153)")]
    [DataRow("(0, 102, 255, 153)")]
    [DataRow("hsv(216, 100, 100, 153)")]
    public void TestParseColor(string colorInput)
    {
        Color value = new Color32(0, 102, 255, 153);
        if (!DevkitServerUtility.TryParseColor(colorInput, out Color color))
            Assert.Fail();


        Assert.That.AreNearlyEqual(value.r, color.r);
        Assert.That.AreNearlyEqual(value.g, color.g);
        Assert.That.AreNearlyEqual(value.b, color.b);
        Assert.That.AreNearlyEqual(value.a, color.a);
    }
    [TestMethod]
    [DataRow("#0066ff")]
    [DataRow("0066ff")]
    [DataRow("rgb(0, 102, 255)")]
    [DataRow("(0, 102, 255)")]
    [DataRow("hsv(216, 100, 100)")]
    public void TestParseColorNoAlpha(string colorInput)
    {
        Color value = new Color32(0, 102, 255, 255);
        if (!DevkitServerUtility.TryParseColor(colorInput, out Color color))
            Assert.Fail();


        Assert.That.AreNearlyEqual(value.r, color.r);
        Assert.That.AreNearlyEqual(value.g, color.g);
        Assert.That.AreNearlyEqual(value.b, color.b);
        Assert.That.AreNearlyEqual(value.a, color.a);
    }
    [TestMethod]
    [DataRow("#0066ff99")]
    [DataRow("0066ff99")]
    [DataRow("rgb(0, 102, 255, 153)")]
    [DataRow("(0, 102, 255, 153)")]
    [DataRow("hsv(216, 100, 100, 153)")]
    public void TestParseColor32(string colorInput)
    {
        Color32 value = new Color32(0, 102, 255, 153);
        if (!DevkitServerUtility.TryParseColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#0066ff")]
    [DataRow("0066ff")]
    [DataRow("rgb(0, 102, 255)")]
    [DataRow("(0, 102, 255)")]
    [DataRow("hsv(216, 100, 100)")]
    public void TestParseColor32NoAlpha(string colorInput)
    {
        Color32 value = new Color32(0, 102, 255, 255);
        if (!DevkitServerUtility.TryParseColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#0066ff99")]
    [DataRow("0066ff99")]
    public void TestParse8HexColor32(string colorInput)
    {
        Color32 value = new Color32(0, 102, 255, 153);
        if (!DevkitServerUtility.TryParseHexColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#0066ff")]
    [DataRow("0066ff")]
    public void TestParse6HexColor32(string colorInput)
    {
        Color32 value = new Color32(0, 102, 255, 255);
        if (!DevkitServerUtility.TryParseHexColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#ac49")]
    [DataRow("ac49")]
    public void TestParse4HexColor32(string colorInput)
    {
        Color32 value = new Color32(170, 204, 68, 153);
        if (!DevkitServerUtility.TryParseHexColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#ac4")]
    [DataRow("ac4")]
    public void TestParse3HexColor32(string colorInput)
    {
        Color32 value = new Color32(170, 204, 68, 255);
        if (!DevkitServerUtility.TryParseHexColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#a9")]
    [DataRow("a9")]
    public void TestParse2HexColor32(string colorInput)
    {
        Color32 value = new Color32(170, 170, 170, 153);
        if (!DevkitServerUtility.TryParseHexColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow("#a")]
    [DataRow("a")]
    public void TestParse1HexColor32(string colorInput)
    {
        Color32 value = new Color32(170, 170, 170, 255);
        if (!DevkitServerUtility.TryParseHexColor32(colorInput, out Color32 color))
            Assert.Fail();

        Assert.AreEqual(value, color);
    }
    [TestMethod]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder\d.exe", true)]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder2\d.exe", false)]
    [DataRow(@"C:\AFolder\BFolder\DFolder", @"C:\AFolder\BFolder\CFolder2\d.exe", false)]
    [DataRow(@"", @"C:\AFolder\BFolder\CFolder2\d.exe", true)]
    [DataRow(@"C:\AFolder\BFolder\CFolder2", @"", false)]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder\..\..\d.exe", false)]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder2\..\..\d.exe", false)]
    [DataRow(@"C:\AFolder\BFolder\DFolder", @"C:\AFolder\BFolder\CFolder2\..\..\d.exe", false)]
    public void TestCheckParent(string relativeTo, string folder, bool expectedMatch)
    {
        bool isParent = FileUtil.IsChildOf(relativeTo, folder);

        Assert.AreEqual(expectedMatch, isParent);
    }
    [TestMethod]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder\d.exe", "d.exe")]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder\DFolder\d.exe", @"DFolder\d.exe")]
    [DataRow(@"C:\AFolder\BFolder\CFolder", @"C:\AFolder\BFolder\CFolder\DFolder\..\d.exe", "d.exe")]
    public void TestGetRelativePath(string relativeTo, string folder, string expected)
    {
        string path = Path.GetRelativePath(relativeTo, folder);

        Assert.AreEqual(expected, path);
    }
}
