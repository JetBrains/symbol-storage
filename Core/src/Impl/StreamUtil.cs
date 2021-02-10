using System;
using System.IO;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class StreamUtil
  {
    [NotNull]
    public static Stream Rewind([NotNull] this Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      stream.Seek(0, SeekOrigin.Begin);
      return stream;
    }
  }
}