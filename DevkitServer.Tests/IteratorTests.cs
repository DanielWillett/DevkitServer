using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using DevkitServer.API.Iterators;

namespace DevkitServer.Tests;
/// <summary>Unit tests for all iterators.</summary>

[TestClass]
public class IteratorTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TestDistanceListIterator3D(bool desc)
    {
        Vector3 center = new Vector3(8f, 2f, 4f);

        List<DistanceListIteratorTestStorage> list = new List<DistanceListIteratorTestStorage>
        {
            new DistanceListIteratorTestStorage(Vector3.zero, 0),
            new DistanceListIteratorTestStorage(Vector3.zero, 1),
            new DistanceListIteratorTestStorage(new Vector3(1f, 2f, 3f), 2),
            new DistanceListIteratorTestStorage(Vector3.zero, 3),
            new DistanceListIteratorTestStorage(new Vector3(3f, 4f, 5f), 4),
            new DistanceListIteratorTestStorage(Vector3.zero, 5),
            new DistanceListIteratorTestStorage(new Vector3(12f, 5f, 8f), 6),
            new DistanceListIteratorTestStorage(new Vector3(1f, 2f, 3f), 7),
            new DistanceListIteratorTestStorage(new Vector3(-float.NaN, 2f, 3f), 8),
            new DistanceListIteratorTestStorage(new Vector3(8f, 2f, 4f), 9),
            new DistanceListIteratorTestStorage(new Vector3(8f, 2f, 4f), 10),
            new DistanceListIteratorTestStorage(new Vector3(12f, 5f, 8f), 11),
            new DistanceListIteratorTestStorage(new Vector3(8f, 2f, 4f), 12),
            new DistanceListIteratorTestStorage(new Vector3(12f, 5f, 8f), 13),
        };
        int[] expected = !desc ? new int[]
        {
            9,
            10,
            12,
            4,
            6,
            11,
            13,
            2,
            7,
            0,
            1,
            3,
            5,
            8
        } : new int[]
        {
            0,
            1,
            3,
            5,
            2,
            7,
            6,
            11,
            13,
            4,
            9,
            10,
            12,
            8
        };
        List<DistanceListIteratorTestStorage> outList = new List<DistanceListIteratorTestStorage>();
        foreach (DistanceListIteratorTestStorage storage in new DistanceListIterator<DistanceListIteratorTestStorage>(list, x => x.Position, center, desc))
        {
            Console.WriteLine(storage.Position + " #" + storage.Index + " Dist: (" + (center - storage.Position).magnitude + " m)");
            System.Diagnostics.Debug.WriteLine(storage.Position + " #" + storage.Index + " Dist: (" + (center - storage.Position).magnitude + " m)");
            outList.Add(storage);
        }
        Console.WriteLine("Expected:");
        for (int i = 0; i < list.Count; ++i)
        {
            Console.WriteLine(list[expected[i]].Position + " #" + expected[i] + " Dist: (" + (center - list[expected[i]].Position).magnitude + " m)");
        }

        Console.WriteLine("Sort output:");
        foreach (DistanceListIteratorTestStorage storage in desc ? list.OrderByDescending(x => (center - x.Position).sqrMagnitude) : list.OrderBy(x => (center - x.Position).sqrMagnitude))
        {
            Console.WriteLine(storage.Position + " #" + storage.Index + " Dist: (" + (center - storage.Position).magnitude + " m)");
        }

        Assert.AreEqual(list.Count, outList.Count);
        
        for (int i = 0; i < list.Count; ++i)
        {
            Assert.AreEqual(expected[i], outList[i].Index);
        }
    }
    private struct DistanceListIteratorTestStorage
    {
        public Vector3 Position { get; }
        public int Index { get; }
        public DistanceListIteratorTestStorage(Vector3 position, int index)
        {
            Position = position;
            Index = index;
        }
    }
}
