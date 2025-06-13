using System;
using JetBrains.SymbolStorage.Impl.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class DateTimeUtilTest
  {
    [TestMethod]
    public void DosDateTest()
    {
      Assert.ThrowsException<Exception>(() => new DateTime(1979, 12, 31).ToDosDate());
      Assert.AreEqual((ushort) 0x21, new DateTime(1980, 1, 1).ToDosDate());
      Assert.AreEqual((ushort) 0x5275, new DateTime(2021, 3, 21).ToDosDate());
    }

    [TestMethod]
    public void DosTimeTest()
    {
      Assert.AreEqual((ushort) 0xAF31, new DateTime(1600, 1, 1, 21, 57, 34).ToDosTime());
      Assert.AreEqual((ushort) 0xAF31, new DateTime(1600, 1, 1, 21, 57, 35).ToDosTime());
    }

    [TestMethod]
    public void ToCeilTest()
    {
      Assert.AreEqual(new DateTime(1999, 1, 1, 0, 0, 2), new DateTime(1998, 12, 31, 23, 59, 51).ToCeil(TimeSpan.FromSeconds(13)));
    }

    [TestMethod]
    public void ToFloorTest()
    {
      Assert.AreEqual(new DateTime(1998, 12, 31, 23, 59, 49), new DateTime(1998, 12, 31, 23, 59, 51).ToFloor(TimeSpan.FromSeconds(13)));
    }
  }
}