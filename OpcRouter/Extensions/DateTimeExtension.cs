using System;

namespace OpcRouter.Extensions;

public static class DateTimeExtension
{
    // convert datetime to timestamp
    public static long ToTimestamp(this DateTime dateTime, TimeZoneInfo timeZoneInfo)
    {
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, timeZoneInfo);
        var unixTime = utcTime.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return (long)unixTime.TotalSeconds;
    }

    // convert timestamp to datetime
    public static DateTime ToDateTime(this long timestamp, TimeZoneInfo timeZoneInfo)
    {
        var utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp);
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZoneInfo);
    }
}