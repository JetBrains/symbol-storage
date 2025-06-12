#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class PathTreeTest
  {
    [TestMethod]
    public void TreeBuildTest()
    {
      var treeBuilder = PathTree.BuildNew();
      var next = treeBuilder.Root.GetOrInsert("abc");
      next = next.GetOrInsert("bcd".AsSpan());
      next = next.GetOrInsert("cde".AsSpan());
      next.AddFile("data.exe");

      
      var tree = treeBuilder.Build();
      Assert.IsTrue(tree.Root.HasChildren);
      
      var child = tree.Root.Lookup("abc".AsSpan());
      Assert.IsNotNull(child);
      child = child.Lookup("bcd");
      Assert.IsNotNull(child);
      child = child.Lookup("cde".AsSpan());
      Assert.IsNotNull(child);
      Assert.IsTrue(child.HasFiles);
      Assert.IsFalse(child.HasChildren);
      Assert.AreEqual("data.exe", child.GetFiles().Single());
    }

    [TestMethod]
    public void TreeRecursiveLookupTest()
    {
      var treeBuilder = PathTree.BuildNew();
      var next = treeBuilder.Root.GetOrInsert("abc");
      next = next.GetOrInsert("bcd".AsSpan());
      next = next.GetOrInsert("cde".AsSpan());
      next.AddFile("data.exe");

      next = treeBuilder.AddPathRecursive($"abc{Path.DirectorySeparatorChar}www");
      next.AddFile("data2.exe");
      
      var tree = treeBuilder.Build();
      Assert.IsTrue(tree.Root.HasChildren);

      var child = tree.LookupPathRecursive($"abc{Path.DirectorySeparatorChar}bcd{Path.DirectorySeparatorChar}cde");
      Assert.IsNotNull(child);
      Assert.AreEqual("data.exe", child.GetFiles().Single());
      
      child = tree.LookupPathRecursive($"abc{Path.DirectorySeparatorChar}www");
      Assert.IsNotNull(child);
      Assert.AreEqual("data2.exe", child.GetFiles().Single());
    }

    [TestMethod]
    public void TreeParallelBuildTest()
    {
      List<string> paths = new List<string>();
      for (char c1 = 'a'; c1 <= 'z'; c1++)
      {
        for (char c2 = 'a'; c2 <= 'z'; c2++)
        {
          for (char c3 = 'a'; c3 <= 'z'; c3++)
          {
            paths.Add($"{c1}{Path.DirectorySeparatorChar}{c2}{Path.DirectorySeparatorChar}{c3}{Path.DirectorySeparatorChar}data.exe");
          }
        }
      }

      var treeBuilder = PathTree.BuildNew();

      Parallel.ForEach(paths, new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) }, item =>
      {
        var dir = Path.GetDirectoryName(item.AsSpan());
        var node = treeBuilder.Root.AddPathRecursive(dir);
        node.AddFile(Path.GetFileName(item));
      });
      
      var tree = treeBuilder.Build();

      foreach (var item in paths)
      {
        var dir = Path.GetDirectoryName(item.AsSpan());
        var child = tree.LookupPathRecursive(dir);
        Assert.IsNotNull(child);
        Assert.AreEqual("data.exe", child.GetFiles().Single());
      }
    }
  }
}