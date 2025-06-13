#nullable enable

using JetBrains.SymbolStorage.Impl;
using JetBrains.SymbolStorage.Impl.Tags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class TagUtilTest
  {
    [DataTestMethod]
    [DataRow(true, "flat.txt")]
    [DataRow(true, "pingme.txt")]
    [DataRow(true, "index2.txt")]
    [DataRow(false, "_jb.lowercase")]
    [DataRow(false, "_jb.uppercase")]
    [DataRow(false, "_jb.tags")]
    [DataRow(false, "_jb.tagsx")]
    [DataRow(false, "_jb.tags/abs")]
    [DataRow(false, "_jb.tags/abs/saf")]
    [DataRow(false, "foo.exe/542D574Ec2000/foo.exe")]
    [DataRow(false, "foo.exe/542D574Ec2000/foo.ex_")]
    [DataRow(false, "foo.dll/542D574Ec2000/foo.dll")]
    [DataRow(false, "foo.dll/542D574Ec2000/foo.dl_")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pd_")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow(false, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(false, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(false, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(false, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(false, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    public void StorageFormatFileTest(bool expected, string file)
    {
      Assert.AreEqual(expected, TagUtil.IsStorageFormatFile(file.NormalizeSystem()));
    }

    [DataTestMethod]
    [DataRow(false, "flat.txt")]
    [DataRow(false, "pingme.txt")]
    [DataRow(false, "index2.txt")]
    [DataRow(true, "_jb.lowercase")]
    [DataRow(true, "_jb.uppercase")]
    [DataRow(false, "_jb.tags")]
    [DataRow(false, "_jb.tagsx")]
    [DataRow(false, "_jb.tags/abs")]
    [DataRow(false, "_jb.tags/abs/saf")]
    [DataRow(false, "foo.exe/542D574Ec2000/foo.exe")]
    [DataRow(false, "foo.exe/542D574Ec2000/foo.ex_")]
    [DataRow(false, "foo.dll/542D574Ec2000/foo.dll")]
    [DataRow(false, "foo.dll/542D574Ec2000/foo.dl_")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pd_")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow(false, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(false, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(false, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(false, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(false, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    public void StorageCasingFileTest(bool expected, string file)
    {
      Assert.AreEqual(expected, TagUtil.IsStorageCasingFile(file.NormalizeSystem()));
    }

    [DataTestMethod]
    [DataRow(false, "flat.txt")]
    [DataRow(false, "pingme.txt")]
    [DataRow(false, "index2.txt")]
    [DataRow(false, "_jb.lowercase")]
    [DataRow(false, "_jb.uppercase")]
    [DataRow(false, "_jb.tags")]
    [DataRow(false, "_jb.tagsx")]
    [DataRow(true, "_jb.tags/abs")]
    [DataRow(true, "_jb.tags/abs/saf")]
    [DataRow(false, "foo.exe/542D574Ec2000/foo.exe")]
    [DataRow(false, "foo.exe/542D574Ec2000/foo.ex_")]
    [DataRow(false, "foo.dll/542D574Ec2000/foo.dll")]
    [DataRow(false, "foo.dll/542D574Ec2000/foo.dl_")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pd_")]
    [DataRow(false, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow(false, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(false, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(false, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(false, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(false, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    public void TagFileTest(bool expected, string file)
    {
      Assert.AreEqual(expected, TagUtil.IsTagFile(file.NormalizeSystem()));
    }

    [DataTestMethod]
    [DataRow(false, "flat.txt")]
    [DataRow(false, "pingme.txt")]
    [DataRow(false, "index2.txt")]
    [DataRow(false, "_jb.lowercase")]
    [DataRow(false, "_jb.uppercase")]
    [DataRow(false, "_jb.tags/abs")]
    [DataRow(false, "_jb.tags/abs/saf")]
    [DataRow(true, "foo.exe/542D574Ec2000/foo.exe")]
    [DataRow(true, "foo.exe/542D574Ec2000/foo.ex_")]
    [DataRow(true, "foo.dll/542D574Ec2000/foo.dll")]
    [DataRow(true, "foo.dll/542D574Ec2000/foo.dl_")]
    [DataRow(true, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb")]
    [DataRow(true, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pd_")]
    [DataRow(true, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow(true, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(true, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(true, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(true, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(true, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    public void DataFileTest(bool expected, string file)
    {
      Assert.AreEqual(expected, TagUtil.IsDataFile(file.NormalizeSystem()));
    }
  }
}