using System;
using System.Globalization;

namespace KlstBackup.Tests;

/// <summary>
/// Pins <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>
/// for the duration of a test and restores both on dispose. Matches the existing
/// <see cref="IDisposable"/> convention used by <c>BackupEngineTests</c>.
/// <para>
/// Deliberately touches ONLY the thread-local CurrentCulture / CurrentUICulture properties and
/// NEVER <see cref="CultureInfo.DefaultThreadCurrentCulture"/> /
/// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>: those are process-wide, and xUnit
/// runs test classes in parallel, so mutating them here would leak into other test classes and
/// cause flaky cross-class interference.
/// </para>
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _oldCulture;
    private readonly CultureInfo _oldUiCulture;

    /// <summary>Pins both the regional format and the UI language to the same culture.</summary>
    public CultureScope(string culture)
        : this(culture, culture)
    {
    }

    /// <summary>Pins the regional format (CurrentCulture) and the UI language (CurrentUICulture)
    /// independently - the overload that proves the two are decoupled.</summary>
    public CultureScope(string culture, string uiCulture)
    {
        _oldCulture = CultureInfo.CurrentCulture;
        _oldUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _oldCulture;
        CultureInfo.CurrentUICulture = _oldUiCulture;
    }
}
