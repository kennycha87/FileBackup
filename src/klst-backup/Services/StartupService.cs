using System;
using Microsoft.Win32;

namespace KlstBackup.Services;

/// <summary>Registers/unregisters the app in the per-user Windows startup (Run key).</summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "FileBackup";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetRegistered(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enable)
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                key.SetValue(AppValueName, $"\"{exe}\"");
            }
        }
        else if (key.GetValue(AppValueName) is not null)
        {
            key.DeleteValue(AppValueName, throwOnMissingValue: false);
        }
    }
}
