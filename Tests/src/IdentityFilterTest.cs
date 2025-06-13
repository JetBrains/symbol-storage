#nullable enable

using System;
using JetBrains.SymbolStorage.Impl.Tags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class IdentityFilterTest
  {
    [TestMethod]
    public void Test0()
    {
      var identityFilter = new IdentityFilter(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
      Assert.IsTrue(identityFilter.IsMatch("km", "ab182653c"));
      Assert.IsTrue(identityFilter.IsMatch("akama", "ab98324jc"));
      Assert.IsTrue(identityFilter.IsMatch("aakakkaamama", "a3454bc"));
    }
    
    [TestMethod]
    public void Test1()
    {
      var identityFilter = new IdentityFilter(
        new[] {"*k*m*"},
        Array.Empty<string>(),
        new[] {"a*2*c"},
        Array.Empty<string>());
      Assert.IsTrue(identityFilter.IsMatch("km", "ab182653c"));
      Assert.IsTrue(identityFilter.IsMatch("akama", "ab98324jc"));
      Assert.IsTrue(identityFilter.IsMatch("aakakkaamama", "a23454bc"));
      Assert.IsFalse(identityFilter.IsMatch("akaka", "ab98324jc"));
      Assert.IsFalse(identityFilter.IsMatch("akama", "ab9834jc"));
      Assert.IsFalse(identityFilter.IsMatch("akaha", "ab9834jc"));
    }
    
    [TestMethod]
    public void Test2()
    {
      var identityFilter = new IdentityFilter(
        new[] {"*k*m*"},
        new[] {"*kk*"},
        new[] {"a*2*c"},
        new[] {"aa*"});
      Assert.IsTrue(identityFilter.IsMatch("km", "ab182653c"));
      Assert.IsTrue(identityFilter.IsMatch("akama", "ab98324jc"));
      Assert.IsFalse(identityFilter.IsMatch("akkama", "ab98324jc"));
      Assert.IsFalse(identityFilter.IsMatch("aakakkaamama", "a23454bc"));
    }
  }
}