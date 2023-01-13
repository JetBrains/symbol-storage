using System;
using System.Text;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal static class ConsoleUtil
  {
    [NotNull]
    public static string ReadHiddenConsoleInput([NotNull] string msg)
    {
      Console.Write(msg);
      Console.Write(": ");
      var secret = new StringBuilder();
      while (true)
      {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter)
          break;
        if (key.Key == ConsoleKey.Backspace && secret.Length > 0)
          secret.Remove(secret.Length - 1, 1);
        else if (key.Key != ConsoleKey.Backspace)
          secret.Append(key.KeyChar);
      }
      Console.WriteLine();
      return secret.ToString();
    }
  }
}