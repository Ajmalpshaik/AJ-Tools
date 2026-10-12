using System;
using Autodesk.Revit.UI;
using AJTools.AiShell.Views;
using AJTools.AiShell.ViewModels;
using AJTools.AiShell.Services;
using AJTools.AiShell.Configuration;

namespace AJTools.AiShell.DockablePane
{
    public class AiShellPaneProvider : IDockablePaneProvider
    {
        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("d4a8e32d-1a8c-4f9e-a89e-4a6c4b2d3c1d"));
        private readonly AiShellView _view;

        public AiShellPaneProvider()
        {
            // Simple DI setup for the Pane. The config is the session-wide shared instance (see
            // AiShellConfig.Shared) so Saved Scripts / Run Pinned and this pane never save over
            // each other's changes.
            var config = AiShellConfig.Shared;
            var geminiService = new GeminiApiService(config);
            var openAiService = new OpenAiApiService(config);
            var anthropicService = new AnthropicApiService(config);
            var nvidiaService = new NvidiaApiService(config);
            var roslynService = new RoslynService();
            var revitExecService = new RevitExecutionService(roslynService);
            var contextService = new RevitContextExtractionService();
            var replService = new ReplSessionService(roslynService);
            var snoopService = new ElementSnoopService();

            var vm = new AiShellViewModel(config, geminiService, openAiService, anthropicService, nvidiaService, revitExecService, contextService, replService, snoopService, GetRunningRevitYear());

            _view = new AiShellView { DataContext = vm };
        }

        /// <summary>The year of the Revit this add-in is running in (2020, 2024, ...), so the AI is
        /// told which Revit API its script will be compiled against. Read from the loaded RevitAPI
        /// assembly, whose major version is the year minus 2000 (checked against the installed
        /// RevitAPI.dll of Revit 2020, 2024, 2026 and 2027: 20.x, 24.x, 26.x, 27.x). This pane is
        /// built during Revit's OnStartup with no API context, so no Application object is used.
        /// If the read ever fails it falls back to a year with the same unit API as this build
        /// (2020, 2021, or 2022 for any 2022+ build) - the part of the prompt the year decides.</summary>
        private static int GetRunningRevitYear()
        {
            try
            {
                int major = typeof(Autodesk.Revit.DB.Document).Assembly.GetName().Version.Major;
                if (major >= 20 && major < 100)
                {
                    return 2000 + major;
                }
            }
            catch (Exception)
            {
                // Fall through to the build's own version family below.
            }

#if REVIT2022_OR_GREATER
            return 2022;
#elif REVIT2021_OR_GREATER
            return 2021;
#else
            return 2020;
#endif
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = _view as System.Windows.FrameworkElement;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                MinimumWidth = 300
            };
        }
    }
}
