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

    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RemindersForICloud");
    private static readonly string LogPath = Path.Combine(DataRoot, "diagnostic.log");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var userDataFolder = Path.Combine(DataRoot, "WebView2Data");
        Directory.CreateDirectory(userDataFolder);

        var options = new CoreWebView2EnvironmentOptions
        {
            // Force Chromium to build the renderer accessibility tree on
            // every process, regardless of whether we can detect a UIA
            // client at startup. Without this, iCloud's ARIA grids/lists
            // never fully wire up arrow-key navigation.
            AdditionalBrowserArguments = "--force-renderer-accessibility=complete",
        };

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;

        // (1) UA string: strip "WebView2/x.y.z" segment.
        var originalUa = core.Settings.UserAgent;
        var cleanedUa = Regex.Replace(
            originalUa,
            @"\s+WebView2/\S+",
            "",
            RegexOptions.IgnoreCase).Trim();
        core.Settings.UserAgent = cleanedUa;

        // (2) UA Client Hints: rewrite the `Sec-CH-UA` brand list on every
        // outgoing request so iCloud sees us as "Microsoft Edge", not
        // "Microsoft Edge WebView2". This is the more likely cause of the
        // ARIA list/grid keyboard navigation regression.
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += RewriteClientHints;

        // (3) Brand spoof: override navigator.userAgentData *inside the
        // page* so iCloud's client-side detection sees "Microsoft Edge"
        // instead of "Microsoft Edge WebView2". Must run before iCloud's
        // own scripts — AddScriptToExecuteOnDocumentCreatedAsync does
        // exactly that. This is the actual fix for the list-navigation
        // regression; the network-header rewrite above only spoofed what
        // the server sees, but iCloud's SPA reads navigator.userAgentData
        // locally to decide whether to wire up the ARIA grid keyboard
        // handlers.
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync(BrandSpoofScript);
        }
        catch { }

        // (4) Diagnostic JS — captures what iCloud actually sees *after*
        // our spoofing, and posts it back via chrome.webview.postMessage.
        core.WebMessageReceived += OnDiagnosticMessage;
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync(DiagnosticScript);
        }
        catch { }

        TryWriteDiagnostic($"original UA : {originalUa}");
        TryWriteDiagnostic($"effective UA: {cleanedUa}");
        TryWriteDiagnostic($"target URL  : {TargetUrl}");

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

    private static void RewriteClientHints(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        try
        {
            var headers = args.Request.Headers;
            foreach (var name in new[] { "Sec-CH-UA", "Sec-CH-UA-Full-Version-List" })
            {
                if (!headers.Contains(name)) continue;
                var value = headers.GetHeader(name);
                var renamed = value.Replace(
                    "\"Microsoft Edge WebView2\"",
                    "\"Microsoft Edge\"",
                    StringComparison.Ordinal);
                if (!ReferenceEquals(renamed, value))
                {
                    headers.SetHeader(name, renamed);
                }
            }
        }
        catch
        {
            // Header rewriting must never break the request.
        }
    }

    // Runs at document-creation time, before any page scripts. Replaces the
    // "Microsoft Edge WebView2" brand in navigator.userAgentData with plain
    // "Microsoft Edge", both for the low-entropy `brands` property and the
    // high-entropy getHighEntropyValues() Promise (which iCloud's SPA may
    // use for fingerprinting). Version numbers are preserved verbatim so
    // the result remains a believable Edge build.
    private const string BrandSpoofScript = @"
        (function () {
            try {
                var orig = navigator.userAgentData;
                if (!orig || !Array.isArray(orig.brands)) return;
                function rename(b) {
                    return b && b.brand === 'Microsoft Edge WebView2'
                        ? { brand: 'Microsoft Edge', version: b.version }
                        : b;
                }
                var newBrands = orig.brands.map(rename);
                var origGetHigh = typeof orig.getHighEntropyValues === 'function'
                    ? orig.getHighEntropyValues.bind(orig)
                    : null;
                var spoof = {
                    brands: newBrands,
                    mobile: orig.mobile,
                    platform: orig.platform,
                    getHighEntropyValues: function (hints) {
                        if (!origGetHigh) {
                            return Promise.resolve({
                                brands: newBrands,
                                mobile: orig.mobile,
                                platform: orig.platform,
                            });
                        }
                        return origGetHigh(hints).then(function (r) {
                            if (Array.isArray(r.brands)) r.brands = r.brands.map(rename);
                            if (Array.isArray(r.fullVersionList)) r.fullVersionList = r.fullVersionList.map(rename);
                            return r;
                        });
                    },
                    toJSON: function () {
                        return { brands: newBrands, mobile: orig.mobile, platform: orig.platform };
                    },
                };
                Object.defineProperty(navigator, 'userAgentData', {
                    value: spoof,
                    writable: false,
                    configurable: true,
                });
            } catch (e) {
                // Spoofing must never break the page.
            }
        })();
    ";

    private const string DiagnosticScript = @"
        (function () {
            if (window.__icloudWrapperDiag) return;
            window.__icloudWrapperDiag = true;
            function snapshot() {
                try {
                    var brands = null;
                    if (navigator.userAgentData && navigator.userAgentData.brands) {
                        brands = navigator.userAgentData.brands.map(function (b) {
                            return b.brand + '/' + b.version;
                        }).join(', ');
                    }
                    var probe = null;
                    var sel = '[role=""row""], [role=""option""], [role=""listitem""], [role=""gridcell""]';
                    var first = document.querySelector(sel);
                    if (first) {
                        probe = {
                            tag: first.tagName,
                            role: first.getAttribute('role'),
                            ariaLabel: first.getAttribute('aria-label'),
                            ariaSelected: first.getAttribute('aria-selected'),
                            parentRole: first.parentElement && first.parentElement.getAttribute('role'),
                        };
                    }
                    var msg = {
                        type: 'diag',
                        url: location.href,
                        ua: navigator.userAgent,
                        brands: brands,
                        readyState: document.readyState,
                        firstRow: probe,
                    };
                    window.chrome.webview.postMessage(msg);
                } catch (e) {
                    try {
                        window.chrome.webview.postMessage({ type: 'diag-error', err: String(e) });
                    } catch (_) {}
                }
            }
            window.addEventListener('load', function () { setTimeout(snapshot, 1500); });
            setTimeout(snapshot, 8000);
        })();
    ";

    private void OnDiagnosticMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var json = args.TryGetWebMessageAsString() ?? args.WebMessageAsJson;
            TryWriteDiagnostic($"[js] {json}");
        }
        catch
        {
            // Diagnostics must never affect the app.
        }
    }

    private static void TryWriteDiagnostic(string line)
    {
        try
        {
            File.AppendAllLines(LogPath, new[] { $"{DateTime.UtcNow:O} {line}" });
        }
        catch { }
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
