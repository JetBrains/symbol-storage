using JetBrains.SymbolStorage.Impl.Logger;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class StatisticsTest
  {
    [TestMethod]
    public void FixTest()
    {
      var statistics = new Statistics();
      Assert.AreEqual(0, statistics.Errors);
      Assert.AreEqual(0, statistics.Warnings);
      Assert.AreEqual(0, statistics.Fixes);
      Assert.IsFalse(statistics.HasProblems);

      statistics.IncrementFix();
      Assert.AreEqual(0, statistics.Errors);
      Assert.AreEqual(0, statistics.Warnings);
      Assert.AreEqual(1, statistics.Fixes);
      Assert.IsFalse(statistics.HasProblems);
    }

    [TestMethod]
    public void WarningTest()
    {
      var statistics = new Statistics();
      Assert.AreEqual(0, statistics.Errors);
      Assert.AreEqual(0, statistics.Warnings);
      Assert.AreEqual(0, statistics.Fixes);
      Assert.IsFalse(statistics.HasProblems);

      statistics.IncrementWarning();
      statistics.IncrementWarning();
      Assert.AreEqual(0, statistics.Errors);
      Assert.AreEqual(2, statistics.Warnings);
      Assert.AreEqual(0, statistics.Fixes);
      Assert.IsTrue(statistics.HasProblems);
    }

    [TestMethod]
    public void ErrorTest()
    {
      var statistics = new Statistics();
      Assert.AreEqual(0, statistics.Errors);
      Assert.AreEqual(0, statistics.Warnings);
      Assert.AreEqual(0, statistics.Fixes);
      Assert.IsFalse(statistics.HasProblems);

      statistics.IncrementError();
      statistics.IncrementError();
      statistics.IncrementError();
      Assert.AreEqual(3, statistics.Errors);
      Assert.AreEqual(0, statistics.Warnings);
      Assert.AreEqual(0, statistics.Fixes);
      Assert.IsTrue(statistics.HasProblems);
    }
  }
}