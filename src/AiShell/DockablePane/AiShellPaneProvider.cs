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
            // Simple DI setup for the Pane
            var config = AiShellConfig.Load();
            var geminiService = new GeminiApiService(config);
            var openAiService = new OpenAiApiService(config);
            var anthropicService = new AnthropicApiService(config);
            var nvidiaService = new NvidiaApiService(config);
            var roslynService = new RoslynService();
            var revitExecService = new RevitExecutionService(roslynService);
            var contextService = new RevitContextExtractionService();
            var replService = new ReplSessionService(roslynService);
            var snoopService = new ElementSnoopService();

            var vm = new AiShellViewModel(config, geminiService, openAiService, anthropicService, nvidiaService, revitExecService, contextService, replService, snoopService);

            _view = new AiShellView { DataContext = vm };
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
