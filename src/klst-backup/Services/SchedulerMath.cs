using System;
using KlstBackup.Models;

namespace KlstBackup.Services;

/// <summary>
/// Pure schedule calculations (no timers, no I/O) so they can be unit tested.
/// All calculations use local DateTime values; only <see cref="BackupJob.LastRunUtc"/> is UTC.
/// </summary>
public static class SchedulerMath
{
    /// <summary>Next time the job should run, strictly after <paramref name="now"/>.</summary>
    public static DateTime ComputeNextRun(BackupJob job, DateTime now)
    {
        switch (job.ScheduleType)
        {
            case ScheduleType.Daily:
            {
                var today = now.Date.Add(job.Time.ToTimeSpan());
                return today > now ? today : today.AddDays(1);
            }
            case ScheduleType.Weekly:
            {
                var day = job.WeekDay ?? DayOfWeek.Monday;
                int diff = ((int)day - (int)now.DayOfWeek + 7) % 7;
                var candidate = now.Date.AddDays(diff).Add(job.Time.ToTimeSpan());
                if (candidate <= now)
                {
                    candidate = candidate.AddDays(7);
                }

                return candidate;
            }
            case ScheduleType.Monthly:
            {
                var thisMonth = MonthlyOccurrence(now.Year, now.Month, job);
                if (thisMonth > now)
                {
                    return thisMonth;
                }

                var next = now.AddMonths(1);
                return MonthlyOccurrence(next.Year, next.Month, job);
            }
            default:
                return now;
        }
    }

    /// <summary>
    /// True when a scheduled slot has passed since the job's last run, i.e. there is a
    /// scheduled time T with lastRun &lt; T &lt;= now. Missing slots while the app was
    /// closed are caught up on the next tick, but each slot only triggers one run.
    /// </summary>
    public static bool IsDue(BackupJob job, DateTime now)
    {
        if (!job.Enabled)
        {
            return false;
        }

        // A job that has never run becomes due as soon as the current period's slot
        // has passed; slots from before the job existed are not caught up.
        if (job.LastRunUtc is null)
        {
            return CurrentPeriodOccurrence(job, now) <= now;
        }

        var last = job.LastRunUtc.Value.ToLocalTime();

        switch (job.ScheduleType)
        {
            case ScheduleType.Daily:
            {
                var candidate = now.Date.Add(job.Time.ToTimeSpan());
                for (var i = 0; i < 400 && candidate > last; i++)
                {
                    if (candidate <= now)
                    {
                        return true;
                    }

                    candidate = candidate.AddDays(-1);
                }

                return false;
            }
            case ScheduleType.Weekly:
            {
                var day = job.WeekDay ?? DayOfWeek.Monday;
                int diff = ((int)now.DayOfWeek - (int)day + 7) % 7;
                var candidate = now.Date.AddDays(-diff).Add(job.Time.ToTimeSpan());
                for (var i = 0; i < 60 && candidate > last; i++)
                {
                    if (candidate <= now)
                    {
                        return true;
                    }

                    candidate = candidate.AddDays(-7);
                }

                return false;
            }
            case ScheduleType.Monthly:
            {
                var candidate = MostRecentMonthlyOccurrence(job, now);
                for (var i = 0; i < 60 && candidate is not null && candidate.Value > last; i++)
                {
                    if (candidate.Value <= now)
                    {
                        return true;
                    }

                    var prev = candidate.Value.AddMonths(-1);
                    candidate = MonthlyOccurrence(prev.Year, prev.Month, job);
                }

                return false;
            }
            default:
                return false;
        }
    }

    /// <summary>Human readable description of the job's schedule, e.g. "Daily at 02:00 (Full)".</summary>
    public static string Describe(BackupJob job)
    {
        var typeText = job.JobType == BackupType.Full ? "Full" : "Differential";
        return job.ScheduleType switch
        {
            ScheduleType.Daily => $"Daily at {job.Time:HH:mm} ({typeText})",
            ScheduleType.Weekly => $"Every {job.WeekDay ?? DayOfWeek.Monday} at {job.Time:HH:mm} ({typeText})",
            ScheduleType.Monthly => $"Monthly on day {job.DayOfMonth} at {job.Time:HH:mm} ({typeText})",
            _ => typeText
        };
    }

    /// <summary>The scheduled occurrence inside the period that contains <paramref name="now"/> (today for Daily, this week's day for Weekly, ...).</summary>
    private static DateTime CurrentPeriodOccurrence(BackupJob job, DateTime now)
    {
        return job.ScheduleType switch
        {
            ScheduleType.Daily => now.Date.Add(job.Time.ToTimeSpan()),
            ScheduleType.Weekly => now.Date
                .AddDays(-(((int)now.DayOfWeek - (int)(job.WeekDay ?? DayOfWeek.Monday) + 7) % 7))
                .Add(job.Time.ToTimeSpan()),
            ScheduleType.Monthly => MonthlyOccurrence(now.Year, now.Month, job),
            _ => now
        };
    }

    private static DateTime MonthlyOccurrence(int year, int month, BackupJob job)
    {
        var day = Math.Clamp(job.DayOfMonth, 1, 31);
        day = Math.Min(day, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, day).Add(job.Time.ToTimeSpan());
    }

    private static DateTime? MostRecentMonthlyOccurrence(BackupJob job, DateTime now)
    {
        var candidate = MonthlyOccurrence(now.Year, now.Month, job);
        if (candidate > now)
        {
            var prev = now.AddMonths(-1);
            candidate = MonthlyOccurrence(prev.Year, prev.Month, job);
        }

        return candidate;
    }
}
