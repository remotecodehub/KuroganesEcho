using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace KuroganesEcho.Core;

/// <summary>
/// Installs the bundled Array font files for the current user when possible.
/// The game does not require the installation to succeed because its SpriteFonts
/// are already compiled into the content pipeline.
/// </summary>
internal static class FontInstaller
{
    private const string WindowsFontsRegistryPath = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Fonts";

    public static void TryInstallBundledFonts()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            return;

        try
        {
            var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "Array");
            if (!Directory.Exists(sourceDirectory))
                return;

            var destinationDirectory = GetUserFontDirectory();
            Directory.CreateDirectory(destinationDirectory);

            foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*.ttf"))
            {
                var fileName = Path.GetFileName(sourcePath);
                var destinationPath = Path.Combine(destinationDirectory, fileName);
                if (!File.Exists(destinationPath))
                    File.Copy(sourcePath, destinationPath);

                if (OperatingSystem.IsWindows())
                    RegisterWindowsFont(destinationPath, fileName);
            }

            if (OperatingSystem.IsLinux())
                RefreshLinuxFontCache(destinationDirectory);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Array font installation skipped: {exception.Message}");
        }
    }

    private static string GetUserFontDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Fonts");
        }

        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "fonts",
            "KuroganesEcho");
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsFont(string path, string fileName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WindowsFontsRegistryPath);
        key?.SetValue($"Kuroganes Echo {Path.GetFileNameWithoutExtension(fileName)} (TrueType)", path);

        NativeMethods.AddFontResourceEx(path, 0x10, IntPtr.Zero);
    }

    private static void RefreshLinuxFontCache(string directory)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "fc-cache",
                Arguments = $"-f \"{directory}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // The font directory is still valid; a later font-cache refresh can pick it up.
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);
    }
}
