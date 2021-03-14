using System;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Logger
{
  internal sealed class LoggerWithStatistics : ILogger
  {
    private readonly ILogger myLogger;
    private readonly Statistics myStatistics;

    public LoggerWithStatistics([NotNull] ILogger logger, [NotNull] Statistics statistics)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStatistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    void ILogger.Verbose(string str) => myLogger.Verbose(str);
    void ILogger.Info(string str) => myLogger.Info(str);

    void ILogger.Fix(string str)
    {
      myStatistics.IncrementFix();
      myLogger.Fix(str);
    }

    void ILogger.Warning(string str)
    {
      myStatistics.IncrementWarning();
      myLogger.Warning(str);
    }

    void ILogger.Error(string str)
    {
      myStatistics.IncrementError();
      myLogger.Error(str);
    }
  }
}