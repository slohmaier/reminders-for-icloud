using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace RemindersForICloud;

/// <summary>
/// Tiny launcher that opens iCloud Reminders in Microsoft Edge "app mode"
/// (<c>--app=&lt;URL&gt;</c>) with its own isolated user-data-dir.
///
/// We tried embedding iCloud in a WebView2 first. WebView2 is the Edge
/// engine, but iCloud's SPA serves a different (less accessible) code path
/// to it — UA / UA-CH / brand spoofing did not move the needle. The only
/// reliable fix is to *be* Edge, so this launcher does exactly that and
/// then exits.
/// </summary>
internal static class Program
{
    private const string TargetUrl = "https://www.icloud.com/reminders";
    private const string AppDataFolder = "RemindersForICloud";
    private const string AppTitle = "Reminders for iCloud";
    private const string MutexName = "Global\\RemindersForICloud-Launcher-8b4e1d7f";

    [STAThread]
    private static int Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        try
        {
            LaunchEdgeAppMode();
            Thread.Sleep(500);
            return 0;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            return 1;
        }
    }

    private static void LaunchEdgeAppMode()
    {
        var profileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolder,
            "EdgeProfile");
        Directory.CreateDirectory(profileDir);

        var edge = FindEdgeExecutable()
            ?? throw new FileNotFoundException(
                "Microsoft Edge (msedge.exe) was not found. It must be installed to run this app.");

        var psi = new ProcessStartInfo
        {
            FileName = edge,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Normal,
        };
        psi.ArgumentList.Add($"--app={TargetUrl}");
        psi.ArgumentList.Add($"--user-data-dir={profileDir}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");

        Process.Start(psi);
    }

    private static string? FindEdgeExecutable()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
            var path = key?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
        }
        foreach (var candidate in new[]
                 {
                     @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                     @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                 })
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static void ShowError(string message)
    {
        const uint MB_ICONERROR = 0x10;
        MessageBoxW(IntPtr.Zero, message, AppTitle, MB_ICONERROR);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
