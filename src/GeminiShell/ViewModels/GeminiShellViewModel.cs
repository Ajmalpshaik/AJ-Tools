using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using AJTools.GeminiShell.Models;
using AJTools.GeminiShell.Services;
using AJTools.GeminiShell.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AJTools.GeminiShell.ViewModels
{
    public class GeminiShellViewModel : ObservableObject
    {
        private readonly GeminiShellConfig _config;
        private readonly IAiProviderService _geminiService;
        private readonly IAiProviderService _openAiService;
        private readonly RevitExecutionService _executionService;
        private readonly RevitContextExtractionService _contextService;
        private CancellationTokenSource _cts;
        private const int MaxHistorySize = 50;

        public GeminiShellViewModel(
            GeminiShellConfig config,
            IAiProviderService geminiService,
            IAiProviderService openAiService,
            RevitExecutionService executionService,
            RevitContextExtractionService contextService)
        {
            _config = config;
            _geminiService = geminiService;
            _openAiService = openAiService;
            _executionService = executionService;
            _contextService = contextService;

            History = new ObservableCollection<GeminiMessage>();

            // Load settings
            SelectedProvider = _config.SelectedProvider;
            GeminiApiKeyInput = _config.GetGeminiApiKey();
            OpenAiApiKeyInput = _config.GetOpenAiApiKey();
            OpenAiModel = _config.OpenAiModel;
            SavedHistory = new ObservableCollection<HistoryItem>();
            ScriptsFolderPath = _config.ScriptsFolderPath;
            RefreshScriptsList();

            ToggleSettingsCommand = new RelayCommand(ToggleSettings);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            GenerateCodeCommand = new AsyncRelayCommand(GenerateCodeAsync);
            RunCodeCommand = new AsyncRelayCommand(RunCodeAsync);
            ReviewCodeCommand = new AsyncRelayCommand(ReviewCodeAsync);
            FormatCodeCommand = new RelayCommand(FormatCode);
            SaveScriptCommand = new RelayCommand(SaveScript);
            StopCommand = new RelayCommand(StopProcess);
            RunFromHistoryCommand = new RelayCommand<HistoryItem>(RunFromHistory);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
        }

        private IAiProviderService GetActiveService()
        {
            return SelectedProvider == "OpenAI" ? _openAiService : _geminiService;
        }

        private string _promptInput;
        public string PromptInput
        {
            get => _promptInput;
            set => SetProperty(ref _promptInput, value);
        }

        private string _codeEditorContent;
        public string CodeEditorContent
        {
            get => _codeEditorContent;
            set => SetProperty(ref _codeEditorContent, value);
        }

        private string _executionResults;
        public string ExecutionResults
        {
            get => _executionResults;
            set => SetProperty(ref _executionResults, value);
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private int _executionProgress = 0;
        public int ExecutionProgress
        {
            get => _executionProgress;
            set => SetProperty(ref _executionProgress, value);
        }

        private string _executionProgressText = string.Empty;
        public string ExecutionProgressText
        {
            get => _executionProgressText;
            set => SetProperty(ref _executionProgressText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // --- Settings Properties ---
        private bool _isSettingsVisible;
        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set => SetProperty(ref _isSettingsVisible, value);
        }

        private string _selectedProvider;
        public string SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                SetProperty(ref _selectedProvider, value);
                _config.SelectedProvider = value;
                _config.Save();
                OnPropertyChanged(nameof(IsGeminiSelected));
                OnPropertyChanged(nameof(IsOpenAiSelected));
            }
        }

        public bool IsGeminiSelected => SelectedProvider == "Gemini";
        public bool IsOpenAiSelected => SelectedProvider == "OpenAI";

        private string _geminiApiKeyInput;
        public string GeminiApiKeyInput
        {
            get => _geminiApiKeyInput;
            set => SetProperty(ref _geminiApiKeyInput, value);
        }

        private string _openAiApiKeyInput;
        public string OpenAiApiKeyInput
        {
            get => _openAiApiKeyInput;
            set => SetProperty(ref _openAiApiKeyInput, value);
        }

        private string _openAiModel;
        public string OpenAiModel
        {
            get => _openAiModel;
            set => SetProperty(ref _openAiModel, value);
        }

        private string _scriptsFolderPath;
        public string ScriptsFolderPath
        {
            get => _scriptsFolderPath;
            set
            {
                if (SetProperty(ref _scriptsFolderPath, value))
                {
                    _config.ScriptsFolderPath = value;
                    _config.Save();
                    RefreshScriptsList();
                }
            }
        }

        public ObservableCollection<GeminiMessage> History { get; }
        public ObservableCollection<HistoryItem> SavedHistory { get; }

        public ICommand ToggleSettingsCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand GenerateCodeCommand { get; }
        public ICommand RunCodeCommand { get; }
        public ICommand ReviewCodeCommand { get; }
        public ICommand FormatCodeCommand { get; }
        public ICommand SaveScriptCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RunFromHistoryCommand { get; }
        public ICommand BrowseFolderCommand { get; }

        private void RunFromHistory(HistoryItem item)
        {
            if (item == null || !File.Exists(item.FilePath)) return;
            PromptInput = item.Prompt;
            CodeEditorContent = item.Code;
            
            if (RunCodeCommand.CanExecute(null))
            {
                RunCodeCommand.Execute(null);
            }
        }

        private void BrowseFolder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = ScriptsFolderPath;
                dialog.Description = "Select a folder to save and load your scripts.";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ScriptsFolderPath = dialog.SelectedPath;
                }
            }
        }

        private void RefreshScriptsList()
        {
            if (SavedHistory == null) return;
            SavedHistory.Clear();
            if (string.IsNullOrWhiteSpace(ScriptsFolderPath) || !Directory.Exists(ScriptsFolderPath)) return;

            var files = Directory.GetFiles(ScriptsFolderPath, "*.cs");
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var prompt = "";
                if (content.StartsWith("// Prompt: "))
                {
                    var firstLineEnd = content.IndexOf('\n');
                    if (firstLineEnd > 0)
                    {
                        prompt = content.Substring(11, firstLineEnd - 11).Trim();
                        content = content.Substring(firstLineEnd + 1).TrimStart();
                    }
                }

                SavedHistory.Add(new HistoryItem
                {
                    FilePath = file,
                    Prompt = prompt,
                    Code = content,
                    DateCreated = File.GetCreationTime(file)
                });
            }

            // Sort by DateCreated descending
            var sorted = new System.Collections.Generic.List<HistoryItem>(SavedHistory);
            sorted.Sort((a, b) => b.DateCreated.CompareTo(a.DateCreated));
            SavedHistory.Clear();
            foreach (var item in sorted) SavedHistory.Add(item);
        }

        private void SaveScript()
        {
            if (string.IsNullOrWhiteSpace(CodeEditorContent)) return;

            if (string.IsNullOrWhiteSpace(ScriptsFolderPath))
            {
                StatusText = "Please select a Scripts Folder in Settings first.";
                return;
            }

            if (!Directory.Exists(ScriptsFolderPath))
            {
                Directory.CreateDirectory(ScriptsFolderPath);
            }

            IsBusy = true;
            StatusText = "Saving script...";

            string fileName = "GeneratedScript.cs";
            
            // Extract the name from the first line of the code to save API tokens
            if (!string.IsNullOrWhiteSpace(CodeEditorContent))
            {
                var firstLine = CodeEditorContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstLine != null && firstLine.StartsWith("// Name:"))
                {
                    string cleanName = firstLine.Substring(8).Trim().Replace(" ", "").Replace(".cs", "").Replace("`", "").Replace("\"", "").Replace("'", "");
                    foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                    {
                        cleanName = cleanName.Replace(c.ToString(), "");
                    }
                    if (!string.IsNullOrWhiteSpace(cleanName))
                    {
                        fileName = cleanName + ".cs";
                    }
                }
            }

            string filePath = Path.Combine(ScriptsFolderPath, fileName);
            int counter = 1;
            while (File.Exists(filePath))
            {
                filePath = Path.Combine(ScriptsFolderPath, $"{Path.GetFileNameWithoutExtension(fileName)}_{counter}.cs");
                counter++;
            }

            string fileContent = $"// Prompt: {PromptInput?.Replace("\n", " ")}\n\n{CodeEditorContent}";
            File.WriteAllText(filePath, fileContent);

            StatusText = $"Script saved to {Path.GetFileName(filePath)}";
            IsBusy = false;
            
            RefreshScriptsList();
        }

        private void StopProcess()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                StatusText = "Stopping process...";
            }
        }

        private void TrimHistory()
        {
            while (History.Count > MaxHistorySize)
            {
                History.RemoveAt(0);
            }
        }

        private void ToggleSettings()
        {
            IsSettingsVisible = !IsSettingsVisible;
        }

        private void SaveSettings()
        {
            _config.SetGeminiApiKey(GeminiApiKeyInput);
            _config.SetOpenAiApiKey(OpenAiApiKeyInput);
            if (!string.IsNullOrWhiteSpace(OpenAiModel))
            {
                _config.OpenAiModel = OpenAiModel.Trim();
            }
            _config.Save();
            IsSettingsVisible = false;
            StatusText = "Settings saved securely.";
        }

        private async Task GenerateCodeAsync()
        {
            var activeService = GetActiveService();
            if (!activeService.IsConfigured())
            {
                StatusText = $"Please configure API Key for {activeService.ProviderName} in Settings.";
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptInput)) return;

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                IsBusy = true;
                StatusText = "Extracting Revit context...";

                string contextString = string.Empty;
                if (_contextService != null)
                {
                    contextString = await _contextService.ExtractContextAsync();
                }

                StatusText = $"Generating code using {activeService.ProviderName}...";
                
                var messages = new System.Collections.Generic.List<GeminiMessage>();
                if (!string.IsNullOrWhiteSpace(contextString) && contextString != "No active document." && contextString != "No elements currently selected.")
                {
                    messages.Add(new GeminiMessage { Role = "user", Content = $"[SYSTEM CONTEXT INJECTION]\n{contextString}\nPlease consider this current context if my request implies operating on selected elements." });
                }
                messages.Add(new GeminiMessage { Role = "user", Content = PromptInput });

                            string systemPrompt = @"You are an expert Revit API C# Developer.
