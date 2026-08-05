using Autodesk.Revit.UI;
using AJTools.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace AJTools.UI
{
    public partial class AboutWindow : Window
    {
        private readonly UIApplication _uiApp;
        private readonly Dictionary<string, FrameworkElement> _sections;
        private readonly List<Button> _navButtons;

        private static readonly SolidColorBrush ActiveButtonBackground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private static readonly SolidColorBrush ActiveButtonForeground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
        private static readonly SolidColorBrush InactiveButtonForeground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAB4C5"));

        private const string GitHubUrl = "https://github.com/Ajmalpshaik/AJ-Tools";
        private const string LinkedInUrl = "https://www.linkedin.com/in/ajmalps/";
        private const string SupportEmail = "mailto:ajmalnattika@gmail.com";

        // A maximized window must have square corners or the desktop shows through all four.
        // Children get 21 rather than 22 because they sit inside the shell's 1px border and
        // a concentric inner radius is outer minus thickness.
        private static readonly CornerRadius ShellCorners = new CornerRadius(22);
        private static readonly CornerRadius SidebarCorners = new CornerRadius(21, 0, 0, 21);
        private static readonly CornerRadius HeaderCorners = new CornerRadius(0, 21, 0, 0);
        private static readonly CornerRadius FooterCorners = new CornerRadius(0, 0, 21, 0);
        private static readonly CornerRadius SquareCorners = new CornerRadius(0);

        // Motion state. The entrance is fully declarative (Window.Triggers in the XAML);
        // only the exit, the section swap, and the ambient pulse need code, because WPF
        // cannot delay a window close or retarget a shared storyboard from markup alone.
        private Storyboard _ambientPulse;
        private bool _isLoaded;
        private bool _isExitPlaying;
        private bool _isReadyToClose;

        static AboutWindow()
        {
            ActiveButtonBackground.Freeze();
            ActiveButtonForeground.Freeze();
            InactiveButtonForeground.Freeze();
        }

        public AboutWindow(UIApplication uiApp)
        {
            InitializeComponent();

            _uiApp = uiApp;
            _sections = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
            {
                { "general", GeneralPanel },
                { "developer", DeveloperPanel },
                { "tools", ToolsPanel },
                { "updates", UpdatesPanel },
                { "license", LicensePanel }
            };

            _navButtons = new List<Button>
            {
                BtnGeneral,
                BtnDeveloper,
                BtnTools,
                BtnUpdates,
                BtnLicense
            };

            LoadData();
            LoadWindowIcon();

            // Wired here rather than as XAML attributes on the <Window> tag, and read through
            // _isLoaded below, so the first ShowSection call cannot animate mid-construction.
            Loaded += AboutWindow_Loaded;
            Closing += AboutWindow_Closing;
            StateChanged += AboutWindow_StateChanged;

            ShowSection("general");
        }

        /// <summary>
        /// Flattens the rounded corners while maximized and restores them otherwise. Hooked to
        /// StateChanged rather than to the maximize button, so it also covers the header
        /// double-click, Win+Up, and any restore Revit or Windows triggers on its own.
        /// </summary>
        private void AboutWindow_StateChanged(object sender, EventArgs e)
        {
            bool isMaximized = WindowState == System.Windows.WindowState.Maximized;

            RootShell.CornerRadius = isMaximized ? SquareCorners : ShellCorners;
            SidebarPanel.CornerRadius = isMaximized ? SquareCorners : SidebarCorners;
            HeaderBar.CornerRadius = isMaximized ? SquareCorners : HeaderCorners;
            FooterBar.CornerRadius = isMaximized ? SquareCorners : FooterCorners;
        }

        private void AboutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            StartAmbientPulse();
        }

        /// <summary>
        /// Ambient layer: the status dot breathes once the entrance has landed.
        /// Held in a field so it can be stopped on close - a RepeatBehavior.Forever clock
        /// would otherwise keep ticking against a dead window for the life of the Revit session.
        /// </summary>
        private void StartAmbientPulse()
        {
            try
            {
                var template = FindResource("StatusPulseStoryboard") as Storyboard;
                if (template == null)
                {
                    return;
                }

                _ambientPulse = template.Clone();
                Storyboard.SetTarget(_ambientPulse, StatusDot);
                _ambientPulse.Begin();
            }
            catch
            {
                // Motion is cosmetic: the window is fully usable without the pulse.
                _ambientPulse = null;
            }
        }

        /// <summary>
        /// Plays the exit storyboard before the window actually closes. Every close request is
        /// blocked until the animation finishes; only the storyboard's own completion callback
        /// is allowed through.
        ///
        /// The two flags are not interchangeable. The Close button is both Click="CloseButton_Click"
        /// (which calls Close) and IsCancel="True" (which sets DialogResult, which also calls
        /// Close), so a single click raises Closing twice. A one-flag "already started" guard
        /// would let that second call straight through and cut the exit off mid-flight.
        /// </summary>
        private void AboutWindow_Closing(object sender, CancelEventArgs e)
        {
            // The storyboard finished and asked for the real close.
            if (_isReadyToClose)
            {
                return;
            }

            // Everything else waits for the animation, including the IsCancel second pass.
            e.Cancel = true;

            if (_isExitPlaying)
            {
                return;
            }

            if (_ambientPulse != null)
            {
                _ambientPulse.Stop();
                _ambientPulse = null;
            }

            try
            {
                var template = FindResource("WindowExitStoryboard") as Storyboard;
                if (template == null)
                {
                    ReleaseClose(e);
                    return;
                }

                // Clone: a Storyboard held in a ResourceDictionary can be frozen, and a frozen
                // Freezable refuses both .Completed handlers and .Stop().
                Storyboard exit = template.Clone();
                exit.Completed += (s, args) =>
                {
                    _isReadyToClose = true;
                    Close();
                };

                _isExitPlaying = true;
                exit.Begin(this);
            }
            catch
            {
                // If the exit animation cannot run, close normally rather than trapping the user.
                ReleaseClose(e);
            }
        }

        private void ReleaseClose(CancelEventArgs e)
        {
            _isReadyToClose = true;
            e.Cancel = false;
        }

        private void LoadWindowIcon()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                var iconLoader = new IconLoader(assemblyPath);
                BitmapSource icon = iconLoader.LoadLarge("About.png");
                if (icon != null)
                {
                    Icon = icon;
                }
            }
            catch
            {
                // Non-critical: the window still works without a taskbar/alt-tab icon.
            }
        }

        private void LoadData()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = string.Format("v{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            }

            string revitVersion = "2020";
            if (_uiApp != null && _uiApp.Application != null && !string.IsNullOrWhiteSpace(_uiApp.Application.VersionNumber))
            {
                revitVersion = _uiApp.Application.VersionNumber;
            }

            RevitVersionText.Text = "Revit " + revitVersion;
            FrameworkText.Text = GetFrameworkVersionLabel();
            DeploymentText.Text = GetDeploymentLabel();
            LicenseKeyText.Text = "AJ-TOOLS-REVIT-" + revitVersion;
            CopyrightText.Text = "Created & All Rights Reserved @ Ajmal P.S.";
        }

        private static string GetFrameworkVersionLabel()
        {
            var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.FrameworkName))
            {
                return "4.7.2";
            }

            Match match = Regex.Match(attribute.FrameworkName, @"Version=v(?<version>[\d\.]+)");
            return match.Success ? match.Groups["version"].Value : "4.7.2";
        }

        private static string GetDeploymentLabel()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrWhiteSpace(assemblyLocation))
                {
                    return "GitHub Release / Add-in Package";
                }

                string directory = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
                if (Regex.IsMatch(directory, @"Autodesk\\Revit\\Addins\\\d{4}", RegexOptions.IgnoreCase))
                {
                    return "Revit Add-ins Folder";
                }

                return "GitHub Release / Add-in Package";
            }
            catch
            {
                return "GitHub Release / Add-in Package";
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            string sectionKey = button.Tag as string;
            if (string.IsNullOrWhiteSpace(sectionKey))
            {
                return;
            }

            ShowSection(sectionKey);
        }

        private void ShowSection(string key)
        {
            FrameworkElement shownSection = null;

            foreach (KeyValuePair<string, FrameworkElement> entry in _sections)
            {
                bool isMatch = entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase);
                entry.Value.Visibility = isMatch ? Visibility.Visible : Visibility.Collapsed;

                if (isMatch)
                {
                    shownSection = entry.Value;
                }
            }

            foreach (Button button in _navButtons)
            {
                bool isActive = string.Equals(button.Tag as string, key, StringComparison.OrdinalIgnoreCase);
                button.Background = isActive ? ActiveButtonBackground : Brushes.Transparent;
                button.Foreground = isActive ? ActiveButtonForeground : InactiveButtonForeground;
                button.BorderBrush = Brushes.Transparent;
            }

            // Skipped for the constructor's initial call - that section arrives with the entrance.
            if (_isLoaded && shownSection != null)
            {
                PlaySectionEnter(shownSection);
            }
        }

        /// <summary>
        /// Rises the newly shown section into place using the same easing as the entrance, so a
        /// section swap reads as part of the same motion language rather than a hard cut.
        /// </summary>
        private void PlaySectionEnter(FrameworkElement section)
        {
            try
            {
                var template = FindResource("SectionEnterStoryboard") as Storyboard;
                if (template == null)
                {
                    return;
                }

                Storyboard enter = template.Clone();
                Storyboard.SetTarget(enter, section);
                enter.Begin();
            }
            catch
            {
                // Motion is cosmetic: the section is already visible either way.
            }
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenExternal(GitHubUrl);
        }

        private void LinkedInButton_Click(object sender, RoutedEventArgs e)
        {
            OpenExternal(LinkedInUrl);
        }

        private void SupportButton_Click(object sender, RoutedEventArgs e)
        {
            OpenExternal(SupportEmail);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private static void OpenExternal(string target)
        {
            try
            {
                var startInfo = new ProcessStartInfo(target)
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AJ Tools", "Unable to open link.\n\n" + ex.Message);
            }
        }
    }
}
