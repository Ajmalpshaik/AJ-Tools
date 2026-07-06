#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : GeminiShellViewModel.cs
 * Purpose       : WPF ViewModel driving the AJ AI dockable pane — takes a plain-English prompt,
 *                 sends it (with live Revit context) to Gemini or OpenAI, extracts the returned
 *                 C# script, runs it safely against the open Revit document, and auto-retries a
 *                 failed run with the AI's help up to a fixed attempt limit.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-01-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF, no direct Revit API calls — all Revit access goes through
 *                 RevitContextExtractionService / RevitExecutionService via ExternalEvent)
 *
 * Dependencies  : IAiProviderService (Gemini/OpenAI), RevitExecutionService, RevitContextExtractionService,
 *                 GeneratedCodeSafetyValidator, ErrorCorrectionService
 *
 * Input         : User prompt text, live Revit selection context, saved script history
 * Output        : Generated/edited C# script, execution results, saved .cs script files
 *
 * Notes         :
 * - Single system prompt constant (previously duplicated in two methods) constrains the AI to
 *   Revit 2020-safe C#, no external I/O, and a required Execute()-returns-summary structure.
 * - Every run is scanned by GeneratedCodeSafetyValidator before execution: outright-dangerous
 *   patterns (Process.Start, registry, network, reflection/unsafe, raw file I/O) are blocked;
 *   destructive-but-legitimate ones (Delete/Purge) require a Yes/No confirmation once per run.
 * - The auto-fix retry loop stops early if the AI returns the same error twice in a row, instead
 *   of burning all remaining attempts on a fix that isn't working.
 * - IsBusy is checked at the top of every command method as a defensive re-entrancy guard, in
 *   addition to the XAML now disabling the action buttons while a request is in flight.
 * - Generate Code is now stateful: if the editor already has code, the next prompt is sent as an
 *   incremental change request ([EXISTING CODE TO UPDATE] + the new instruction) so the AI edits
 *   only the requested part instead of rewriting the whole script. Clearing the editor first is
 *   how the user starts a fresh, unrelated request.
 *
 * Changelog     :
 * v1.0.0 (2026-01-01) - Initial release.
 * v1.1.0 (2026-07-01) - Safety-hardening pass: deduped system prompt, added pre-execution safety
 *                       scan + confirmation, repeat-error detection, IsBusy guards, provider/key
 *                       status text, saved-script provider metadata, activity logging.
 * v1.2.0 (2026-07-01) - Generate Code now sends the existing script as context for incremental
 *                       edits when the editor is non-empty, instead of always starting fresh.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
        // The one and only system prompt sent to the AI for both code generation and auto-fix
        // requests. Previously this exact text was duplicated in GenerateCodeAsync and
        // RunCodeAsync — kept in sync manually. Now there is exactly one copy.
        private const string SystemPrompt = @"You are generating C# code for a live Revit automation assistant, acting as an expert Revit API developer. Think like a BIM Modeler and a Coding Agent — write clean, concise, and efficient code.

HARD RULES:
1. Generate raw C# script only. Do not create a full add-in project. Do not generate Python, Dynamo, PowerShell, or external application code. Do not use external NuGet packages.
2. Target the Revit 2020 API strictly. Do NOT use ForgeTypeId, UnitTypeId, SpecTypeId, Parameter.GetElementId(), or any other Revit 2021+ API. Use only APIs compatible with Revit 2020 and .NET Framework 4.7.2.
3. Use only the provided globals: Document Document, UIDocument UIDocument, Application Application, Action<int,string> ReportProgress. Do not access the network, the registry, or external processes, and do not launch other programs.
4. Do not delete, purge, or bulk-modify elements unless the user's request clearly asked for that.
5. Validate before you use: check elements are not null and IsValidObject, check a Category exists before assuming it, check a Parameter is not null and not read-only before setting it. Never guess a parameter name.
6. Use a Transaction only when the model is actually being modified. Read-only code (counting, checking, reporting) must NOT open a Transaction.
7. Do not use TaskDialog for routine success messages — return a plain summary string instead so it shows in the tool's own output panel.
8. Keep code short, safe, and focused on exactly what was asked.
9. If the user's message contains a block labeled [EXISTING CODE TO UPDATE], that is the script currently in the editor. Treat the instruction that follows it as a small addition or change to that exact script — NOT a new request. Change only the part needed for the new instruction and keep every other part of that script exactly as given, including its structure. Return the complete script with the change applied, not just the changed lines.

REQUIRED STRUCTURE (read carefully — this prevents a common mistake):
1. The very first line of your response MUST be a comment with a name: // Name: YourPascalCaseName
2. Do NOT declare your own class (do NOT write `public class SomeName { ... }`). A class you declare yourself CANNOT see Document, UIDocument, Application, or ReportProgress — those are only visible in code written directly in the script, including a plain method written directly in the script.
3. Structure the script exactly like this:
   string Execute()
   {
       // your logic here, using Document, UIDocument, Application directly by name
       return ""summary of what happened"";
   }
   return Execute();
