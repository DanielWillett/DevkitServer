using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevkitServer.Tests;
/// <summary>Unit tests for <see cref="DevkitServerUtility"/>.</summary>

[TestClass]
public class FormattingUtilTests
{
    [TestMethod]
    public void TestSpaceProperCaseStringNormal()
    {
        string result = FormattingUtil.SpaceProperCaseString("DevkitServer");

        Assert.AreEqual("Devkit Server", result);
    }

    [TestMethod]
    public void TestSpaceProperCaseStringStartingAcronym()
    {
        string result = FormattingUtil.SpaceProperCaseString("AAABbb");

        Assert.AreEqual("AAA Bbb", result);
    }

    [TestMethod]
    public void TestSpaceProperCaseStringFullAcronym()
    {
        string result = FormattingUtil.SpaceProperCaseString("AAAA");

        Assert.AreEqual("AAAA", result);
    }

    [TestMethod]
    public void TestSpaceProperCaseStringFullAcronymWithSpaces()
    {
        string result = FormattingUtil.SpaceProperCaseString("UI EXT MANAGER");

        Assert.AreEqual("UI EXT MANAGER", result);
    }

    [TestMethod]
    public void TestSpaceProperCaseStringMiddleAcronym()
    {
        string result = FormattingUtil.SpaceProperCaseString("AaaaAABb");

        Assert.AreEqual("Aaaa AA Bb", result);
    }

    [TestMethod]
    public void TestSpaceProperCaseStringEndingAcronym()
    {
        string result = FormattingUtil.SpaceProperCaseString("AaAA");

        Assert.AreEqual("Aa AA", result);
    }
    [TestMethod]
    public void TestSpaceProperCaseUnderscoresNormal()
    {
        string result = FormattingUtil.SpaceProperCaseString("Devkit_Server");

        Assert.AreEqual("Devkit Server", result);
    }
}
