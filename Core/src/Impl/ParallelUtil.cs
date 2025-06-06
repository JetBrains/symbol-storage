#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class ParallelUtil
  { 
    public static List<TResult> ParallelFor<TSource, TResult>(
      this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, TResult> func)
    {
      var results = sources.TryGetNonEnumeratedCount(out var expectedCount) ? new List<TResult>(expectedCount) : new List<TResult>();
      var lockObj = new Lock();
      
      Parallel.ForEach(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, (x, _) =>
      {
        var result = func(x);
        lock (lockObj)
          results.Add(result);
      });
      
      if (results.Capacity > results.Count + 1024)
        results.TrimExcess();

      return results;
    }
    
    
    public static Task ParallelForAsync<TSource>(
      this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, ValueTask> func)
    {
      return Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, (x, _) => func(x));
    }

    public static async Task<List<TResult>> ParallelForAsync<TSource, TResult>(
      this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, ValueTask<TResult>> func)
    {
      var results = sources.TryGetNonEnumeratedCount(out var expectedCount) ? new List<TResult>(expectedCount) : new List<TResult>();
      var lockObj = new Lock();
      
      await Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, async (x, _) =>
      {
        var result = await func(x);
        lock (lockObj)
          results.Add(result);
      });
      
      if (results.Capacity > results.Count + 1024)
        results.TrimExcess();

      return results;
    }

    public static Task ParallelForAsync<TSource>(
      this IAsyncEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, ValueTask> func)
    {
      return Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, (x, _) => func(x));
    }

    public static async Task<List<TResult>> ParallelForAsync<TSource, TResult>(
      this IAsyncEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, ValueTask<TResult>> func)
    {
      var results = new List<TResult>();
      var lockObj = new Lock();
      
      await Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, async (x, _) =>
      {
        var result = await func(x);
        lock (lockObj)
          results.Add(result);
      });

      if (results.Capacity > results.Count + 1024)
        results.TrimExcess();
      
      return results;
    }
  }
}