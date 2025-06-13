using System;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class PresentationUtil
  {
    public static string ToHex(this byte[] data)
    {
      return BitConverter.ToString(data).Replace("-", "");
    }

    public static string ToKibibyte(this long value)
    {
      if (value < 0)
        throw new ArgumentException("value");
      if (value < 1000)
        return $"{value} B";
      if (value < 1000 * 1000)
        return $"{value / 1024d:F2} KiB";
      if (value < 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024):F2} MiB";
      if (value < 1000L * 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024 * 1024):F2} GiB";
      if (value < 1000L * 1000 * 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024 * 1024 * 1024):F2} TiB";
      if (value < 1000L * 1000 * 1000 * 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024 * 1024 * 1024 * 1024):F2} PiB";
      return $"{value} B";
    }

    public static string ToKibibyte(this ulong value)
    {
      if (value < 1000U)
        return $"{value} B";
      if (value < 1000U * 1000)
        return $"{value / 1024d:F2} KiB";
      if (value < 1000U * 1000 * 1000)
        return $"{value / (1024d * 1024):F2} MiB";
      if (value < 1000UL * 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024 * 1024):F2} GiB";
      if (value < 1000UL * 1000 * 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024 * 1024 * 1024):F2} TiB";
      if (value < 1000UL * 1000 * 1000 * 1000 * 1000 * 1000)
        return $"{value / (1024d * 1024 * 1024 * 1024 * 1024):F2} PiB";
      return $"{value} B";
    }
  }
}