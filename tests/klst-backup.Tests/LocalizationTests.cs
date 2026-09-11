using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using KlstBackup.Models;
using KlstBackup.Resources;
using KlstBackup.Services;
using KlstBackup.ViewModels;
using Xunit;

namespace KlstBackup.Tests;

/// <summary>
/// Guards the localization feature: resource-key parity across satellites, the hand-written
/// <see cref="Strings"/> accessor, the display-only converters, and the split between
/// locale-adaptive display and invariant persistence.
/// </summary>
public class LocalizationTests
{
    /// <summary>An enum type that intentionally has no <c>Enum_TestColor_*</c> resource keys,
    /// so the display converter must fall back to the raw member name rather than blank.</summary>
    private enum TestColor
    {
        Crimson
    }

    // ----- 1. Resource parity across satellites -----

    [Fact]
    public void Resources_AllKeysPresentInEverySatellite()
    {
        var neutral = Strings.ResourceManager.GetResourceSet(
            CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true);

        Assert.True(neutral is not null,
            "Neutral resource set (Strings.resx) is null. The ResourceManager base name " +
            "'KlstBackup.Resources.Strings' is wrong or the neutral resx was not embedded.");

        var neutralKeys = neutral!.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .ToList();
        Assert.NotEmpty(neutralKeys);

        foreach (var tag in new[] { "zh-HK", "zh-CN" })
        {
            // tryParents: false is the whole point - a satellite must never be silently
            // satisfied by English parent fallback. A null here is the highest-impact silent
            // failure in this feature (a missing/mis-deployed satellite renders as an
            // all-English app with no error), so the message names the likely culprits.
            var satellite = Strings.ResourceManager.GetResourceSet(
                CultureInfo.GetCultureInfo(tag), createIfNotExists: true, tryParents: false);

            Assert.True(satellite is not null,
                $"Satellite resource set for '{tag}' is null. Check that the satellite assembly " +
                $"was produced/deployed and that the 'SatelliteResourceLanguages' property in " +
                $"klst-backup.csproj still includes '{tag}'.");

            var satelliteKeys = satellite!.Cast<DictionaryEntry>()
                .Select(e => (string)e.Key)
                .ToList();

            // Every neutral key resolves to a non-empty value in the satellite.
            foreach (var key in neutralKeys)
            {
                var value = satellite.GetString(key);
                Assert.False(string.IsNullOrEmpty(value),
                    $"Key '{key}' is missing or empty in the '{tag}' satellite.");
            }

            // And the satellite carries no extra keys absent from neutral (drift guard).
            var extra = satelliteKeys.Except(neutralKeys).ToList();
            Assert.True(extra.Count == 0,
                $"Satellite '{tag}' has keys absent from neutral: {string.Join(", ", extra)}");
        }
    }

    // ----- 2. Hand-written accessor parity -----

