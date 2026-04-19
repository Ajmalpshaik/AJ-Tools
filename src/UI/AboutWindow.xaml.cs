using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            ShowSection("general");
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
            CopyrightText.Text = string.Format("(c) {0} AJ Tools", DateTime.Now.Year);
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
                if (directory.IndexOf("Autodesk\\Revit\\Addins\\2020", StringComparison.OrdinalIgnoreCase) >= 0)
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
            foreach (KeyValuePair<string, FrameworkElement> entry in _sections)
            {
                entry.Value.Visibility = entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            foreach (Button button in _navButtons)
            {
                bool isActive = string.Equals(button.Tag as string, key, StringComparison.OrdinalIgnoreCase);
                button.Background = isActive ? ActiveButtonBackground : Brushes.Transparent;
                button.Foreground = isActive ? ActiveButtonForeground : InactiveButtonForeground;
                button.BorderBrush = Brushes.Transparent;
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
