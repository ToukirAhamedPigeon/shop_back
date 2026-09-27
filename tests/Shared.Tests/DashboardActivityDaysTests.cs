using shop_back.src.Shared.Infrastructure.Helpers;
using Xunit;

namespace Shared.Tests;

public class DashboardActivityDaysTests
{
    private static readonly TimeZoneInfo Dhaka = ActivityDays.ResolveTimeZone("Asia/Dhaka");
    private static readonly TimeZoneInfo NewYork = ActivityDays.ResolveTimeZone("America/New_York");

    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveTimeZone_accepts_iana_ids_and_falls_back_to_utc()
    {
        Assert.Equal(TimeSpan.FromHours(6), Dhaka.BaseUtcOffset);
        Assert.Same(TimeZoneInfo.Utc, ActivityDays.ResolveTimeZone("Not/AZone"));
        Assert.Same(TimeZoneInfo.Utc, ActivityDays.ResolveTimeZone(null));
        Assert.Same(TimeZoneInfo.Utc, ActivityDays.ResolveTimeZone("  "));
    }

    [Theory]
    [InlineData(0, ActivityDays.DefaultDays)]
    [InlineData(-3, ActivityDays.DefaultDays)]
    [InlineData(7, 7)]
    [InlineData(500, ActivityDays.MaxDays)]
    public void ClampDays_keeps_the_series_in_range(int requested, int expected) =>
        Assert.Equal(expected, ActivityDays.ClampDays(requested));

    [Fact]
    public void LocalDates_end_on_the_local_date_not_the_utc_date()
    {
        // 20:00 UTC on 26 Sep is already 02:00 on 27 Sep in Dhaka.
        var dates = ActivityDays.LocalDates(Dhaka, 3, Utc(2026, 9, 26, 20));

        Assert.Equal(new[] { new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 26), new DateOnly(2026, 9, 27) }, dates);
    }

    [Fact]
    public void RangeUtc_spans_local_midnight_to_local_midnight()
    {
        var dates = ActivityDays.LocalDates(Dhaka, 2, Utc(2026, 9, 27, 12));
        var (start, end) = ActivityDays.RangeUtc(dates, Dhaka);

        Assert.Equal(Utc(2026, 9, 25, 18), start); // 26 Sep 00:00 in Dhaka
        Assert.Equal(Utc(2026, 9, 27, 18), end);   // 28 Sep 00:00 in Dhaka
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public void Bucket_counts_by_local_day_and_ignores_out_of_range()
    {
        var dates = ActivityDays.LocalDates(Dhaka, 2, Utc(2026, 9, 27, 12));
        var stamps = new[]
        {
            Utc(2026, 9, 25, 17, 59), // 25 Sep 23:59 local: before the series
            Utc(2026, 9, 25, 18, 0),  // 26 Sep 00:00 local
            Utc(2026, 9, 26, 17, 59), // 26 Sep 23:59 local
            Utc(2026, 9, 26, 18, 0),  // 27 Sep 00:00 local, though still 26 Sep in UTC
            DateTime.SpecifyKind(new DateTime(2026, 9, 27, 3, 0, 0), DateTimeKind.Unspecified), // treated as UTC
        };

        var days = ActivityDays.Bucket(stamps, dates, Dhaka);

        Assert.Equal(new[] { "2026-09-26", "2026-09-27" }, days.Select(d => d.Date));
        Assert.Equal(new[] { 2, 2 }, days.Select(d => d.Count));
    }

    [Fact]
    public void Bucket_follows_dst_changes()
    {
        // US DST ended on 1 Nov 2026 at 02:00 local; that day is 25 hours long.
        var dates = new[] { new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2) };
        Assert.Equal(Utc(2026, 11, 1, 4), ActivityDays.StartOfDayUtc(dates[1], NewYork)); // EDT, UTC-4
        Assert.Equal(Utc(2026, 11, 2, 5), ActivityDays.StartOfDayUtc(dates[2], NewYork)); // EST, UTC-5

        var days = ActivityDays.Bucket(new[] { Utc(2026, 11, 2, 4, 30) }, dates, NewYork); // 23:30 on 1 Nov, EST

        Assert.Equal(new[] { 0, 1, 0 }, days.Select(d => d.Count));
    }

    [Fact]
    public void StartOfDayUtc_skips_a_midnight_that_does_not_exist()
    {
        // Chile springs forward at 00:00 (to 01:00) on 6 Sep 2026.
        var santiago = ActivityDays.ResolveTimeZone("America/Santiago");
        var start = ActivityDays.StartOfDayUtc(new DateOnly(2026, 9, 6), santiago);

        Assert.Equal(new DateOnly(2026, 9, 6), DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, santiago)));
    }
}
