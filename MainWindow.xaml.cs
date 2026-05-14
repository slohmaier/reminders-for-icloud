using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace RemindersForICloud;

public partial class MainWindow : Window
{
    private const string TargetUrl = "https://www.icloud.com/reminders";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RemindersForICloud");
        var userDataFolder = Path.Combine(dataRoot, "WebView2Data");
        var logPath = Path.Combine(dataRoot, "diagnostic.log");
        Directory.CreateDirectory(userDataFolder);

        var options = new CoreWebView2EnvironmentOptions
        {
            // Force Chromium to emit the full accessibility tree on every
            // renderer. Without this, embedded WebView2 lazily builds the
            // tree only when a UIA client connects, which breaks iCloud's
            // ARIA grid / listbox keyboard navigation (arrow up/down does
            // not move between rows in the notes and reminders lists).
            AdditionalBrowserArguments = "--force-renderer-accessibility=complete",
        };

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;

        // Strip the optional "WebView2/X.Y.Z.W" segment that some Edge
        // versions append to the UA string. iCloud (and other UA-sniffing
        // sites) then sees us as plain stable Edge.
        var originalUa = core.Settings.UserAgent;
        var cleanedUa = Regex.Replace(
            originalUa,
            @"\s+WebView2/\S+",
            "",
            RegexOptions.IgnoreCase).Trim();
        core.Settings.UserAgent = cleanedUa;

        TryWriteDiagnostic(logPath, originalUa, cleanedUa);

        core.NewWindowRequested += (s, args) =>
        {
            args.Handled = true;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = args.Uri,
                    UseShellExecute = true
                });
            }
            catch { }
        };

        core.DocumentTitleChanged += (s, _) =>
        {
            var t = core.DocumentTitle;
            Title = string.IsNullOrWhiteSpace(t) ? "Reminders for iCloud" : $"{t} – Reminders for iCloud";
        };

        WebView.Source = new Uri(TargetUrl);
        WebView.Focus();
    }

    private static void TryWriteDiagnostic(string logPath, string originalUa, string effectiveUa)
    {
        try
        {
            var lines = new[]
            {
                $"== {DateTime.UtcNow:O} ==",
                $"original UA : {originalUa}",
                $"effective UA: {effectiveUa}",
                $"target URL  : {TargetUrl}",
                ""
            };
            File.AppendAllLines(logPath, lines);
        }
        catch
        {
            // Diagnostic logging must never affect app startup.
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 && WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.Alt && WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.Navigate(TargetUrl);
            e.Handled = true;
        }
    }
}
