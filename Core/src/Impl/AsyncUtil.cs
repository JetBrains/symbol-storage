#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class AsyncUtil
  {
    public static Task ParallelFor<TSource>(
      this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, Task> func)
    {
      return Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, (x, _) => new ValueTask(func(x)));
    }

    public static async Task<List<TResult>> ParallelFor<TSource, TResult>(
      this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, Task<TResult>> func)
    {
      var results = sources.TryGetNonEnumeratedCount(out var expectedCount) ? new List<TResult>(expectedCount) : new List<TResult>();
      var lockObj = new Lock();
      
      await Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, async (x, _) =>
      {
        var result = await func(x);
        lock (lockObj)
          results.Add(result);
      });

      return results;
    }

    public static Task ParallelFor<TSource>(
      this IAsyncEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, Task> func)
    {
      return Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, (x, _) => new ValueTask(func(x)));
    }

    public static async Task<List<TResult>> ParallelFor<TSource, TResult>(
      this IAsyncEnumerable<TSource> sources,
      int degreeOfParallelism,
      Func<TSource, Task<TResult>> func)
    {
      var results = new List<TResult>();
      var lockObj = new Lock();
      
      await Parallel.ForEachAsync(sources, new ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, async (x, _) =>
      {
        var result = await func(x);
        lock (lockObj)
          results.Add(result);
      });

      return results;
    }
  }
}