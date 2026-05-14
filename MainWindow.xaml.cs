using System;
using System.IO;
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
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RemindersForICloud",
            "WebView2Data");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder);

        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.UserAgent = core.Settings.UserAgent;

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