Think like a BIM Modeler and a Coding Agent. Write clean, concise, and highly efficient code to save API tokens.

Provide ONLY valid C# code that can be executed via Roslyn.
Assume the following globals are available:
- Document Document
- UIDocument UIDocument
- Application Application
- Action<int, string> ReportProgress

CRITICAL FORMATTING AND STRUCTURE RULES:
1. The very first line of your response MUST be a comment with a name, formatted exactly like this: // Name: YourPascalCaseName
2. You MUST structure your code by placing the main logic inside a `public string Execute()` method.
3. At the very end of the script, you MUST call this method and return its value by adding: `return Execute();`
4. Inside the Execute() method, all modifications must be wrapped in a Transaction.
5. Your Execute() method MUST return a summary string explaining exactly how many elements were modified (e.g., `return $""{count} elements were updated successfully."";`). Do NOT return void.
6. CRITICAL API VERSION: You MUST write code strictly for the Revit 2020 API. Do NOT use newer APIs (like ForgeTypeId, Parameter.GetElementId(), etc.) introduced in Revit 2021+.";

                string response = await activeService.SendMessageAsync(messages, systemPrompt, _cts.Token);
                CodeEditorContent = Helpers.CodeExtractionHelper.ExtractCSharpCode(response);
                StatusText = "Code generated successfully.";
                
                History.Add(new GeminiMessage { Role = "user", Content = PromptInput });
                History.Add(new GeminiMessage { Role = "model", Content = response });
                TrimHistory();

                PromptInput = string.Empty;
            }
            catch (Exception ex)
            {
                StatusText = $"Error generating code: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RunCodeAsync()
        {
            if (string.IsNullOrWhiteSpace(CodeEditorContent)) return;

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                IsBusy = true;
                StatusText = "Executing code in Revit...";
                ExecutionResults = "Running...";
                ExecutionProgress = 0;
                ExecutionProgressText = "Initializing...";

                int attempt = 0;
                int maxAttempts = 5;
                bool success = false;

                Action<int, string> progressCallback = (percent, message) =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ExecutionProgress = percent;
                        ExecutionProgressText = message;
                    });
                };

                while (attempt < maxAttempts && !success && !_cts.IsCancellationRequested)
                {
                    attempt++;
                    var result = await _executionService.ExecuteAsync(CodeEditorContent, progressCallback);

                    if (result.Success)
                    {
                        ExecutionResults += $"\n\n[Attempt {attempt}] Execution successful.\n{result.Output}";
                        StatusText = "Execution successful.";
                        ExecutionProgress = 100;
                        ExecutionProgressText = "Done";
                        success = true;
                    }
                    else
                    {
                        ExecutionResults += $"\n\n[Attempt {attempt}] Execution Failed:\n{result.ErrorMessage}";
                        StatusText = $"Execution failed (Attempt {attempt}/{maxAttempts}). Requesting fix...";

                        var activeService = GetActiveService();
                        if (activeService.IsConfigured() && attempt < maxAttempts && !_cts.IsCancellationRequested)
                        {
                            string systemPrompt = @"You are an expert Revit API C# Developer.
Think like a BIM Modeler and a Coding Agent. Write clean, concise, and highly efficient code to save API tokens.

Provide ONLY valid C# code that can be executed via Roslyn.
Assume the following globals are available:
- Document Document
- UIDocument UIDocument
- Application Application
- Action<int, string> ReportProgress

CRITICAL FORMATTING AND STRUCTURE RULES:
1. The very first line of your response MUST be a comment with a name, formatted exactly like this: // Name: YourPascalCaseName
2. You MUST structure your code by placing the main logic inside a `public string Execute()` method.
3. At the very end of the script, you MUST call this method and return its value by adding: `return Execute();`
4. Inside the Execute() method, all modifications must be wrapped in a Transaction.
5. Your Execute() method MUST return a summary string explaining exactly how many elements were modified (e.g., `return $""{count} elements were updated successfully."";`). Do NOT return void.
6. CRITICAL API VERSION: You MUST write code strictly for the Revit 2020 API. Do NOT use newer APIs (like ForgeTypeId, Parameter.GetElementId(), etc.) introduced in Revit 2021+.";
                            
                            var errorService = new ErrorCorrectionService(activeService);
                            var fixedCode = await errorService.RequestFixAsync(CodeEditorContent, result.ErrorMessage, new System.Collections.Generic.List<GeminiMessage>(History), systemPrompt, _cts.Token);
                            
                            if (!string.IsNullOrWhiteSpace(fixedCode))
                            {
                                CodeEditorContent = fixedCode;
                                ExecutionResults += "\nReceived fixed code from AI. Re-running automatically...";
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                if (_cts.IsCancellationRequested)
                {
                    ExecutionResults += "\n\nProcess was stopped by the user.";
                    StatusText = "Stopped.";
                }
                else if (!success)
                {
                    StatusText = "Max auto-fix attempts reached.";
                }
            }
            catch (OperationCanceledException)
            {
                ExecutionResults += "\n\nProcess was stopped by the user.";
                StatusText = "Stopped.";
            }
            catch (Exception ex)
            {
                ExecutionResults += $"\n\nCritical Error:\n{ex.Message}";
                StatusText = "Critical execution error.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReviewCodeAsync()
        {
            var activeService = GetActiveService();
            if (!activeService.IsConfigured()) return;

            if (string.IsNullOrWhiteSpace(CodeEditorContent)) return;

            History.Add(new GeminiMessage { Role = "user", Content = $"Please review the following Revit API C# code for performance, safety, and best practices:\n\n{CodeEditorContent}" });
            TrimHistory();

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                IsBusy = true;
                StatusText = "Reviewing code...";
                
                var response = await activeService.SendMessageAsync(new System.Collections.Generic.List<GeminiMessage>(History), null, _cts.Token);
                History.Add(new GeminiMessage { Role = "model", Content = response });
                TrimHistory();

                ExecutionResults = response;
                StatusText = "Review complete.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error reviewing code: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void FormatCode()
        {
            if (string.IsNullOrWhiteSpace(CodeEditorContent)) return;
            try
            {
                var options = new CSharpParseOptions(kind: SourceCodeKind.Script);
                var syntaxTree = CSharpSyntaxTree.ParseText(CodeEditorContent, options);
                var formattedNode = syntaxTree.GetRoot().NormalizeWhitespace();
                CodeEditorContent = formattedNode.ToFullString();
                StatusText = "Code formatted.";
            }
            catch (Exception ex)
            {
                StatusText = "Format failed: " + ex.Message;
            }
        }
    }
}
