using System;
using System.Diagnostics;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal interface IStatsReportingCommand : ICommand
  {
    long SubOperationsCount { get; }
  }

  internal class TimeReportingCommandWrapper : IStatsReportingCommand
  {
    private readonly ICommand myCommand;
    private readonly ILogger? myLogger;

    public TimeReportingCommandWrapper(ICommand command, ILogger? logger)
    {
      myCommand = command;
      myLogger = logger;
      ExecutionTime = TimeSpan.Zero;
      Executed = false;
    }
    
    public long SubOperationsCount => (myCommand as IStatsReportingCommand)?.SubOperationsCount ?? (Executed ? 1 : 0);
    public TimeSpan ExecutionTime { get; private set; }
    public bool Executed { get; private set; }

    public async Task<int> ExecuteAsync()
    {
      Stopwatch sw = Stopwatch.StartNew();
      try
      {
        return await myCommand.ExecuteAsync();
      }
      finally
      {
        ExecutionTime = sw.Elapsed;
        Executed = true;

        if (myLogger != null)
        {
          string logText = $"[{DateTime.Now:s}] '{myCommand.GetType().Name}' executed in {ExecutionTime}.";
          if (myCommand is IStatsReportingCommand sRepCmd && sRepCmd.SubOperationsCount > 0)
            logText += $" Sub-operations count: {sRepCmd.SubOperationsCount}, RPS: {sRepCmd.SubOperationsCount / ExecutionTime.TotalSeconds}.";
          myLogger.Info(logText);
        }
      }
    }
  }

  internal static class TimeReportingCommandExtensions
  {
    public static TimeReportingCommandWrapper WithTimeReporting(this ICommand command, ILogger? logger = null)
    {
      return new TimeReportingCommandWrapper(command, logger);
    }

    public static TimeReportingCommandWrapper WithTimeReportingToConsole(this ICommand command)
    {
      return new TimeReportingCommandWrapper(command, new ConsoleLogger(verbose: false));
    }
  }
}