    [Fact]
    public void Strings_EveryPropertyHasAResourceEntry()
    {
        var properties = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .ToList();
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var value = (string?)property.GetValue(null);
            Assert.False(string.IsNullOrEmpty(value),
                $"Strings.{property.Name} returned null or empty.");

            // Get(key) degrades to the key itself (?? key) when no resx entry exists, so a value
            // equal to the property name means a hand-written property with no resx backing.
            Assert.NotEqual(property.Name, value);
        }
    }

    // ----- 3. Enum display converter fallback -----

    [Fact]
    public void EnumDisplayNameConverter_UnknownEnum_FallsBackToName()
    {
        var converter = new EnumDisplayNameConverter();

        var result = converter.Convert(TestColor.Crimson, typeof(string), null!, CultureInfo.InvariantCulture);

        var text = Assert.IsType<string>(result);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Equal(nameof(TestColor.Crimson), text);
    }

    // ----- 4. RunMessage converter sentinels vs pass-through -----

    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-HK")]
    [InlineData("zh-CN")]
    public void RunMessageConverter_SentinelsLocalized_OtherValuesPassThrough(string culture)
    {
        using (new CultureScope(culture))
        {
            var converter = new RunMessageConverter();

            Assert.Equal(Strings.Enum_RunStatus_Cancelled,
                converter.Convert("Cancelled", typeof(string), null!, CultureInfo.InvariantCulture));
            Assert.Equal(Strings.Enum_RunStatus_Failed,
                converter.Convert("Failed", typeof(string), null!, CultureInfo.InvariantCulture));

            // A set-folder name and an arbitrary engine diagnostic are NOT sentinels; they must
            // pass through byte-identical so nothing localized reaches persisted history text.
            const string setFolder = "Full_20260910_020000";
            const string diagnostic = "Source folder not found: C:\\does_not_exist";
            Assert.Equal(setFolder,
                converter.Convert(setFolder, typeof(string), null!, CultureInfo.InvariantCulture));
            Assert.Equal(diagnostic,
                converter.Convert(diagnostic, typeof(string), null!, CultureInfo.InvariantCulture));
        }
    }

    // ----- 5. SchedulerMath.Describe follows the UI culture -----

    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-HK")]
    [InlineData("zh-CN")]
    public void SchedulerMath_Describe_UsesUiCulture(string culture)
    {
        using (new CultureScope(culture))
        {
            var daily = Job(ScheduleType.Daily, "02:00");
            var described = SchedulerMath.Describe(daily);

            if (culture == "en-US")
            {
                Assert.Equal("Daily at 02:00 (Full)", described);
                return;
            }

            // Non-English output...
            Assert.NotEqual("Daily at 02:00 (Full)", described);
            // ...and a Weekly job's weekday text equals the culture's own day name.
            var weekly = Job(ScheduleType.Weekly, "02:00", DayOfWeek.Monday);
            var weeklyText = SchedulerMath.Describe(weekly);
            var expectedDay = CultureInfo.CurrentUICulture.DateTimeFormat.GetDayName(DayOfWeek.Monday);
            Assert.Contains(expectedDay, weeklyText, StringComparison.Ordinal);
        }
    }

    // ----- 6. TimeFormatter.Short follows CurrentCulture, not CurrentUICulture -----

    [Theory]
    [InlineData("en-US", "zh-HK")]
    [InlineData("zh-CN", "en-US")]
    [InlineData("de-DE", "zh-CN")]
    public void TimeFormatter_Short_FollowsCurrentCulture(string culture, string uiCulture)
    {
        // culture and uiCulture are deliberately different to prove the decoupling: Short must
        // follow CurrentCulture (regional format) and ignore CurrentUICulture (display language).
        using (new CultureScope(culture, uiCulture))
        {
            var value = new DateTime(2026, 9, 11, 14, 30, 0);
            var expected = value.ToString("g", CultureInfo.GetCultureInfo(culture));

            Assert.Equal(expected, TimeFormatter.Short(value));
        }
    }

    // ----- 7. FormatBytes is invariant -----

    [Fact]
    public void FormatBytes_IsInvariant()
    {
        using (new CultureScope("de-DE"))
        {
            // de-DE would otherwise render "1,00 KB"; FormatBytes is pinned to invariant
            // because its output is echoed verbatim into run log files.
            Assert.Equal("1.00 KB", BackupEngine.FormatBytes(1024));
        }
    }

    // ----- 8. Set-folder name round-trips under a non-Gregorian calendar -----

    [Fact]
    public void SetFolderName_RoundTrips_UnderNonGregorianCalendar()
    {
        using (new CultureScope("th-TH")) // Buddhist calendar: 2026 renders as year 2569
        {
            var now = new DateTime(2026, 9, 10, 2, 0, 0);

            // Write side mirrors BackupEngine.RunBackup: prefix + ToString("yyyyMMdd_HHmmss",
            // InvariantCulture). PAIRED WRITE SITE: src\klst-backup\Services\BackupEngine.cs
            // (the "yyyyMMdd_HHmmss" stamp). Keep this pattern in sync with that line. The write
            // is not reachable without performing file I/O, so the format string is duplicated
            // here; the read side below uses the real ManifestStore.ParseSetFolderName.
            var setName = "Full_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            var parsed = ManifestStore.ParseSetFolderName(setName);

            Assert.NotNull(parsed);
            // The recovered date must be Gregorian, not Buddhist-era, regardless of ambient culture.
            Assert.Equal(2026, parsed!.Value.Year);
            Assert.Equal(9, parsed.Value.Month);
            Assert.Equal(10, parsed.Value.Day);
            Assert.Equal(2, parsed.Value.Hour);
        }
    }

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
}
