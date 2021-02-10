using System;

namespace JetBrains.SymbolStorage.Impl.Logger
{
  internal sealed class ConsoleLogger : ILogger
  {
    public static readonly ILogger Instance = new ConsoleLogger();
    private readonly object myLock = new();

    private ConsoleLogger() => Console.ResetColor();

    void ILogger.Info(string str)
    {
      lock (myLock)
        Console.WriteLine(str);
    }

    void ILogger.Fix(string str)
    {
      lock (myLock)
      {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("FIX: ");
        Console.WriteLine(str);
        Console.ResetColor();
      }
    }

    void ILogger.Warning(string str)
    {
      lock (myLock)
      {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("WARNING: ");
        Console.WriteLine(str);
        Console.ResetColor();
      }
    }

    void ILogger.Error(string str)
    {
      lock (myLock)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("ERROR: ");
        Console.Error.WriteLine(str);
        Console.ResetColor();
      }
    }
  }
}