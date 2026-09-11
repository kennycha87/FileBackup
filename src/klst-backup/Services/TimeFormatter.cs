using System;
using System.Globalization;

namespace KlstBackup.Services;

/// <summary>
/// Display-only date/time formatting. Always follows the Windows regional format
/// (<see cref="CultureInfo.CurrentCulture"/>), never the UI language - so a user with a zh-HK
/// display language and an en-US regional format sees Chinese labels with
/// <c>9/11/2026 2:00 PM</c> timestamps, exactly like Windows itself.
/// NEVER use for persisted values (config.json, manifests, log files, backup-set folder names) -
/// those must use <see cref="CultureInfo.InvariantCulture"/>.
/// <para>Static and pure, mirroring the existing <see cref="SchedulerMath"/> testability contract.</para>
/// </summary>
public static class TimeFormatter
{
    /// <summary>Renders a local timestamp with the short date + short time pattern ("g").</summary>
    public static string Short(DateTime value) => value.ToString("g", CultureInfo.CurrentCulture);

    /// <summary>Renders a persisted UTC timestamp as local time in the regional short format.</summary>
    public static string UtcShort(DateTime utc) => utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>
    /// Renders a schedule time as the literal <c>HH:mm</c> the user types into the edit dialog
    /// (see the <c>Edit_AtToolTip</c> resource). Deliberately invariant: display and input must
    /// agree, so a locale whose time separator is not ":" must not change either side.
    /// Named <c>FormatTime</c> rather than <c>TimeOnly</c> to avoid a resolution hazard against
    /// the identically-named parameter type.
    /// </summary>
    public static string FormatTime(TimeOnly value) => value.ToString("HH\\:mm", CultureInfo.InvariantCulture);
}
