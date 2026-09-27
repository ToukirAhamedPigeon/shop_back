using shop_back.src.Shared.Application.DTOs.Dashboard;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    /// <summary>
    /// Splits UTC timestamps into the caller's local calendar days. Day
    /// boundaries come from the time zone rules, so DST changes are handled.
    /// </summary>
    public static class ActivityDays
    {
        public const int DefaultDays = 14;
        public const int MaxDays = 90;

        /// <summary>Accepts IANA ("Asia/Dhaka") or Windows ids; anything unknown is UTC.</summary>
        public static TimeZoneInfo ResolveTimeZone(string? id)
        {
            if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var tz))
                return tz;
            return TimeZoneInfo.Utc;
        }

        public static int ClampDays(int days) => days < 1 ? DefaultDays : Math.Min(days, MaxDays);

        /// <summary>The local dates of the series, oldest first, ending on today's local date.</summary>
        public static DateOnly[] LocalDates(TimeZoneInfo tz, int days, DateTime nowUtc)
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(nowUtc), tz));
            return Enumerable.Range(0, days).Select(i => today.AddDays(i - (days - 1))).ToArray();
        }

        /// <summary>UTC instant of local midnight at the start of <paramref name="date"/>.</summary>
        public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo tz)
        {
            var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            // In zones where DST starts at midnight, 00:00 doesn't exist; the day starts an hour later.
            while (tz.IsInvalidTime(local)) local = local.AddMinutes(30);
            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }

        /// <summary>[startUtc, endUtc) covering every day of the series.</summary>
        public static (DateTime StartUtc, DateTime EndUtc) RangeUtc(DateOnly[] dates, TimeZoneInfo tz) =>
            (StartOfDayUtc(dates[0], tz), StartOfDayUtc(dates[^1].AddDays(1), tz));

        public static List<ActivityDayDto> Bucket(IEnumerable<DateTime> timestampsUtc, DateOnly[] dates, TimeZoneInfo tz)
        {
            var counts = dates.ToDictionary(d => d, _ => 0);
            foreach (var ts in timestampsUtc)
            {
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(ts), tz));
                if (counts.ContainsKey(day)) counts[day]++;
            }
            return dates.Select(d => new ActivityDayDto { Date = d.ToString("yyyy-MM-dd"), Count = counts[d] }).ToList();
        }

        // SQL Server hands back Unspecified; the column always holds UTC.
        private static DateTime AsUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
