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
  public class AsyncUtilTest
  {
    [TestMethod]
    public async Task ForEachOnEnumerableWithResultProcessesAllItems()
    {
      var result = await Enumerable.Range(0, 10000).ParallelFor(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        return data;
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachOnEnumerableWithoutResultProcessesAllItems()
    {
      var result = new List<int>();
      var lockObj = new Lock();
      await Enumerable.Range(0, 10000).ParallelFor(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        lock (lockObj)
          result.Add(data);
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachOnAsyncEnumerableWithResultProcessesAllItems()
    {
      var result = await Enumerable.Range(0, 10000).ToAsyncEnumerable().SelectAwait(async val => 
      {
        await Task.Yield();
        return val;
      }).ParallelFor(Math.Max(2, Environment.ProcessorCount), async data =>
      {
        await Task.Yield();
        return data;
      });
      
      result.Sort();
      Assert.IsTrue(result.SequenceEqual(Enumerable.Range(0, 10000)));
    }
    
    [TestMethod]
    public async Task ForEachOnAsyncEnumerableWithoutResultProcessesAllItems()
    {
      var result = new List<int>();
      var lockObj = new Lock();
      await Enumerable.Range(0, 10000).ToAsyncEnumerable().SelectAwait(async val => 
      {
        await Task.Yield();
        return val;
      }).ParallelFor(Math.Max(2, Environment.ProcessorCount), async data =>
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