namespace JetBrains.SymbolStorage.Impl.Logger
{
  internal interface ILogger
  {
    void Verbose(string str);
    void Info(string str);
    void Fix(string str);
    void Warning(string str);
    void Error(string str);
  }
}