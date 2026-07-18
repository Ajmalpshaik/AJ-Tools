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
        private readonly McpBridgeService _mcpBridge;

        /// <summary>Shared with the standalone "AJ AI Bridge" ribbon button (ToggleAiBridgeCommand,
        /// via the static App.AiBridge reference) so both reach the same running bridge/pipe instead
        /// of a second one being created.</summary>
        public McpBridgeService Bridge => _mcpBridge;

        public AiShellPaneProvider()
        {
            // Simple DI setup for the Pane
            var config = AiShellConfig.Load();
            var geminiService = new GeminiApiService(config);
            var openAiService = new OpenAiApiService(config);
            var roslynService = new RoslynService();
            var revitExecService = new RevitExecutionService(roslynService);
            var contextService = new RevitContextExtractionService();
            _mcpBridge = new McpBridgeService(revitExecService);

            var vm = new AiShellViewModel(config, geminiService, openAiService, revitExecService, contextService);

            _view = new AiShellView { DataContext = vm };
        }

        /// <summary>Stops the AJ AI bridge and clears its discovery file on Revit shutdown.</summary>
        public void Shutdown()
        {
            _mcpBridge?.Stop();
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
