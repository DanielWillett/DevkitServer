using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace DevkitServer.Tests;

[TestClass]
public class ParseWorldTimeTests
{
    [TestMethod]
    [DataRow("0:0", 0, 0)]
    [DataRow("00:00", 0, 0)]
    [DataRow("000:000", 0, 0)]
    [DataRow("12:00", 12, 0)]
    [DataRow("12:59", 12, 59)]
    [DataRow("23:59", 23, 59)]
    [DataRow("23:59:59", 23, 59)]

    [DataRow("11:59 AM", 11, 59)]
    [DataRow("11:59:00 AM", 11, 59)]
    [DataRow("11:59 PM", 23, 59)]
    [DataRow("11:59:00 PM", 23, 59)]

    [DataRow("3", 3, 0)]

    [DataRow("3 AM", 3, 0)]
    [DataRow("3AM", 3, 0)]
    [DataRow("3 PM", 15, 0)]
    [DataRow("3PM", 15, 0)]

    [DataRow("12 PM", 12, 0)]
    [DataRow("12 AM", 0, 0)]
    public void TestTimeParse(string time, int hrs, int mins)
    {
        Assert.IsTrue(LightingUtil.TryParseTime(time, CultureInfo.InvariantCulture, out uint hours, out uint minutes));

        Assert.AreEqual((uint)hrs, hours);
        Assert.AreEqual((uint)mins, minutes);
    }

    [TestMethod]
    [DataRow("24:00")]
    [DataRow("-10:00")]
    [DataRow("10:-10")]
    [DataRow("3:60")]
    [DataRow("25:61")]
    [DataRow("0 AM")]
    [DataRow("0:00 AM")]
    [DataRow("0 PM")]
    [DataRow("0:00 PM")]
    public void TestOutOfRangeTimeParse(string time)
    {
        Assert.IsFalse(LightingUtil.TryParseTime(time, CultureInfo.InvariantCulture, out uint hours, out uint minutes));
    }

}