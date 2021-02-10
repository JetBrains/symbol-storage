using System;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using Microsoft.SymbolStore;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed partial class Scanner
  {
    private sealed class Tracer : ITracer
    {
      private readonly ILogger myLogger;

      public Tracer([NotNull] ILogger logger)
      {
        myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      }

      void ITracer.WriteLine(string message)
      {
      }

      void ITracer.WriteLine(string format, params object[] arguments)
      {
      }

      void ITracer.Information(string message) => myLogger.Info(message);
      void ITracer.Information(string format, params object[] arguments) => myLogger.Info(string.Format(format, arguments));

      void ITracer.Warning(string message) => myLogger.Warning(message);
      void ITracer.Warning(string format, params object[] arguments) => myLogger.Warning(string.Format(format, arguments));

      void ITracer.Error(string message) => myLogger.Error(message);
      void ITracer.Error(string format, params object[] arguments) => myLogger.Error(string.Format(format, arguments));

      void ITracer.Verbose(string message)
      {
      }

      void ITracer.Verbose(string format, params object[] arguments)
      {
      }
    }
  }
}