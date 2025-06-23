using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Commands;
using JetBrains.SymbolStorage.Impl.Storages;
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
      var filePath = SymbolStoragePath.Combine("abc", "bcd", "cde", "data.exe");
      var next = treeBuilder.Root.GetOrInsert("abc");
      next = next.GetOrInsert("bcd".AsSpan());
      next = next.GetOrInsert("cde".AsSpan());
      next.AddFile(filePath);

      
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
      Assert.AreEqual(filePath, child.GetFiles().Single());
    }

    [TestMethod]
    public void TreeRecursiveLookupTest()
    {
      var treeBuilder = PathTree.BuildNew();
      var filePath1 = SymbolStoragePath.Combine("abc", "bcd", "cde", "data.exe");
      var next = treeBuilder.Root.GetOrInsert("abc");
      next = next.GetOrInsert("bcd".AsSpan());
      next = next.GetOrInsert("cde".AsSpan());
      next.AddFile(filePath1);

      var filePath2 = SymbolStoragePath.Combine("abc", "www", "data2.exe");
      next = treeBuilder.AddPathRecursive(SymbolStoragePath.Combine("abc", "www"));
      next.AddFile(filePath2);

      var filePath3 = new SymbolStoragePath("data3.exe");
      next = treeBuilder.AddPathRecursive("", '/');
      next.AddFile(filePath3);
      
      var tree = treeBuilder.Build();
      Assert.IsTrue(tree.Root.HasChildren);

      var child = tree.LookupPathRecursive(SymbolStoragePath.Combine("abc", "bcd", "cde"));
      Assert.IsNotNull(child);
      Assert.AreEqual(filePath1, child.GetFiles().Single());
      
      child = tree.LookupPathRecursive(SymbolStoragePath.Combine("abc", "www").AsRef());
      Assert.IsNotNull(child);
      Assert.AreEqual(filePath2, child.GetFiles().Single());
      
      child = tree.LookupPathRecursive("", '/');
      Assert.IsNotNull(child);
      Assert.AreEqual(filePath3, child.GetFiles().Single());
      Assert.AreEqual(filePath3, tree.Root.GetFiles().Single());
    }

    [TestMethod]
    public void TreeParallelBuildTest()
    {
      List<SymbolStoragePath> paths = new List<SymbolStoragePath>();
      for (char c1 = 'a'; c1 <= 'z'; c1++)
      {
        for (char c2 = 'a'; c2 <= 'z'; c2++)
        {
          for (char c3 = 'a'; c3 <= 'z'; c3++)
          {
            paths.Add(SymbolStoragePath.Combine(c1.ToString(), c2.ToString(), c3.ToString(), "data.exe"));
          }
        }
      }

      var treeBuilder = PathTree.BuildNew();

      Parallel.ForEach(paths, new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) }, item =>
      {
        var dir = SymbolStoragePath.GetDirectoryName(item.AsRef());
        var node = treeBuilder.Root.AddPathRecursive(dir);
        node.AddFile(item);
      });
      
      var tree = treeBuilder.Build();

      foreach (var item in paths)
      {
        var dir = SymbolStoragePath.GetDirectoryName(item.AsRef());
        var child = tree.LookupPathRecursive(dir);
        Assert.IsNotNull(child);
        Assert.AreEqual(item, child.GetFiles().Single());
      }
    }
  }
}