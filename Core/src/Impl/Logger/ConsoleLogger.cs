using System;
using System.Threading;

namespace JetBrains.SymbolStorage.Impl.Logger
{
  internal sealed class ConsoleLogger : ILogger
  {
    private readonly bool myVerbose;
    private static readonly Lock ourLock = new();

    public ConsoleLogger(bool verbose)
    {
      myVerbose = verbose;
      Console.ResetColor();
    }

    void ILogger.Verbose(string str)
    {
      if (myVerbose)
        lock (ourLock)
          Console.WriteLine(str);
    }

    void ILogger.Info(string str)
    {
      lock (ourLock)
        Console.WriteLine(str);
    }

    void ILogger.Fix(string str)
    {
      lock (ourLock)
      {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("FIX: ");
        Console.WriteLine(str);
        Console.ResetColor();
      }
    }

    void ILogger.Warning(string str)
    {
      lock (ourLock)
      {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("WARNING: ");
        Console.WriteLine(str);
        Console.ResetColor();
      }
    }

    void ILogger.Error(string str)
    {
      lock (ourLock)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("ERROR: ");
        Console.Error.WriteLine(str);
        Console.ResetColor();
      }
    }

    public static void Exception(Exception e)
    {
      lock (ourLock)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("ERROR: ");
        Console.Error.WriteLine(e);
        Console.ResetColor();
      }
    }
  }
}