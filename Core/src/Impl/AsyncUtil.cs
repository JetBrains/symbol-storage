using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class AsyncUtil
  {
    public static Task ParallelFor<TSource>(
      [NotNull] this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      [NotNull] Func<TSource, Task> func) => sources.ParallelFor(degreeOfParallelism, async x =>
      {
        await func(x);
        return true;
      });

    public static async Task<IReadOnlyCollection<TResult>> ParallelFor<TSource, TResult>(
      [NotNull] this IEnumerable<TSource> sources,
      int degreeOfParallelism,
      [NotNull] Func<TSource, Task<TResult>> func)
    {
      if (sources == null) throw new ArgumentNullException(nameof(sources));
      if (func == null) throw new ArgumentNullException(nameof(func));
      using var semaphore = new SemaphoreSlim(degreeOfParallelism);
      var semaphore1 = semaphore;
      return await Task.WhenAll(sources.Select(async source =>
        {
          await semaphore1.WaitAsync();
          try
          {
            return await func(source);
          }
          finally
          {
            semaphore1.Release();
          }
        }));
    }

    public static Task ParallelFor<TSource>(
      [NotNull] this IAsyncEnumerable<TSource> sources,
      int degreeOfParallelism,
      [NotNull] Func<TSource, Task> func) => sources.ParallelFor(degreeOfParallelism, async x =>
      {
        await func(x);
        return true;
      });

    public static async Task<IReadOnlyCollection<TResult>> ParallelFor<TSource, TResult>(
      [NotNull] this IAsyncEnumerable<TSource> sources,
      int degreeOfParallelism,
      [NotNull] Func<TSource, Task<TResult>> func)
    {
      if (sources == null) throw new ArgumentNullException(nameof(sources));
      if (func == null) throw new ArgumentNullException(nameof(func));
      using var semaphore = new SemaphoreSlim(degreeOfParallelism);
      var semaphore1 = semaphore;
      return await Task.WhenAll(await sources.Select(async source =>
        {
          await semaphore1.WaitAsync();
          try
          {
            return await func(source);
          }
          finally
          {
            semaphore1.Release();
          }
        }).ToListAsync());
    }
  }
}