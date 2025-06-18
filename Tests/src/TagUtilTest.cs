using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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

    [TestMethod]
    public async Task DeserializeTagTest()
    {
      var tagStr = """
                {
                  "ToolId": "SymbolStorageMaker/1.2.4",
                  "FileId": "a0919f17-a32e-4a4a-9651-ba66ca868cf2",
                  "Product": "coreclr",
                  "Version": "3.1.20201006.7",
                  "Properties": [
                    {
                      "Key": "MadeIn",
                      "Value": "JetBrains"
                    }
                  ],
                  "Directories": [
                    "_.dwarf/mach-uuid-sym-1b7f1c87fcc5342e8af350af7e67d408",
                    "_.dwarf/mach-uuid-sym-1f03e7f4488539f3886c07d72fc53ea2",
                    "_.dwarf/mach-uuid-sym-3d886ebc1be53b128afc0dac716749d5",
                    "_.dwarf/mach-uuid-sym-4c33c3af091336e19422d7891cc40e7c",
                    "_.dwarf/mach-uuid-sym-7e0ae8164492328594d4314cab4c8468",
                    "_.dwarf/mach-uuid-sym-9682d080e8e83879ab1d1775844b6022",
                    "_.dwarf/mach-uuid-sym-983cf782cc4d3b3cbb1c3eb2abfd57d6",
                    "_.dwarf/mach-uuid-sym-9e5d291cd8cb3f94ad945f81f2c2ce3d",
                    "_.dwarf/mach-uuid-sym-a9f1027b6c193365b7d4f04ea569d297",
                    "_.dwarf/mach-uuid-sym-cb27e08179ee301c95e48221e2fca7a3",
                    "_.dwarf/mach-uuid-sym-ddc6eb9df5f431409bf1f8b5396cc850",
                    "_.dwarf/mach-uuid-sym-e495bbb5d29e35c2bcdca7a14a59bfdc",
                    "_.dwarf/mach-uuid-sym-f26a0be7db40374eac2248c8ec3cdd3d",
                    "_.dwarf/mach-uuid-sym-f4635a2e35ca3ad2b7cf88c6c568282c"
                  ]
                }
                """;
      
      var tag = await TagUtil.ReadTagScriptAsync(new MemoryStream(Encoding.UTF8.GetBytes(tagStr)));
      Assert.AreEqual("SymbolStorageMaker/1.2.4", tag.ToolId);
      Assert.AreEqual("3.1.20201006.7", tag.Version);
      Assert.AreEqual(Guid.Parse("a0919f17-a32e-4a4a-9651-ba66ca868cf2"), tag.FileId);
      Assert.AreEqual(DateTime.MinValue, tag.CreationUtcTime);
      
      Assert.IsNotNull(tag.Properties);
      Assert.AreEqual(1, tag.Properties.Length);
      Assert.AreEqual("JetBrains", tag.Properties[0].Value);
      
      Assert.IsNotNull(tag.Directories);
      Assert.AreEqual(14, tag.Directories.Length);
      Assert.AreEqual(PathUtil.NormalizeSystem("_.dwarf/mach-uuid-sym-f4635a2e35ca3ad2b7cf88c6c568282c"), tag.Directories[13]);
    }
    
    [TestMethod]
    public async Task SerializeTagTest()
    {
      var tagStr = """
                   {
                     "ToolId": "SymbolStorageMaker/1.2.4",
                     "FileId": "a0919f17-a32e-4a4a-9651-ba66ca868cf2",
                     "Product": "coreclr",
                     "Version": "3.1.20201006.7",
                     "CreationUtcTime": "2025-06-17T15:45:45",
                     "Properties": [
                       {
                         "Key": "MadeIn",
                         "Value": "JetBrains"
                       }
                     ],
                     "Directories": [
                       "_.dwarf/mach-uuid-sym-1b7f1c87fcc5342e8af350af7e67d408"
                     ]
                   }
                   """;
      
      var tag = await TagUtil.ReadTagScriptAsync(new MemoryStream(Encoding.UTF8.GetBytes(tagStr)));
      Assert.AreEqual("SymbolStorageMaker/1.2.4", tag.ToolId);
      Assert.AreEqual("3.1.20201006.7", tag.Version);
      
      Assert.IsNotNull(tag.Properties);
      Assert.AreEqual(1, tag.Properties.Length);
      
      Assert.IsNotNull(tag.Directories);
      Assert.AreEqual(1, tag.Directories.Length);

      var resultData = new MemoryStream();
      await TagUtil.WriteTagScriptAsync(tag, resultData);

      resultData.Position = 0;
      var resultJson = new StreamReader(resultData, detectEncodingFromByteOrderMarks: true).ReadToEnd();
      Assert.AreEqual(tagStr, resultJson);
    }
  }
}