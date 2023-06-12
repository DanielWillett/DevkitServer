using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDG.Unturned;

namespace DevkitServer.Tests;
internal static class AssertEx
{
    public static void AreNearlyEqual(this Assert impl, float expected, float actual)
    {
        if (!MathfEx.IsNearlyEqual(expected, actual))
            throw new AssertFailedException($"Failed to assert IsNearlyEqual: Expected: <{expected}>, Actual: <{actual}>.");
    }
}
