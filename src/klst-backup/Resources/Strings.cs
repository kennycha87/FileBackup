using System.Resources;

namespace KlstBackup.Resources;

/// <summary>
/// Strongly-typed accessor over Resources\Strings.resx (+ zh-HK / zh-CN satellites).
/// Hand-written because SDK-style <c>dotnet build</c> does not generate a Designer class
/// from .resx (that is a Visual Studio design-time-only feature); a
/// <c>&lt;Generator&gt;PublicResXFileCodeGenerator&lt;/Generator&gt;</c> entry would therefore
/// never compile from the CLI and would make VS emit a conflicting duplicate class.
/// Keep exactly one property per resx key; LocalizationTests enforces parity in both
/// directions. Every property uses <c>nameof()</c>, so renaming a key is a compile error.
/// <para>
/// Strings follow <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> (the Windows
/// display language, overridable through the Settings picker). Date and number rendering follows
/// <see cref="System.Globalization.CultureInfo.CurrentCulture"/> (the Windows regional format) and
/// is deliberately independent of the language choice - see
/// <see cref="KlstBackup.Services.TimeFormatter"/>.
/// </para>
/// </summary>
public static class Strings
{
    /// <summary>
    /// Base name derives from <c>RootNamespace=KlstBackup</c> plus the <c>Resources\</c> folder.
    /// The hyphenated <c>AssemblyName=klst-backup</c> does not affect it.
    /// </summary>
    private static readonly ResourceManager Rm =
        new("KlstBackup.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>Exposes the manager so converters can build keys dynamically.</summary>
    public static ResourceManager ResourceManager => Rm;

    // ----- Application / branding -----

    /// <summary>Main window title. Brand name: untranslated in every locale.</summary>
    public static string App_Title => Get(nameof(App_Title));

    /// <summary>Caption for every message box. Brand name: untranslated in every locale.</summary>
    public static string Msg_Caption => Get(nameof(Msg_Caption));

    // ----- System tray -----

    /// <summary>Tray tooltip. Brand name: untranslated in every locale (WinForms caps
    /// <c>NotifyIcon.Text</c> at 63 characters).</summary>
    public static string Tray_Tooltip => Get(nameof(Tray_Tooltip));

    public static string Tray_Open => Get(nameof(Tray_Open));

    public static string Tray_Exit => Get(nameof(Tray_Exit));

    public static string Tray_BalloonBody => Get(nameof(Tray_BalloonBody));

    // ----- Tabs -----

    public static string Tab_Jobs => Get(nameof(Tab_Jobs));

    public static string Tab_History => Get(nameof(Tab_History));

    public static string Tab_Restore => Get(nameof(Tab_Restore));

    public static string Tab_Settings => Get(nameof(Tab_Settings));

    // ----- Buttons -----

    public static string Btn_New => Get(nameof(Btn_New));

    public static string Btn_Edit => Get(nameof(Btn_Edit));

    public static string Btn_Delete => Get(nameof(Btn_Delete));

    public static string Btn_RunNow => Get(nameof(Btn_RunNow));

    public static string Btn_Stop => Get(nameof(Btn_Stop));

    public static string Btn_Dashboard => Get(nameof(Btn_Dashboard));

    public static string Btn_Browse => Get(nameof(Btn_Browse));

    public static string Btn_Restore => Get(nameof(Btn_Restore));

    public static string Btn_Ok => Get(nameof(Btn_Ok));

    public static string Btn_Cancel => Get(nameof(Btn_Cancel));

    public static string Btn_Pause => Get(nameof(Btn_Pause));

    public static string Btn_Resume => Get(nameof(Btn_Resume));

    // ----- Dashboard window -----

    public static string Dashboard_Title => Get(nameof(Dashboard_Title));

    public static string Dashboard_ResourceMonitor => Get(nameof(Dashboard_ResourceMonitor));

    public static string Dashboard_CPU => Get(nameof(Dashboard_CPU));

    public static string Dashboard_Memory => Get(nameof(Dashboard_Memory));

    // ----- Job detail labels (trailing colons are part of the value) -----

    public static string Detail_Source => Get(nameof(Detail_Source));

    public static string Detail_Destination => Get(nameof(Detail_Destination));

    public static string Detail_BackupType => Get(nameof(Detail_BackupType));

    public static string Detail_NextRun => Get(nameof(Detail_NextRun));

    public static string Detail_LastRun => Get(nameof(Detail_LastRun));

    public static string Detail_Enabled => Get(nameof(Detail_Enabled));

    // ----- History grid columns -----

    public static string Col_Job => Get(nameof(Col_Job));

    public static string Col_StartedLocal => Get(nameof(Col_StartedLocal));

    public static string Col_Type => Get(nameof(Col_Type));

    public static string Col_Status => Get(nameof(Col_Status));

    public static string Col_Files => Get(nameof(Col_Files));

    public static string Col_Bytes => Get(nameof(Col_Bytes));

    public static string Col_Duration => Get(nameof(Col_Duration));

    public static string Col_Details => Get(nameof(Col_Details));

    // ----- Shared short values -----

    public static string Common_Yes => Get(nameof(Common_Yes));

    public static string Common_No => Get(nameof(Common_No));

    public static string Common_Never => Get(nameof(Common_Never));

    public static string Common_DueNow => Get(nameof(Common_DueNow));

    // ----- Help paragraphs -----

    public static string Hint_Differential => Get(nameof(Hint_Differential));

    public static string Hint_Restore => Get(nameof(Hint_Restore));

    public static string Hint_Tray => Get(nameof(Hint_Tray));

    public static string Hint_AppData => Get(nameof(Hint_AppData));

    // ----- Restore tab -----

    public static string Restore_Heading => Get(nameof(Restore_Heading));

    public static string Restore_Job => Get(nameof(Restore_Job));

    public static string Restore_BackupSet => Get(nameof(Restore_BackupSet));

    public static string Restore_TargetFolder => Get(nameof(Restore_TargetFolder));

    // ----- Job edit dialog -----

    public static string Edit_Title => Get(nameof(Edit_Title));

    public static string Edit_JobName => Get(nameof(Edit_JobName));

    public static string Edit_SourceFolder => Get(nameof(Edit_SourceFolder));

    public static string Edit_Destination => Get(nameof(Edit_Destination));

    public static string Edit_Frequency => Get(nameof(Edit_Frequency));

    /// <summary>The bare fragment rendered between the frequency combos and the time box.</summary>
    public static string Edit_At => Get(nameof(Edit_At));

    public static string Edit_AtToolTip => Get(nameof(Edit_AtToolTip));

    public static string Edit_BackupType => Get(nameof(Edit_BackupType));

    /// <summary>Used twice in the dialog (as Text and as ToolTip) - deliberately one key.</summary>
    public static string Edit_DiffHint => Get(nameof(Edit_DiffHint));

    public static string Edit_Enabled => Get(nameof(Edit_Enabled));

    // ----- Edit dialog validation errors -----

    public static string Err_NameRequired => Get(nameof(Err_NameRequired));

    public static string Err_SourceMissing => Get(nameof(Err_SourceMissing));

    public static string Err_DestRequired => Get(nameof(Err_DestRequired));

    public static string Err_TimeFormat => Get(nameof(Err_TimeFormat));

    // ----- Run kind (the subject of the completion sentences) -----

    public static string Kind_Scheduled => Get(nameof(Kind_Scheduled));

    public static string Kind_Manual => Get(nameof(Kind_Manual));

    // ----- Status bar -----

    public static string Status_Ready => Get(nameof(Status_Ready));

    /// <summary><c>{0}</c> = exception message (an untranslated developer diagnostic).</summary>
    public static string Status_ConfigSaveFailed => Get(nameof(Status_ConfigSaveFailed));

    /// <summary><c>{0}</c> = exception message.</summary>
    public static string Status_StartupFailed => Get(nameof(Status_StartupFailed));

    /// <summary><c>{0}</c> = job name.</summary>
    public static string Status_ScheduledStarted => Get(nameof(Status_ScheduledStarted));

    /// <summary><c>{0}</c> = kind, <c>{1}</c> = job name, <c>{2}</c> = file count,
    /// <c>{3}</c> = formatted bytes, <c>{4}</c> = backup set folder.</summary>
    public static string Status_BackupCompleted => Get(nameof(Status_BackupCompleted));

    /// <summary><c>{0}</c> = kind, <c>{1}</c> = job name.</summary>
    public static string Status_BackupCancelled => Get(nameof(Status_BackupCancelled));

    /// <summary><c>{0}</c> = kind, <c>{1}</c> = job name, <c>{2}</c> = error.</summary>
    public static string Status_BackupFailed => Get(nameof(Status_BackupFailed));

    /// <summary><c>{0}</c> = job name.</summary>
    public static string Status_JobCreated => Get(nameof(Status_JobCreated));

    /// <summary><c>{0}</c> = job name.</summary>
    public static string Status_JobUpdated => Get(nameof(Status_JobUpdated));

    /// <summary><c>{0}</c> = job name.</summary>
    public static string Status_JobDeleted => Get(nameof(Status_JobDeleted));

    public static string Status_SelectJobFirst => Get(nameof(Status_SelectJobFirst));

    public static string Status_Stopping => Get(nameof(Status_Stopping));

    /// <summary><c>{0}</c> = job name.</summary>
    public static string Status_BackingUp => Get(nameof(Status_BackingUp));

    /// <summary><c>{0}</c> = exception message.</summary>
    public static string Status_BackupError => Get(nameof(Status_BackupError));

    /// <summary><c>{0}</c> = files done, <c>{1}</c> = total files, <c>{2}</c> = formatted bytes,
    /// <c>{3}</c> = current file. Looked up once per run - never inside the per-file
    /// progress handler.</summary>
    public static string Status_ProgressFiles => Get(nameof(Status_ProgressFiles));

    /// <summary><c>{0}</c> = backup set name.</summary>
    public static string Status_Restoring => Get(nameof(Status_Restoring));

    /// <summary><c>{0}</c> = files done, <c>{1}</c> = total files, <c>{2}</c> = current file.
    /// Looked up once per run - never inside the per-file progress handler.</summary>
    public static string Status_ProgressRestore => Get(nameof(Status_ProgressRestore));

    /// <summary><c>{0}</c> = restored file count, <c>{1}</c> = formatted bytes.</summary>
    public static string Status_RestoreCompleted => Get(nameof(Status_RestoreCompleted));

    public static string Status_RestoreCancelled => Get(nameof(Status_RestoreCancelled));

    /// <summary><c>{0}</c> = error.</summary>
    public static string Status_RestoreFailed => Get(nameof(Status_RestoreFailed));

    /// <summary><c>{0}</c> = exception message.</summary>
    public static string Status_RestoreError => Get(nameof(Status_RestoreError));

    /// <summary>Shown after the language picker saves; the choice applies on restart.</summary>
    public static string Status_LanguageRestartHint => Get(nameof(Status_LanguageRestartHint));

    // ----- Message boxes and folder browser prompts -----

    /// <summary><c>{0}</c> = job name.</summary>
    public static string Msg_DeleteConfirm => Get(nameof(Msg_DeleteConfirm));

    public static string Msg_DeleteCaption => Get(nameof(Msg_DeleteCaption));

    public static string Msg_Busy => Get(nameof(Msg_Busy));

    public static string Msg_RestoreSelectFirst => Get(nameof(Msg_RestoreSelectFirst));

    public static string Msg_RestorePickFolder => Get(nameof(Msg_RestorePickFolder));

    public static string Msg_OperationBusy => Get(nameof(Msg_OperationBusy));

    public static string Msg_BrowseRestoreTarget => Get(nameof(Msg_BrowseRestoreTarget));

    public static string Msg_BrowseSource => Get(nameof(Msg_BrowseSource));

    public static string Msg_BrowseDest => Get(nameof(Msg_BrowseDest));

    // ----- Schedule descriptions -----

    /// <summary><c>{0}</c> = time as literal HH:mm, <c>{1}</c> = backup type.</summary>
    public static string Schedule_DailyAt => Get(nameof(Schedule_DailyAt));

    /// <summary><c>{0}</c> = localized weekday, <c>{1}</c> = time as literal HH:mm,
    /// <c>{2}</c> = backup type.</summary>
    public static string Schedule_WeeklyAt => Get(nameof(Schedule_WeeklyAt));

    /// <summary><c>{0}</c> = day of month, <c>{1}</c> = time as literal HH:mm,
    /// <c>{2}</c> = backup type.</summary>
    public static string Schedule_MonthlyAt => Get(nameof(Schedule_MonthlyAt));

    // ----- Settings tab -----

    public static string Settings_StartWithWindows => Get(nameof(Settings_StartWithWindows));

    public static string Settings_Language => Get(nameof(Settings_Language));

    // ----- Enum display names, keyed Enum_<TypeName>_<MemberName> -----

    public static string Enum_BackupType_Full => Get(nameof(Enum_BackupType_Full));

    public static string Enum_BackupType_Differential => Get(nameof(Enum_BackupType_Differential));

    public static string Enum_ScheduleType_Daily => Get(nameof(Enum_ScheduleType_Daily));

    public static string Enum_ScheduleType_Weekly => Get(nameof(Enum_ScheduleType_Weekly));

    public static string Enum_ScheduleType_Monthly => Get(nameof(Enum_ScheduleType_Monthly));

    public static string Enum_RunStatus_Completed => Get(nameof(Enum_RunStatus_Completed));

    public static string Enum_RunStatus_CompletedWithWarnings => Get(nameof(Enum_RunStatus_CompletedWithWarnings));

    public static string Enum_RunStatus_Failed => Get(nameof(Enum_RunStatus_Failed));

    public static string Enum_RunStatus_Cancelled => Get(nameof(Enum_RunStatus_Cancelled));

    /// <summary>
    /// Resolves against <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> because
    /// <c>GetString(key)</c> is called without an explicit culture. A missing translation degrades
    /// to the visible key name rather than to a blank label.
    /// </summary>
    private static string Get(string key) => Rm.GetString(key) ?? key;
}
