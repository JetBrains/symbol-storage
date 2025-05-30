#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JetBrains.SymbolStorage.Impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class CabCompressionUtilTest
  {
    [TestMethod]
    public void CabArchiveFileHasExpectedHashsum()
    {
      // Hash calculated for the file, created with originally used lib (MSFTCompressionCab)
      const string expectedSha512HashAsBase64 = "iQ8UcZWf7wQ+Mwoiec4D9N6i9mE4ZdV6QzNBJv7XsJprzDvgofwIGG7pPgpfbzhUCk1OVxxD3ce4CZr8dKOmlQ==";
      
      if (!OperatingSystem.IsWindows())
        Assert.Inconclusive("This test requires Windows environment");
      
      var tempDataFile = Path.GetTempFileName();
      try
      {
        using (var stream = File.Open(tempDataFile, FileMode.Open, FileAccess.ReadWrite))
        {
          for (int i = 0; i < 100; i++)
            stream.Write(Enumerable.Range(10, 100).Select(x => (byte)x).ToArray());
        }

        var fileInfo = new FileInfo(tempDataFile);
        fileInfo.LastWriteTimeUtc = new DateTime(2025, 1, 1);
        
        var tempCabArchiveFile = Path.GetTempFileName();
        string actualCabFileHash;
        try
        {
          CabCompressionUtil.CompressFiles(tempCabArchiveFile, [
            new CompressionFileInfo("aaa.pdb", tempDataFile)
          ]);
          
          using (var compressedStream = File.OpenRead(tempCabArchiveFile))
          using (var shaAlg = SHA512.Create())
          {
            actualCabFileHash = Convert.ToBase64String(shaAlg.ComputeHash(compressedStream));
          }
        }
        finally
        {
          if (File.Exists(tempCabArchiveFile))
            File.Delete(tempCabArchiveFile);
        }
        
        Assert.AreEqual(expectedSha512HashAsBase64, actualCabFileHash);
      }
      finally
      {
        if (File.Exists(tempDataFile))
          File.Delete(tempDataFile);
      }
    }
  }
}