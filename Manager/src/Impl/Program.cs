namespace JetBrains.SymbolStorage.Manager.Impl
{
  internal static class Program
  {
    internal static int Main(string[] args) => MainUtil.Main(typeof(Program).Assembly, args, MainUtil.MainMode.Full);
  }
}