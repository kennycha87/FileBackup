using System;
using System.Globalization;
using KlstBackup.Models;
using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

/// <summary>
/// Schedule math tests use fixed local dates chosen to avoid DST transition
/// boundaries (2 AM in March/November for US time zones).
/// </summary>
public class SchedulerMathTests
{
    private static BackupJob Job(ScheduleType type, string time, DayOfWeek? weekDay = null, int dayOfMonth = 1)
    {
        return new BackupJob
        {
            ScheduleType = type,
            Time = TimeOnly.Parse(time, CultureInfo.InvariantCulture),
            WeekDay = weekDay,
            DayOfMonth = dayOfMonth,
            Enabled = true
        };
    }

    // ----- ComputeNextRun -----

    [Fact]
    public void Daily_NextRun_IsTodayWhenTimeAhead()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        var now = new DateTime(2026, 9, 10, 10, 0, 0); // Thursday

        Assert.Equal(new DateTime(2026, 9, 10, 14, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Daily_NextRun_IsTomorrowWhenTimePassed()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        var now = new DateTime(2026, 9, 10, 15, 0, 0);

        Assert.Equal(new DateTime(2026, 9, 11, 14, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Weekly_NextRun_IsThisWeeksDayWhenAhead()
    {
        var job = Job(ScheduleType.Weekly, "08:00", weekDay: DayOfWeek.Friday);
        var now = new DateTime(2026, 9, 10, 10, 0, 0); // Thursday

        Assert.Equal(new DateTime(2026, 9, 11, 8, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Weekly_NextRun_IsNextWeekWhenSameDayPassed()
    {
        var job = Job(ScheduleType.Weekly, "08:00", weekDay: DayOfWeek.Thursday);
        var now = new DateTime(2026, 9, 10, 10, 0, 0); // Thursday

        Assert.Equal(new DateTime(2026, 9, 17, 8, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Monthly_NextRun_WithinMonth()
    {
        var job = Job(ScheduleType.Monthly, "02:00", dayOfMonth: 15);
        var now = new DateTime(2026, 9, 10, 10, 0, 0);

        Assert.Equal(new DateTime(2026, 9, 15, 2, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Monthly_NextRun_IsNextMonthWhenPassed()
    {
        var job = Job(ScheduleType.Monthly, "02:00", dayOfMonth: 5);
        var now = new DateTime(2026, 9, 10, 10, 0, 0);

        Assert.Equal(new DateTime(2026, 10, 5, 2, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Monthly_ClampsDayToMonthLength()
    {
        var job = Job(ScheduleType.Monthly, "02:00", dayOfMonth: 31);
        var now = new DateTime(2026, 2, 1, 10, 0, 0); // 2026 is not a leap year

        Assert.Equal(new DateTime(2026, 2, 28, 2, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    [Fact]
    public void Monthly_February29_LeapYear()
    {
        var job = Job(ScheduleType.Monthly, "02:00", dayOfMonth: 31);
        var now = new DateTime(2028, 2, 1, 10, 0, 0); // 2028 is a leap year

        Assert.Equal(new DateTime(2028, 2, 29, 2, 0, 0), SchedulerMath.ComputeNextRun(job, now));
    }

    // ----- IsDue -----

    [Fact]
    public void IsDue_WhenNeverRunAndSlotPassed()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        var now = new DateTime(2026, 9, 10, 15, 0, 0);

        Assert.True(SchedulerMath.IsDue(job, now));
    }

    [Fact]
    public void IsDue_NotBeforeScheduledTime()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        var now = new DateTime(2026, 9, 10, 10, 0, 0);

        Assert.False(SchedulerMath.IsDue(job, now));
    }

    [Fact]
    public void IsDue_FalseWhenAlreadyRanInSlot()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        var now = new DateTime(2026, 9, 10, 15, 0, 0);
        job.LastRunUtc = new DateTime(2026, 9, 10, 14, 30, 0, DateTimeKind.Local);

        Assert.False(SchedulerMath.IsDue(job, now));
    }

    [Fact]
    public void IsDue_TrueWhenSlotAfterLastRun()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        var now = new DateTime(2026, 9, 10, 15, 0, 0);
        job.LastRunUtc = new DateTime(2026, 9, 9, 14, 30, 0, DateTimeKind.Local);

        Assert.True(SchedulerMath.IsDue(job, now));
    }

    [Fact]
    public void IsDue_FalseWhenDisabled()
    {
        var job = Job(ScheduleType.Daily, "14:00");
        job.Enabled = false;
        var now = new DateTime(2026, 9, 10, 15, 0, 0);

        Assert.False(SchedulerMath.IsDue(job, now));
    }

    [Fact]
    public void IsDue_Weekly_AfterLastWeeksRun()
    {
        var job = Job(ScheduleType.Weekly, "08:00", weekDay: DayOfWeek.Thursday);
        var now = new DateTime(2026, 9, 10, 9, 0, 0); // Thursday 09:00
        job.LastRunUtc = new DateTime(2026, 9, 3, 8, 30, 0, DateTimeKind.Local); // last Thursday

        Assert.True(SchedulerMath.IsDue(job, now));
    }

    [Fact]
    public void IsDue_Monthly_AfterLastMonthsRun()
    {
        var job = Job(ScheduleType.Monthly, "02:00", dayOfMonth: 10);
        var now = new DateTime(2026, 9, 10, 9, 0, 0);
        job.LastRunUtc = new DateTime(2026, 8, 10, 2, 30, 0, DateTimeKind.Local);

        Assert.True(SchedulerMath.IsDue(job, now));
    }

    // ----- Describe -----

    [Fact]
    public void Describe_Daily()
    {
        // Describe depends on CurrentUICulture after the localization work, so pin it. The
        // expected en-US string is unchanged (the HH:mm time stays literal by design).
        using (new CultureScope("en-US"))
        {
            var job = Job(ScheduleType.Daily, "02:00");

            Assert.Equal("Daily at 02:00 (Full)", SchedulerMath.Describe(job));
        }
    }
}
