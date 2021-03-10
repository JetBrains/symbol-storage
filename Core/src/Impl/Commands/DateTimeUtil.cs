using System;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal static class DateTimeUtil
  {
    public static ushort ToDosDate(this DateTime date)
    {
      if (date.Year < 1980)
        throw new Exception("The year should be 1980+");
      return (ushort) (
        ((date.Year - 1980) << 9) |
        (date.Month << 5) |
        date.Day);
    }

    public static ushort ToDosTime(this DateTime time)
    {
      return (ushort) (
        (time.Hour << 11) |
        (time.Minute << 5) |
        (time.Second / 2));
    }

    public static DateTime ToDateTime(ushort date, ushort time)
    {
      return new(
        (date >> 9) + 1980,
        (date >> 5) & 0xF,
        date & 0x1F,
        time >> 11,
        (time >> 5) & 0x3F,
        (time & 0x1F) << 1);
    }

    public static DateTime ToFloor(this DateTime date, TimeSpan span)
    {
      var ticks = date.Ticks / span.Ticks;
      return new DateTime(ticks * span.Ticks);
    }

    public static DateTime ToCeil(this DateTime date, TimeSpan span)
    {
      var ticks = (date.Ticks + span.Ticks - 1) / span.Ticks;
      return new DateTime(ticks * span.Ticks);
    }
  }
}