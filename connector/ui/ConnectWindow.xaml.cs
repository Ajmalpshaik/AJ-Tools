#region Metadata
/*
 * Tool Name     : AJ Connect (control window)
 * File Name     : ConnectWindow.xaml.cs
 * Purpose       : The only window this add-in has. One button connects and disconnects, one opens
 *                 the tools page in the browser, and the tool source (a local folder, or Ajmal's
 *                 website) can be changed here without running any script.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : ConnectorServer, ToolCatalogue
 *
 * Input         : None - reads its state from the running server.
 * Output        : Starts/stops the server, opens the browser, saves the tool source.
 *
 * Notes         :
 * - Validation happens INSIDE the window, never after it closes. The house pattern: check on every
 *   TextChanged, show the reason inline, and disable Save - no popup, and the typed value is never
 *   thrown away by a dialog that closes underneath the person.
 * - Deliberately no window entrance/exit motion. AJ Tools gets that from WindowMotionHelper, which
 *   this add-in intentionally does not carry - Ajmal chose a bare connector with no AJ Tools
 *   helpers, and one small window is not worth copying that machinery for.
 * - The owner handle is set by the caller (CmdOpenConnect) before ShowDialog. Without it a Revit
 *   dialog can drop behind the main window, and with ShowInTaskbar="False" there is then no way to
 *   bring it back.
 * - Any bare Visibility value in this class must be written as System.Windows.Visibility.X - Window
 *   already has an instance property called Visibility, which shadows the enum's type name and makes
 *   the short form fail to compile (CS0176).
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace AJToolsConnector.UI
{
    public partial class ConnectWindow : Window
    {
        private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0x9B, 0x57));
        private static readonly Brush StopBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x34, 0x2B));
        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x76, 0xFF));
        private static readonly Brush DangerBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x34, 0x2B));
        private static readonly Brush HintBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x8A, 0xA0));

        private readonly ConnectorServer _server;
        private bool _loaded;

        public ConnectWindow(ConnectorServer server)
        {
            InitializeComponent();

            _server = server;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = "Revit tools in your browser  ·  v" + version.Major + "." + version.Minor + "." + version.Build;

            SourceBox.Text = ToolCatalogue.ReadSourceSetting();
            _loaded = true;

            UpdateState();
        }

        #region State

        private void UpdateState()
        {
            bool running = _server != null && _server.IsRunning;

            StatusDot.Fill = running ? OkBrush : StopBrush;
            StatusText.Text = running ? "Connected" : "Not connected";
            AddressText.Text = running ? _server.Url : string.Empty;

            ConnectButton.Content = running ? "Disconnect" : "Connect";
            ConnectButton.Background = running ? StopBrush : AccentBrush;

            OpenButton.IsEnabled = running;

            // Keep the ribbon icon honest while the window is still open, not only on close.
            ConnectorApp.ApplyIcon(running);
        }

        #endregion

        #region Buttons

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_server == null) return;

            if (_server.IsRunning)
            {
                _server.Stop();
                UpdateState();
                return;
            }

            string error;
            if (!_server.Start(out error))
            {
                // Inline, not a popup - the window stays open so the person can act on it.
                SourceHint.Text = error;
                SourceHint.Foreground = DangerBrush;
                return;
            }

            UpdateState();

            // Connecting is almost always followed by wanting the page, so save the second click.
            OpenInBrowser(_server.Url);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_server != null && _server.IsRunning) OpenInBrowser(_server.Url);
        }

        private void UseLocalButton_Click(object sender, RoutedEventArgs e)
        {
            SourceBox.Text = ToolCatalogue.DefaultLocalFolder;
        }

        private void SaveSourceButton_Click(object sender, RoutedEventArgs e)
        {
            string problem;
            if (!Validate(out problem))
            {
                SourceHint.Text = problem;
                SourceHint.Foreground = DangerBrush;
                return;
            }

            try
            {
                ToolCatalogue.WriteSourceSetting(SourceBox.Text.Trim());

                SourceHint.Text = "Saved. Press \"Check for new tools\" on the page - no restart needed.";
                SourceHint.Foreground = OkBrush;
            }
            catch (Exception ex)
            {
                SourceHint.Text = "Could not save: " + ex.Message;
                SourceHint.Foreground = DangerBrush;
            }
        }

        #endregion

        #region Validation

        private void SourceBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Fires during InitializeComponent before the fields it touches are ready.
            if (!_loaded) return;

            string problem;
            bool ok = Validate(out problem);

            SaveSourceButton.IsEnabled = ok;
            SourceHint.Text = ok ? "A folder on this computer, or your website address." : problem;
            SourceHint.Foreground = ok ? HintBrush : DangerBrush;
        }

        private bool Validate(out string problem)
        {
            string value = SourceBox.Text == null ? string.Empty : SourceBox.Text.Trim();

            if (value.Length == 0)
            {
                problem = "Enter a folder or a website address.";
                return false;
            }

            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Uri parsed;
                if (!Uri.TryCreate(value, UriKind.Absolute, out parsed))
                {
                    problem = "That does not look like a complete web address.";
                    return false;
                }

                problem = null;
                return true;
            }

            if (value.IndexOf(':') < 0 && !value.StartsWith("\\\\", StringComparison.Ordinal))
            {
                problem = "Enter a full folder path, or an address starting with https://";
                return false;
            }

            problem = null;
            return true;
        }

        #endregion

        private static void OpenInBrowser(string url)
        {
            try
            {
                // UseShellExecute = true is required for a URL on .NET (Revit 2025+), where the plain
                // Process.Start(string) overload defaults it to false and throws on anything that is
                // not a real executable path. Identical behaviour on .NET Framework.
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // The address is on screen either way - a browser that refuses to launch is not worth
                // an error dialog.
            }
        }
    }
}
