using System;
using System.IO;
using System.Linq;
using JetBrains.SymbolStorage.Impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class PathUtilTest
  {
    [DataTestMethod]
    [DataRow("")]
    [DataRow("a/b/c", "a", "b", "c")]
    [DataRow("a//c", "a", "", "c")]
    [DataRow("//", "", "", "")]
    public void GetPathComponentsTest(string path, params string[] expectedParts)
    {
      Assert.IsTrue(expectedParts.SequenceEqual(path.NormalizeSystem().GetPathComponents()));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("a/b")]
    [DataRow("\\")]
    [DataRow("\\a")]
    [DataRow("a\\")]
    public void CheckSystemFileTest(string path)
    {
      var path2 = path?.Replace('\\', Path.DirectorySeparatorChar);
      try
      {
        Assert.IsNotNull(path2.CheckSystemFile());
        Assert.Fail();
      }
      catch (ArgumentException)
      {
      }
    }
    
    [DataTestMethod]
    [DataRow(StorageFormat.Normal, "a")]
    [DataRow(StorageFormat.Normal, "a/b/c/d")]
    [DataRow(StorageFormat.Normal, "foo.exe/542D57x4Ec2000/foo.exe")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5a2dx63b6cc4b1/foo.pdb")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5ax2d63b6cc4bFFFFFFFF/foo.pdb")]
    public void ErrorTest(object storageFormat, string path)
    {
      Assert.AreEqual(PathUtil.ValidateAndFixErrors.Error, path.NormalizeSystem().ValidateAndFixDataPath((StorageFormat) storageFormat, out _));
    }

    [DataTestMethod]
    [DataRow(StorageFormat.Normal, "foo.exe/542D574Ec2000/foo.exe")]
    [DataRow(StorageFormat.Normal, "foo.exe/542D574Ec2000/foo.ex_")]
    [DataRow(StorageFormat.Normal, "foo.dll/542D574Ec2000/foo.dll")]
    [DataRow(StorageFormat.Normal, "foo.dll/542D574Ec2000/foo.dl_")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pd_")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow(StorageFormat.Normal, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(StorageFormat.Normal, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(StorageFormat.Normal, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(StorageFormat.Normal, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(StorageFormat.Normal, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    [DataRow(StorageFormat.LowerCase, "foo.exe/542d574ec2000/foo.exe")]
    [DataRow(StorageFormat.LowerCase, "foo.exe/542d574ec2000/foo.ex_")]
    [DataRow(StorageFormat.LowerCase, "foo.dll/542d574ec2000/foo.dll")]
    [DataRow(StorageFormat.LowerCase, "foo.dll/542d574ec2000/foo.dl_")]
    [DataRow(StorageFormat.LowerCase, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pdb")]
    [DataRow(StorageFormat.LowerCase, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1/foo.pd_")]
    [DataRow(StorageFormat.LowerCase, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bffffffff/foo.pdb")]
    [DataRow(StorageFormat.LowerCase, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(StorageFormat.LowerCase, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(StorageFormat.LowerCase, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(StorageFormat.LowerCase, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(StorageFormat.LowerCase, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    [DataRow(StorageFormat.UpperCase, "FOO.EXE/542D574EC2000/FOO.EXE")]
    [DataRow(StorageFormat.UpperCase, "FOO.EXE/542D574EC2000/FOO.EX_")]
    [DataRow(StorageFormat.UpperCase, "FOO.DLL/542D574EC2000/FOO.DLL")]
    [DataRow(StorageFormat.UpperCase, "FOO.DLL/542D574EC2000/FOO.DL_")]
    [DataRow(StorageFormat.UpperCase, "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4B1/FOO.PDB")]
    [DataRow(StorageFormat.UpperCase, "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4B1/FOO.PD_")]
    [DataRow(StorageFormat.UpperCase, "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4BFFFFFFFF/FOO.PDB")]
    [DataRow(StorageFormat.UpperCase, "FOO.SO/ELF-BUILDID-180A373D6AFBABF0EB1F09BE1BC45BD796A71085/FOO.SO")]
    [DataRow(StorageFormat.UpperCase, "_.DEBUG/ELF-BUILDID-SYM-180A373D6AFBABF0EB1F09BE1BC45BD796A71085/_.DEBUG")]
    [DataRow(StorageFormat.UpperCase, "FOO.DYLIB/MACH-UUID-497B72F6390A44FC878E5A2D63B6CC4B/FOO.DYLIB")]
    [DataRow(StorageFormat.UpperCase, "_.DWARF/MACH-UUID-SYM-497B72F6390A44FC878E5A2D63B6CC4B/_.DWARF")]
    [DataRow(StorageFormat.UpperCase, "FOO.CS/SHA1-497B72F6390A44FC878E5A2D63B6CC4B0C2D9984/FOO.CS")]
    [DataRow(StorageFormat.Normal, "foo.exe/542D574Ec2000")]
    [DataRow(StorageFormat.Normal, "foo.dll/542D574Ec2000")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1")]
    [DataRow(StorageFormat.Normal, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF")]
    [DataRow(StorageFormat.Normal, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.Normal, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.Normal, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.Normal, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.Normal, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984")]
    [DataRow(StorageFormat.LowerCase, "foo.exe/542d574ec2000")]
    [DataRow(StorageFormat.LowerCase, "foo.dll/542d574ec2000")]
    [DataRow(StorageFormat.LowerCase, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4b1")]
    [DataRow(StorageFormat.LowerCase, "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bffffffff")]
    [DataRow(StorageFormat.LowerCase, "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.LowerCase, "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.LowerCase, "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.LowerCase, "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.LowerCase, "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984")]
    [DataRow(StorageFormat.UpperCase, "FOO.EXE/542D574EC2000")]
    [DataRow(StorageFormat.UpperCase, "FOO.DLL/542D574EC2000")]
    [DataRow(StorageFormat.UpperCase, "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4B1")]
    [DataRow(StorageFormat.UpperCase, "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4BFFFFFFFF")]
    [DataRow(StorageFormat.UpperCase, "FOO.SO/ELF-BUILDID-180A373D6AFBABF0EB1F09BE1BC45BD796A71085")]
    [DataRow(StorageFormat.UpperCase, "_.DEBUG/ELF-BUILDID-SYM-180A373D6AFBABF0EB1F09BE1BC45BD796A71085")]
    [DataRow(StorageFormat.UpperCase, "FOO.DYLIB/MACH-UUID-497B72F6390A44FC878E5A2D63B6CC4B")]
    [DataRow(StorageFormat.UpperCase, "_.DWARF/MACH-UUID-SYM-497B72F6390A44FC878E5A2D63B6CC4B")]
    [DataRow(StorageFormat.UpperCase, "FOO.CS/SHA1-497B72F6390A44FC878E5A2D63B6CC4B0C2D9984")]
    public void OkTest(object storageFormat, string path)
    {
      Assert.AreEqual(PathUtil.ValidateAndFixErrors.Ok, path.NormalizeSystem().ValidateAndFixDataPath((StorageFormat) storageFormat, out _));
    }

    [DataTestMethod]
    [DataRow(StorageFormat.Normal, "fOO.EXe/542d574Ec2000/FOo.eXE", "foo.exe/542D574Ec2000/foo.exe")]
    [DataRow(StorageFormat.Normal, "fOO.EXe/542d574Ec2000/FOo.eX_", "foo.exe/542D574Ec2000/foo.ex_")]
    [DataRow(StorageFormat.Normal, "fOO.DLl/542d574EC2000/FOo.dLL", "foo.dll/542D574Ec2000/foo.dll")]
    [DataRow(StorageFormat.Normal, "fOO.DLl/542d574EC2000/FOo.dL_", "foo.dll/542D574Ec2000/foo.dl_")]
    [DataRow(StorageFormat.LowerCase, "fOO.EXe/542d574Ec2000/FOo.eXE", "foo.exe/542d574ec2000/foo.exe")]
    [DataRow(StorageFormat.LowerCase, "fOO.EXe/542d574Ec2000/FOo.eX_", "foo.exe/542d574ec2000/foo.ex_")]
    [DataRow(StorageFormat.LowerCase, "fOO.DLl/542d574EC2000/FOo.dLL", "foo.dll/542d574ec2000/foo.dll")]
    [DataRow(StorageFormat.LowerCase, "fOO.DLl/542d574EC2000/FOo.dL_", "foo.dll/542d574ec2000/foo.dl_")]
    [DataRow(StorageFormat.UpperCase, "fOO.EXe/542d574Ec2000/FOo.eXE", "FOO.EXE/542D574EC2000/FOO.EXE")]
    [DataRow(StorageFormat.UpperCase, "fOO.EXe/542d574Ec2000/FOo.eX_", "FOO.EXE/542D574EC2000/FOO.EX_")]
    [DataRow(StorageFormat.UpperCase, "fOO.DLl/542d574EC2000/FOo.dLL", "FOO.DLL/542D574EC2000/FOO.DLL")]
    [DataRow(StorageFormat.UpperCase, "fOO.DLl/542d574EC2000/FOo.dL_", "FOO.DLL/542D574EC2000/FOO.DL_")]
    [DataRow(StorageFormat.Normal, "fOO.PDb/497B72F6390A44FC878e5A2D63b6cc4bFFFFFFFF/fOO.Pdb", "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF/foo.pdb")]
    [DataRow(StorageFormat.LowerCase, "fOO.PDb/497B72F6390A44FC878e5A2D63b6cc4bFFFFFFFF/fOO.Pdb", "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bffffffff/foo.pdb")]
    [DataRow(StorageFormat.UpperCase, "fOO.PDb/497B72F6390A44FC878e5A2D63b6cc4bFFFFFFFF/fOO.Pdb", "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4BFFFFFFFF/FOO.PDB")]
    [DataRow(StorageFormat.Normal, "foO.SO/eLF-BUIldid-180a373D6AFBABF0EB1f09be1bc45bd796A71085/FoO.So", "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(StorageFormat.LowerCase, "foO.SO/eLF-BUIldid-180a373D6AFBABF0EB1f09be1bc45bd796A71085/FoO.So", "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085/foo.so")]
    [DataRow(StorageFormat.UpperCase, "foO.SO/eLF-BUIldid-180a373D6AFBABF0EB1f09be1bc45bd796A71085/FoO.So", "FOO.SO/ELF-BUILDID-180A373D6AFBABF0EB1F09BE1BC45BD796A71085/FOO.SO")]
    [DataRow(StorageFormat.Normal, "_.deBUG/elF-BUildid-sYM-180A373d6afbabf0EB1F09be1bc45bd796a71085/_.dEBug", "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(StorageFormat.LowerCase, "_.deBUG/elF-BUildid-sYM-180A373d6afbabf0EB1F09be1bc45bd796a71085/_.dEBug", "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085/_.debug")]
    [DataRow(StorageFormat.UpperCase, "_.deBUG/elF-BUildid-sYM-180A373d6afbabf0EB1F09be1bc45bd796a71085/_.dEBug", "_.DEBUG/ELF-BUILDID-SYM-180A373D6AFBABF0EB1F09BE1BC45BD796A71085/_.DEBUG")]
    [DataRow(StorageFormat.Normal, "foO.DYlib/maCH-UUId-497b72F6390A44fc878e5a2D63B6CC4B/Foo.dYLib", "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(StorageFormat.LowerCase, "foO.DYlib/maCH-UUId-497b72F6390A44fc878e5a2D63B6CC4B/Foo.dYLib", "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b/foo.dylib")]
    [DataRow(StorageFormat.UpperCase, "foO.DYlib/maCH-UUId-497b72F6390A44fc878e5a2D63B6CC4B/Foo.dYLib", "FOO.DYLIB/MACH-UUID-497B72F6390A44FC878E5A2D63B6CC4B/FOO.DYLIB")]
    [DataRow(StorageFormat.Normal, "_.dwARF/Mach-uuiD-Sym-497b72F6390A44fc878e5a2d63b6CC4B/_.DWArf", "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(StorageFormat.LowerCase, "_.dwARF/Mach-uuiD-Sym-497b72F6390A44fc878e5a2d63b6CC4B/_.DWArf", "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b/_.dwarf")]
    [DataRow(StorageFormat.UpperCase, "_.dwARF/Mach-uuiD-Sym-497b72F6390A44fc878e5a2d63b6CC4B/_.DWArf", "_.DWARF/MACH-UUID-SYM-497B72F6390A44FC878E5A2D63B6CC4B/_.DWARF")]
    [DataRow(StorageFormat.Normal, "foO.Cs/shA1-497B72F6390a44fc878E5A2d63b6cc4b0c2d9984/FOo.cs", "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    [DataRow(StorageFormat.LowerCase, "foO.Cs/shA1-497B72F6390a44fc878E5A2d63b6cc4b0c2d9984/FOo.cs", "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984/foo.cs")]
    [DataRow(StorageFormat.UpperCase, "foO.Cs/shA1-497B72F6390a44fc878E5A2d63b6cc4b0c2d9984/FOo.cs", "FOO.CS/SHA1-497B72F6390A44FC878E5A2D63B6CC4B0C2D9984/FOO.CS")]
    [DataRow(StorageFormat.Normal, "fOO.EXe/542d574Ec2000", "foo.exe/542D574Ec2000")]
    [DataRow(StorageFormat.Normal, "fOO.DLl/542d574EC2000", "foo.dll/542D574Ec2000")]
    [DataRow(StorageFormat.LowerCase, "fOO.EXe/542d574Ec2000", "foo.exe/542d574ec2000")]
    [DataRow(StorageFormat.LowerCase, "fOO.DLl/542d574EC2000", "foo.dll/542d574ec2000")]
    [DataRow(StorageFormat.UpperCase, "fOO.EXe/542d574Ec2000", "FOO.EXE/542D574EC2000")]
    [DataRow(StorageFormat.UpperCase, "fOO.DLl/542d574EC2000", "FOO.DLL/542D574EC2000")]
    [DataRow(StorageFormat.Normal, "fOO.PDb/497B72F6390A44FC878e5A2D63b6cc4bFFFFFFFF", "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bFFFFFFFF")]
    [DataRow(StorageFormat.LowerCase, "fOO.PDb/497B72F6390A44FC878e5A2D63b6cc4bFFFFFFFF", "foo.pdb/497b72f6390a44fc878e5a2d63b6cc4bffffffff")]
    [DataRow(StorageFormat.UpperCase, "fOO.PDb/497B72F6390A44FC878e5A2D63b6cc4bFFFFFFFF", "FOO.PDB/497B72F6390A44FC878E5A2D63B6CC4BFFFFFFFF")]
    [DataRow(StorageFormat.Normal, "foO.SO/eLF-BUIldid-180a373D6AFBABF0EB1f09be1bc45bd796A71085", "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.LowerCase, "foO.SO/eLF-BUIldid-180a373D6AFBABF0EB1f09be1bc45bd796A71085", "foo.so/elf-buildid-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.UpperCase, "foO.SO/eLF-BUIldid-180a373D6AFBABF0EB1f09be1bc45bd796A71085", "FOO.SO/ELF-BUILDID-180A373D6AFBABF0EB1F09BE1BC45BD796A71085")]
    [DataRow(StorageFormat.Normal, "_.deBUG/elF-BUildid-sYM-180A373d6afbabf0EB1F09be1bc45bd796a71085", "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.LowerCase, "_.deBUG/elF-BUildid-sYM-180A373d6afbabf0EB1F09be1bc45bd796a71085", "_.debug/elf-buildid-sym-180a373d6afbabf0eb1f09be1bc45bd796a71085")]
    [DataRow(StorageFormat.UpperCase, "_.deBUG/elF-BUildid-sYM-180A373d6afbabf0EB1F09be1bc45bd796a71085", "_.DEBUG/ELF-BUILDID-SYM-180A373D6AFBABF0EB1F09BE1BC45BD796A71085")]
    [DataRow(StorageFormat.Normal, "foO.DYlib/maCH-UUId-497b72F6390A44fc878e5a2D63B6CC4B", "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.LowerCase, "foO.DYlib/maCH-UUId-497b72F6390A44fc878e5a2D63B6CC4B", "foo.dylib/mach-uuid-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.UpperCase, "foO.DYlib/maCH-UUId-497b72F6390A44fc878e5a2D63B6CC4B", "FOO.DYLIB/MACH-UUID-497B72F6390A44FC878E5A2D63B6CC4B")]
    [DataRow(StorageFormat.Normal, "_.dwARF/Mach-uuiD-Sym-497b72F6390A44fc878e5a2d63b6CC4B", "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.LowerCase, "_.dwARF/Mach-uuiD-Sym-497b72F6390A44fc878e5a2d63b6CC4B", "_.dwarf/mach-uuid-sym-497b72f6390a44fc878e5a2d63b6cc4b")]
    [DataRow(StorageFormat.UpperCase, "_.dwARF/Mach-uuiD-Sym-497b72F6390A44fc878e5a2d63b6CC4B", "_.DWARF/MACH-UUID-SYM-497B72F6390A44FC878E5A2D63B6CC4B")]
    [DataRow(StorageFormat.Normal, "foO.Cs/shA1-497B72F6390a44fc878E5A2d63b6cc4b0c2d9984", "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984")]
    [DataRow(StorageFormat.LowerCase, "foO.Cs/shA1-497B72F6390a44fc878E5A2d63b6cc4b0c2d9984", "foo.cs/sha1-497b72f6390a44fc878e5a2d63b6cc4b0c2d9984")]
    [DataRow(StorageFormat.UpperCase, "foO.Cs/shA1-497B72F6390a44fc878E5A2d63b6cc4b0c2d9984", "FOO.CS/SHA1-497B72F6390A44FC878E5A2D63B6CC4B0C2D9984")]
    public void CanBeFixedTest(object storageFormat, string path, string expectedPath)
    {
      Assert.AreEqual(PathUtil.ValidateAndFixErrors.CanBeFixed, path.NormalizeSystem().ValidateAndFixDataPath((StorageFormat) storageFormat, out var fixedPath));
      Assert.AreEqual(expectedPath.NormalizeSystem(), fixedPath);
    }
  }
}