4. Execute() MUST return a clear summary string describing what happened (e.g. how many elements were checked or changed). It must NOT return void, and it must NOT be declared inside a class.

If the user's request is unsafe, destructive without being explicitly asked for, or unclear, generate safe validation/reporting code instead of risky modification code, and explain in the summary what additional confirmation would be needed.";

        private readonly GeminiShellConfig _config;
        private readonly IAiProviderService _geminiService;
        private readonly IAiProviderService _openAiService;
        private readonly RevitExecutionService _executionService;
        private readonly RevitContextExtractionService _contextService;
        private CancellationTokenSource _cts;

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
                OnPropertyChanged(nameof(ProviderStatusText));
            }
        }

        public bool IsGeminiSelected => SelectedProvider == "Gemini";
        public bool IsOpenAiSelected => SelectedProvider == "OpenAI";

        /// <summary>Plain-language provider + key status for the top bar — never shows the key itself.</summary>
        public string ProviderStatusText =>
            $"{SelectedProvider}: {(GetActiveService()?.IsConfigured() == true ? "API key configured" : "API key missing — open Settings")}";

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
            if (IsBusy || item == null || !File.Exists(item.FilePath)) return;
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
                var rawContent = File.ReadAllText(file);
                var (prompt, provider, code) = ParseSavedScriptHeader(rawContent);

                SavedHistory.Add(new HistoryItem
                {
                    FilePath = file,
                    Prompt = prompt,
                    Provider = provider,
                    Code = code,
                    DateCreated = File.GetCreationTime(file)
                });
            }

            // Sort by DateCreated descending
            var sorted = new System.Collections.Generic.List<HistoryItem>(SavedHistory);
            sorted.Sort((a, b) => b.DateCreated.CompareTo(a.DateCreated));
            SavedHistory.Clear();
            foreach (var item in sorted) SavedHistory.Add(item);
        }

        /// <summary>
        /// Saved scripts start with one or two header comment lines (// Prompt: ... and
        /// // Provider: ...) followed by a blank line and the code. This peels those header
        /// lines off in a single, testable place instead of ad-hoc string math at each call site.
        /// </summary>
        private static (string Prompt, string Provider, string Code) ParseSavedScriptHeader(string rawContent)
        {
            string prompt = string.Empty;
            string provider = string.Empty;
            string remaining = rawContent ?? string.Empty;

            remaining = ConsumeHeaderLine(remaining, "// Prompt: ", out prompt);
            remaining = ConsumeHeaderLine(remaining, "// Provider: ", out provider);

            return (prompt, provider, remaining.TrimStart('\r', '\n'));
        }

        private static string ConsumeHeaderLine(string content, string prefix, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(content) || !content.StartsWith(prefix))
            {
                return content;
            }

            int lineEnd = content.IndexOf('\n');
            if (lineEnd < 0)
            {
                value = content.Substring(prefix.Length).Trim();
                return string.Empty;
            }

            value = content.Substring(prefix.Length, lineEnd - prefix.Length).Trim();
            return content.Substring(lineEnd + 1);
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

            string promptHeader = $"// Prompt: {PromptInput?.Replace("\n", " ")}";
            string providerHeader = $"// Provider: {SelectedProvider} (Revit 2020 target)";
            string fileContent = $"{promptHeader}\n{providerHeader}\n\n{CodeEditorContent}";
            File.WriteAllText(filePath, fileContent);

            StatusText = $"Script saved to {filePath}";
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
            while (History.Count > Helpers.AiShellConstants.MaxChatHistoryMessages)
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
            OnPropertyChanged(nameof(ProviderStatusText));
        }

        private async Task GenerateCodeAsync()
        {
            if (IsBusy) return;

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

                // If the editor already has code, this request is an incremental change to it
                // (e.g. "filter for supply duct", "make it red") rather than a brand new script.
                // Clearing the editor is how the user signals "start a new, unrelated request".
                bool isIncrementalUpdate = !string.IsNullOrWhiteSpace(CodeEditorContent);
                string existingCode = CodeEditorContent;

                StatusText = isIncrementalUpdate
                    ? $"Updating existing code using {activeService.ProviderName}..."
                    : $"Generating code using {activeService.ProviderName}...";

                var messages = new System.Collections.Generic.List<GeminiMessage>();
                if (!string.IsNullOrWhiteSpace(contextString) && contextString != "No active document." && contextString != "No elements currently selected.")
                {
                    messages.Add(new GeminiMessage { Role = "user", Content = $"[SYSTEM CONTEXT INJECTION]\n{contextString}\nPlease consider this current context if my request implies operating on selected elements." });
                }

                if (isIncrementalUpdate)
                {
                    messages.Add(new GeminiMessage
                    {
                        Role = "user",
                        Content = $"[EXISTING CODE TO UPDATE]\n{existingCode}\n\n[NEW INSTRUCTION]\n{PromptInput}"
                    });
                }
                else
                {
                    messages.Add(new GeminiMessage { Role = "user", Content = PromptInput });
                }

                string response = await activeService.SendMessageAsync(messages, SystemPrompt, _cts.Token);
                CodeEditorContent = Helpers.CodeExtractionHelper.ExtractCSharpCode(response);

                var safety = GeneratedCodeSafetyValidator.Validate(CodeEditorContent);
                string resultStatus = isIncrementalUpdate ? "Code updated successfully." : "Code generated successfully.";
                StatusText = safety.HighestLevel == CodeRiskLevel.Safe
                    ? resultStatus
                    : $"{(isIncrementalUpdate ? "Code updated." : "Code generated.")} Note: {safety.Findings.First().Reason} Review before running.";

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
            if (IsBusy) return;
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
                int maxAttempts = Helpers.AiShellConstants.MaxAutoFixAttempts;
                bool success = false;
                bool blocked = false;
                bool userCancelledConfirmation = false;
                bool confirmedThisRun = false;
                string previousErrorMessage = null;

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

                    var safety = GeneratedCodeSafetyValidator.Validate(CodeEditorContent);
                    if (safety.IsBlocked)
                    {
                        blocked = true;
                        string reasons = string.Join("\n - ", safety.Findings.Where(f => f.Level == CodeRiskLevel.Blocked).Select(f => f.Reason));
                        ExecutionResults += $"\n\n[Attempt {attempt}] BLOCKED before running — this script does something AJ AI does not allow:\n - {reasons}\n\nEdit the code to remove this, or ask AJ AI to regenerate a safer version.";
                        StatusText = "Blocked: unsafe operation detected. Not executed.";
                        break;
                    }

                    if (safety.RequiresConfirmation && !confirmedThisRun)
                    {
                        string reasons = string.Join("\n - ", safety.Findings.Select(f => f.Reason));
                        var confirm = System.Windows.MessageBox.Show(
                            $"This script does the following:\n - {reasons}\n\nThis can only be undone with Ctrl+Z in Revit. Continue?",
                            "AJ AI: Confirm Risky Operation",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (confirm != System.Windows.MessageBoxResult.Yes)
                        {
                            userCancelledConfirmation = true;
                            ExecutionResults += "\n\nRun cancelled — destructive operation not confirmed.";
                            StatusText = "Cancelled: destructive operation not confirmed.";
                            break;
                        }

                        // Confirmed once for this run - matches the tool's "once per run" design even if a
                        // later auto-fix attempt introduces a different destructive operation.
                        confirmedThisRun = true;
                    }

                    var result = await _executionService.ExecuteAsync(CodeEditorContent, progressCallback, _cts.Token);

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
                        string currentError = (result.ErrorMessage ?? string.Empty).Trim();
                        ExecutionResults += $"\n\n[Attempt {attempt}] Execution Failed:\n{result.ErrorMessage}";

                        if (previousErrorMessage != null && string.Equals(previousErrorMessage, currentError, StringComparison.OrdinalIgnoreCase))
                        {
                            ExecutionResults += "\n\nSame error as the previous attempt — stopping auto-fix early since it isn't working.";
                            StatusText = "Stopped: the same error repeated.";
                            break;
                        }
                        previousErrorMessage = currentError;

                        StatusText = $"Execution failed (Attempt {attempt}/{maxAttempts}). Requesting fix...";

                        var activeService = GetActiveService();
                        if (activeService.IsConfigured() && attempt < maxAttempts && !_cts.IsCancellationRequested)
                        {
                            var errorService = new ErrorCorrectionService(activeService);
                            var fixedCode = await errorService.RequestFixAsync(CodeEditorContent, result.ErrorMessage, new System.Collections.Generic.List<GeminiMessage>(History), SystemPrompt, _cts.Token);

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
                else if (!success && !blocked && !userCancelledConfirmation)
                {
                    StatusText = "Max auto-fix attempts reached.";
                    ExecutionResults += "\n\nCould not get this script working after several attempts. Try rephrasing your request, or simplify what you're asking for.";
                }

                Helpers.AiShellActivityLogger.LogRun(
                    GetActiveService().ProviderName,
                    PromptInput,
                    success,
                    attempt,
                    scriptName: null,
                    errorSummary: success ? null : previousErrorMessage);
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
            if (IsBusy) return;

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
