using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class ParallelUtilTest
  {
    [TestMethod]
    public void ForEachOnEnumerableWithResultProcessesAllItems()
    {
      var result = Enumerable.Range(0, 10000).ParallelFor(Math.Max(2, Environment.ProcessorCount), data => data);
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachAsyncOnEnumerableWithResultProcessesAllItems()
    {
      var result = await Enumerable.Range(0, 10000).ParallelForAsync(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        return data;
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachAsyncOnEnumerableWithoutResultProcessesAllItems()
    {
      var result = new List<int>();
      var lockObj = new Lock();
      await Enumerable.Range(0, 10000).ParallelForAsync(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        lock (lockObj)
          result.Add(data);
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachAsyncOnAsyncEnumerableWithResultProcessesAllItems()
    {
      var result = await Enumerable.Range(0, 10000).ToAsyncEnumerable().SelectAwait(async val => 
      {
        await Task.Yield();
        return val;
      }).ParallelForAsync(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        return data;
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachAsyncOnAsyncEnumerableWithoutResultProcessesAllItems()
    {
      var result = new List<int>();
      var lockObj = new Lock();
      await Enumerable.Range(0, 10000).ToAsyncEnumerable().SelectAwait(async val => 
      {
        await Task.Yield();
        return val;
      }).ParallelForAsync(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        lock (lockObj)
          result.Add(data);
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
  }
}