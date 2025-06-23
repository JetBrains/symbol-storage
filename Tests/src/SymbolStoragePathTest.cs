using System;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.SymbolStorage.Impl;
using JetBrains.SymbolStorage.Impl.Storages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class SymbolStoragePathTest
  {
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("/a")]
    [DataRow("a/")]
    [DataRow("\\")]
    [DataRow("\\a")]
    [DataRow("a\\")]
    [DataRow("a\\b")]
    public void IncorrectPathDetectionTest(string? path)
    {
      Assert.Throws<ArgumentException>(() =>
      {
        SymbolStoragePath.ValidatePathCorrectness(path);
      });

      if (path != null)
      {
        Assert.Throws<ArgumentException>(() =>
        {
          var symbolStoragePath = new SymbolStoragePath(path);
          Assert.AreEqual(path, symbolStoragePath.Path);
        });
      }
    }
    
    
    [DataTestMethod]
    [DataRow("a/b/c", "a", "b", "c")]
    [DataRow("a//c", "a", "", "c")]
    public void GetPathComponentsTest(string path, params string[] expectedParts)
    {
      Assert.IsTrue(expectedParts.SequenceEqual(new SymbolStoragePath(path).GetPathComponents()));
    }
  }
}