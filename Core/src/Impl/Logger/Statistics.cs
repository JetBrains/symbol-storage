using System.Runtime.CompilerServices;
using System.Threading;

namespace JetBrains.SymbolStorage.Impl.Logger
{
  internal sealed class Statistics
  {
    private long myErrors;
    private long myWarnings;
    private long myFixes;

    public long Errors => myErrors;
    public long Warnings => myWarnings;
    public long Fixes => myFixes;

    public bool HasProblems => myWarnings != 0 || myErrors != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementError() => Interlocked.Increment(ref myErrors);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementWarning() => Interlocked.Increment(ref myWarnings);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementFix() => Interlocked.Increment(ref myFixes);
  }
}