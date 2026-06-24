using System;
using Autodesk.Revit.UI;
using AJTools.GeminiShell.Views;
using AJTools.GeminiShell.ViewModels;
using AJTools.GeminiShell.Services;
using AJTools.GeminiShell.Configuration;

namespace AJTools.GeminiShell.DockablePane
{
    public class GeminiShellPaneProvider : IDockablePaneProvider
    {
        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("d4a8e32d-1a8c-4f9e-a89e-4a6c4b2d3c1d"));
        private readonly GeminiShellView _view;

        public GeminiShellPaneProvider()
        {
            // Simple DI setup for the Pane
            var config = GeminiShellConfig.Load();
            var geminiService = new GeminiApiService(config);
            var openAiService = new OpenAiApiService(config);
            var roslynService = new RoslynService();
            var revitExecService = new RevitExecutionService(roslynService);
            var contextService = new RevitContextExtractionService();

            var vm = new GeminiShellViewModel(config, geminiService, openAiService, revitExecService, contextService);
            
            _view = new GeminiShellView { DataContext = vm };
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
