namespace JetBrains.SymbolStorage.Uploader.Impl
{
  internal static class Program
  {
    internal static int Main(string[] args) => MainUtil.Main(args, MainUtil.MainMode.UploadOnly);
  }
}