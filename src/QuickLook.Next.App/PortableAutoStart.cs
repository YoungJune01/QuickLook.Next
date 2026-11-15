using Microsoft.Win32;
using QuickLook.Next.Core;

namespace QuickLook.Next.App;

internal static class PortableAutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static PortableStartupRegistration Registration => new(
        AppStorage.Current.PortableRoot!, Environment.GetFolderPath(Environment.SpecialFolder.System));

    public static bool IsSupported => Registration.IsComplete;

    public static bool IsEnabled()
    {
        var registration = Registration;
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
        return registration.IsComplete && registration.OwnsCommand(key?.GetValue(registration.ValueName));
    }

    public static bool SetEnabled(bool enabled)
    {
        var registration = Registration;
        // Windows Run commands are limited to 260 characters. Do not report success for
        // an entry Windows cannot launch (the user can move a deeply nested copy first).
        if (enabled && (!registration.IsComplete || registration.Command.Length > 260))
            return false;
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null)
            return false;
        object? previous = key.GetValue(registration.ValueName);
        // Do not overwrite or delete a different command, even at our per-location name.
        if (previous is not null && !registration.OwnsCommand(previous))
            return false;
        if (enabled)
            key.SetValue(registration.ValueName, registration.Command, RegistryValueKind.String);
        else
            key.DeleteValue(registration.ValueName, throwOnMissingValue: false);
        return enabled == registration.OwnsCommand(key.GetValue(registration.ValueName));
    }
}
