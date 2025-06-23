using System;
using System.IO;
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
    [DataRow("a//b")]
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
    [DataRow("a/c", "a", "c")]
    [DataRow("a", "a")]
    public void GetPathComponentsTest(string path, params string[] expectedParts)
    {
      Assert.IsTrue(expectedParts.SequenceEqual(new SymbolStoragePath(path).GetPathComponents()));

      int index = 0;
      foreach (var range in new SymbolStoragePath(path).AsRef().GetPathComponents())
      {
        Assert.IsTrue(index < expectedParts.Length);
        Assert.IsTrue(path[range].SequenceEqual(expectedParts[index]));
        index++;
      }
    }

    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("abc/def")]
    [DataRow("abc/def/www")]
    public void CreationFromSystemPathTest(string path)
    {
      var systemPath = path.Replace('/', Path.DirectorySeparatorChar);
      var normalizedPath = SymbolStoragePath.FromSystemPath(systemPath);
      Assert.AreEqual(path, normalizedPath.Path);
    }
    
    [DataTestMethod]
    [DataRow("abc", "", "abc")]
    [DataRow("abc/def", "abc", "def")]
    [DataRow("abc/def/www", "abc", "def/www")]
    public void CreationFromRelativeSystemPathTest(string path, string basePath, string expected)
    {
      var systemPath = path.Replace('/', Path.DirectorySeparatorChar);
      var normalizedPath = SymbolStoragePath.FromSystemPath(systemPath, basePath);
      Assert.AreEqual(expected, normalizedPath.Path);
    }
    
    [TestMethod]
    public void CombineSymbolStoragePathsTest()
    {
      Assert.AreEqual(new SymbolStoragePath("abc/def"), SymbolStoragePath.Combine(new SymbolStoragePath("abc"), new SymbolStoragePath("def")));
      Assert.AreEqual(new SymbolStoragePath("abc/def/www"), SymbolStoragePath.Combine(new SymbolStoragePath("abc"), new SymbolStoragePath("def"), new SymbolStoragePath("www")));
      Assert.AreEqual(new SymbolStoragePath("abc/def"), SymbolStoragePath.Combine("abc", "def"));
      Assert.AreEqual(new SymbolStoragePath("abc/def/www/http"), SymbolStoragePath.Combine("abc", "def", "www", "http"));
    }
    
    [DataTestMethod]
    [DataRow("abc", null)]
    [DataRow("abc/def", "abc")]
    [DataRow("abc/def/www", "abc/def")]
    public void GetDirectoryNameTest(string path, string? expected)
    {
      var symbolStoragePath = new SymbolStoragePath(path);
      var dirName = SymbolStoragePath.GetDirectoryName(symbolStoragePath);
      Assert.AreEqual(expected, dirName?.Path);
      
      var dirNameSpan = SymbolStoragePath.GetDirectoryName(symbolStoragePath.AsRef());
      Assert.IsTrue((expected == null && dirNameSpan.IsEmpty) || dirNameSpan == (expected ?? ""));
    }
    
    [DataTestMethod]
    [DataRow("abc", "abc")]
    [DataRow("abc/def", "def")]
    [DataRow("abc/def/www", "www")]
    public void GetFileNameTest(string path, string expected)
    {
      var symbolStoragePath = new SymbolStoragePath(path);
      var fileName = SymbolStoragePath.GetFileName(symbolStoragePath);
      Assert.AreEqual(expected, fileName.Path);
      
      var fileNameSpan = SymbolStoragePath.GetFileName(symbolStoragePath.AsRef());
      Assert.IsTrue(fileNameSpan == expected);
    }
    
    [DataTestMethod]
    [DataRow("abc", "")]
    [DataRow("abc/def.EXE", ".EXE")]
    [DataRow("abc/def/www.db_", ".db_")]
    [DataRow("foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb", ".pdb")]
    [DataRow("foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so", ".so")]
    [DataRow("_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug", ".debug")]
    [DataRow("foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib", ".dylib")]
    [DataRow("_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf", ".dwarf")]
    [DataRow("foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs", ".cs")]
    public void GetExtensionTest(string path, string expected)
    {
      var symbolStoragePath = new SymbolStoragePath(path);
      var ext = SymbolStoragePath.GetExtension(symbolStoragePath);
      Assert.AreEqual(expected, ext);
      
      var extSpan = SymbolStoragePath.GetExtension(symbolStoragePath.AsRef());
      Assert.IsTrue(extSpan.SequenceEqual(expected.AsSpan()));
    }
    
    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("abc/def.EXE")]
    [DataRow("abc/def/www.db_")]
    [DataRow("foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow("foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow("_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow("foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow("_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow("foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    public void ToLowerToUpperTest(string path)
    {
      var symbolStoragePath = new SymbolStoragePath(path);
      Assert.IsTrue(symbolStoragePath.ToUpper() == path.ToUpperInvariant());
      Assert.IsTrue(symbolStoragePath.ToLower() == path.ToLowerInvariant());
    }

    [TestMethod]
    public void EqualityTest()
    {
      var path1 = new SymbolStoragePath("1");
      var path1Eq = new SymbolStoragePath(new string(new char[] { '1' }));
      var path2 = new SymbolStoragePath(new string(new char[] { '2' }));
      Assert.IsTrue(path1 == path1Eq);
      Assert.IsTrue(path1 == path1Eq.AsRef());
      Assert.IsTrue(path1.AsRef() == path1Eq);
      Assert.IsTrue(path1.AsRef() == path1Eq.AsRef());
      Assert.IsTrue(path1 == "1");
      Assert.IsTrue(path1.AsRef() == "1");
      Assert.AreEqual(0, path1.CompareTo(path1Eq));
      
      Assert.IsTrue(path1 != path2);
      Assert.IsTrue(path1 != path2.AsRef());
      Assert.IsTrue(path1.AsRef() != path2);
      Assert.IsTrue(path1.AsRef() != path2.AsRef());
      Assert.IsTrue(path1 != "2");
      Assert.IsTrue(path1.AsRef() != "2");
      Assert.AreEqual(-1, path1.CompareTo(path2));
    }
  }
}