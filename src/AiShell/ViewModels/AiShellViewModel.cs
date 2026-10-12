#region Metadata
/*
 * Tool Name     : C#
 * File Name     : AiShellViewModel.cs
 * Purpose       : WPF ViewModel driving the "C#" dockable pane — takes a plain-English prompt,
 *                 sends it (with live Revit context) to Gemini, OpenAI, Claude, or NVIDIA, extracts
 *                 the returned C# script, runs it safely against the open Revit document, and
 *                 auto-retries a failed run with the AI's help up to a fixed attempt limit.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.13.1
 *
 * Created Date  : 2026-01-01
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF, no direct Revit API calls — all Revit access goes through
 *                 RevitContextExtractionService / RevitExecutionService via ExternalEvent)
 *
 * Dependencies  : IAiProviderService (Gemini/OpenAI/Claude/NVIDIA), RevitExecutionService, RevitContextExtractionService,
 *                 GeneratedCodeSafetyValidator, ErrorCorrectionService
 *
 * Input         : User prompt text, live Revit selection context
 * Output        : Generated/edited C# script, execution results, saved .cs script files
 *
 * Notes         :
 * - Single system prompt (previously duplicated in two methods) constrains the AI to C# for the
 *   Revit version actually running (its API rule - units in particular - is built from that
 *   version at construction), no external I/O, and a required Execute()-returns-summary structure.
 * - Every run is scanned by GeneratedCodeSafetyValidator before execution: outright-dangerous
 *   patterns (Process.Start, registry, network, reflection/unsafe, raw file I/O) are blocked;
 *   destructive-but-legitimate ones (Delete/Purge/file writes) require a Yes/No confirmation -
 *   asked again whenever an auto-fix attempt adds one that was not confirmed yet.
 * - The auto-fix retry loop stops early if the AI returns the same error twice in a row, instead
 *   of burning all remaining attempts on a fix that isn't working.
 * - IsBusy is checked at the top of every command method as a defensive re-entrancy guard, in
 *   addition to the XAML now disabling the action buttons while a request is in flight.
 *
 * Changelog     :
 * v1.13.1 (2026-10-12) - Audit fixes. (1) A reply with no code no longer wipes the editor: the
 *                       providers now raise AiEmptyReplyException (with their stated reason) and a
 *                       blank extracted script is refused - the existing code, its undo history and
 *                       the recovery file stay as they were, and the status says "The AI returned
 *                       no code". (2) The Delete/Purge/file-write confirmation is asked again when
 *                       an auto-fix attempt adds a finding not confirmed yet (it was attempt 1
 *                       only). (3) Save Script reports a failed save in the status line and always
 *                       clears IsBusy (an exception used to leave every pane button disabled).
 *                       (4) Settings come from the session-wide AiShellConfig.Shared and the
 *                       scripts folder is read through from it, so a pin/folder set in Saved
 *                       Scripts is no longer written back to its old value. (5) The constructor no
 *                       longer saves the settings file on every Revit start, and an unreadable
 *                       settings file is reported once in the status line. (6) The system prompt
 *                       and the auto-fix prompt name the running Revit version: 2020-2021 legacy
 *                       units, 2022+ ForgeTypeId units (Revit 2020's prompt is word for word as
 *                       before). (7) No API key: a failed Run stops with "No API key - auto-fix
 *                       skipped" instead of re-running the same script; Review Code says why it did
 *                       nothing. (8) RevertUnsavedSettings for the Settings window's Close.
 *                       (9) Removed an always-true context check and stale comments; reworded the
 *                       changelog below without naming outside tools (project rule).
 * v1.13.0 (2026-08-05) - Added NVIDIA as a FOURTH provider (NvidiaApiService), giving the pane the
 *                       ~130-model NIM catalog on build.nvidia.com's free tier - Ajmal asked for it
 *                       to cut API cost and to try specific open models. Same additive shape as the
 *                       v1.11.0 Claude addition: IsNvidiaSelected / NvidiaApiKeyInput / NvidiaModel,
 *                       one line in GetActiveService(), nothing else touched. SelectedProvider still
 *                       defaults to "Gemini", so nothing changes until he picks NVIDIA in Settings.
 *                       Default model z-ai/glm-5.2. ONE UI DIFFERENCE from the other three, and it is
 *                       deliberate: NvidiaModel binds to an EDITABLE dropdown, because a fixed list
 *                       cannot represent a 130-model catalog - the list is a verified shortlist and
 *                       any other id can be pasted straight off a model card without a rebuild.
 *                       ErrorCorrectionService needed no change: it takes whichever IAiProviderService
 *                       it is constructed with, so it follows GetActiveService() automatically.
 * v1.12.1 (2026-07-25) - Merge of the local and GitHub lines of this file (both had independently
 *                       added the same third-provider feature). Provider key string unified to
 *                       "Claude" (GitHub naming; shown in the Settings dropdown), default model
 *                       claude-sonnet-5. Local v1.12.0 Saved Scripts split kept as-is.
 * v1.12.0 (2026-07-21) - Removed the "Saved Scripts History" expander (SavedHistory, PinScriptCommand,
 *                       RunFromHistoryCommand, PinnedScriptDisplayText, RefreshScriptsList and its
 *                       header-parsing helpers) - moved to its own standalone "Saved Scripts" ribbon
 *                       button/window (ShowSavedScriptsCommand/SavedScriptsWindow), same reasoning
 *                       as "Run Pinned": reachable whether or not the C# pane is open. This pane no
 *                       longer tracks saved-script state at all; ScriptsFolderPath (Settings) is the
 *                       only piece of that feature still owned here.
 * v1.11.0 (2026-07-21/23) - Added Claude as a third AI provider option alongside Gemini and OpenAI
 *                       (AnthropicApiService, IsAnthropicSelected/AnthropicApiKeyInput/
 *                       AnthropicModel settings properties, same IAiProviderService pattern as the
 *                       other two) - the in-Revit chat pane can now use the same model family as
 *                       the outside coding agent that drove the "AJ AI Bridge" MCP connection
 *                       (McpBridgeService, removed in 1.57.0). Purely additive: SelectedProvider still defaults to
 *                       "Gemini", so nothing changes until Ajmal opens Settings and picks Claude.
 * v1.10.0 (2026-07-21) - Two more Live Console additions: (1) console command history now persists
 *                       to ajai-console-history.json (same best-effort pattern as the crash-
 *                       recovery snapshot) so Up/Down recall survives Revit being closed and
 *                       reopened. (2) SendReplToEditorCommand - appends the last-run console line to
 *                       CodeEditorContent (never overwrites) and, if that line failed, pre-fills
 *                       PromptInput with the exact error so clicking Generate C# Code hands the fix
 *                       request straight to the existing incremental-edit AI flow (v1.5.0) instead
 *                       of needing a second AI request path just for the console.
 * v1.9.0 (2026-07-21) - Added PinScriptCommand + PinnedScriptDisplayText: a "📌 Pin" button on each
 *                       Saved Scripts History row writes AiShellConfig.PinnedScriptPath, which the
 *                       new standalone RunPinnedScriptCommand ribbon button reads to run that one
 *                       script with a single click - one statically-compiled button, rather than a
 *                       new button type generated at runtime for every script.
 * v1.8.0 (2026-07-21) - Added the two most useful scripting-shell pieces this tool was still
 *                       missing, adapted to the AI-powered workflow already here rather than
 *                       replacing it: (1) a Live Console - type one raw C# line, press Enter,
 *                       it runs immediately with variables persisted to the next line (a true
 *                       interactive shell; backed by the new
 *                       ReplSessionService/RoslynService.ExecuteReplLineAsync, which continue a
 *                       Roslyn ScriptState across lines instead of compiling each run standalone).
 *                       (2) Snoop Selection - a one-click, no-code dump of every instance/type
 *                       parameter on the current selection (ElementSnoopService). Deliberately
 *                       skipped: full Roslyn IntelliSense (too much workspace/completion-service
 *                       plumbing to get right without a local Revit+Visual Studio test loop),
 *                       startup scripts and macro-to-ribbon-button deployment (both run arbitrary
 *                       code with less visibility than a click - a bigger safety trade-off than this
 *                       tool takes elsewhere without a stronger case for the extra risk).
 * v1.7.0 (2026-07-19) - Two more Ajmal-requested improvements: (1) CodeGenerated now also fires from
 *                       RunCodeAsync's auto-fix loop (previousCode captured right before
 *                       RequestFixAsync, event raised right after CodeEditorContent is reassigned) -
 *                       the diff-highlight added in v1.18.0 (AssemblyInfo) previously only covered
 *                       Generate, not an auto-fix rewrite, which was the exact same "can't see what
 *                       changed" gap via a different path. (2) New crash/close recovery: PromptInput
 *                       and CodeEditorContent are auto-saved (2s debounce via a DispatcherTimer, same
 *                       pattern as AiShellView's syntax-check timer) to %AppData%/AJTools/
 *                       ajai-recovery.json, and restored on next pane construction if present and
 *                       non-empty - so a Revit crash or a closed pane no longer loses in-progress work
 *                       that was never explicitly "Save Script"-ed. Best-effort throughout (a recovery
 *                       read/write failure must never break the tool, same convention as
 *                       AiShellConfig/McpBridgeService's own file I/O).
 * v1.6.0 (2026-07-18) - Two Ajmal-requested improvements on the v1.5.0 incremental-edit feature below:
 *                       (1) PromptInput no longer clears after a successful generate - Ajmal has to
 *                       retype the whole request to make a small follow-up tweak otherwise. (2) Added
 *                       a CodeGenerated(previousCode, newCode) event, fired right after
 *                       CodeEditorContent is set, so AiShellView can diff-highlight which lines
 *                       actually changed (reuses TextMarkerService, the same plumbing behind syntax-
 *                       error squiggles, with a translucent Neon Blue background marker instead).
 *                       Deliberately an event, not a generic PropertyChanged hook - Format/
 *                       RunFromHistory/the auto-fix loop also reassign CodeEditorContent and should
 *                       NOT trigger a diff highlight (noisy/meaningless for those cases), so this
 *                       needed an explicit signal only GenerateCodeAsync raises.
 * v1.5.0 (2026-07-18) - GenerateCodeAsync now sends the current CodeEditorContent (if any) as context
 *                       along with the new prompt, with explicit instructions for the AI to decide:
 *                       small related change (a different color/value/parameter/category target) ->
 *                       edit only that part of the existing script; unrelated/substantially different
 *                       request -> ignore the existing code and write fresh. Previously every
 *                       "Generate C# Code" click was fully stateless - no History, no existing code -
 *                       so "change the color to green" right after "change all ducts to red" always
 *                       generated a brand new unrelated-looking script instead of editing the one
 *                       already in the editor. Ajmal's example: "change all ducts to red" then "change
 *                       duct ACCESSORIES to green" should edit (small target change: duct -> duct
 *                       accessories), but "create a filter" after either should generate fresh
 *                       (unrelated task). Deliberately only injects the editor's current code, not the
 *                       full History, to keep the request small - the editor content already reflects
 *                       the latest generated/edited script by construction.
 * v1.4.0 (2026-07-18) - Removed IsSettingsVisible/ToggleSettingsCommand entirely - Settings is now a
 *                       separate popup Window (SettingsWindow.xaml, opened from AiShellView's code-
 *                       behind) instead of an inline collapsible panel in this pane, per Ajmal's
 *                       request. SaveSettings() no longer resets a visibility flag; the popup closes
 *                       itself on Save/Close. Every other property/command this ViewModel exposes for
 *                       Settings (SelectedProvider, GeminiApiKeyInput, OpenAiApiKeyInput, OpenAiModel,
 *                       ScriptsFolderPath, BrowseFolderCommand, SaveSettingsCommand,
 *                       IsGeminiSelected/IsOpenAiSelected) is unchanged - the new window just binds
 *                       to the same DataContext instance instead of this pane owning the UI for it.
 * v1.3.0 (2026-07-18) - Removed the AJ AI Bridge Connect/Disconnect toggle (IsMcpConnected,
 *                       McpStatusText, McpToggleButtonText, ToggleMcpBridgeCommand) and the
 *                       McpBridgeService dependency entirely - that control moved out of this panel
 *                       into its own standalone ribbon button (ToggleAiBridgeCommand, reaching the
 *                       same bridge instance via the static AJTools.App.App.AiBridge reference),
 *                       per Ajmal's request so the bridge can be controlled without opening this
 *                       chat panel. AiShellPaneProvider still owns and starts/stops the bridge; this
 *                       ViewModel simply no longer references it.
 * v1.0.0 (2026-01-01) - Initial release.
 * v1.1.0 (2026-07-01) - Safety-hardening pass: deduped system prompt, added pre-execution safety
 *                       scan + confirmation, repeat-error detection, IsBusy guards, provider/key
 *                       status text, saved-script provider metadata, activity logging.
 * v1.2.0 (2026-07-07) - Added the AutoDebugger MCP bridge Connect/Disconnect toggle (IsMcpConnected,
 *                       McpStatusText, ToggleMcpBridgeCommand) so an external MCP server can drive
 *                       RevitExecutionService for automated tool debugging.
 * v1.2.1 (2026-07-16) - Fixed the execution progress bar never updating: the ReportProgress callback
 *                       reached the UI dispatcher via System.Windows.Application.Current, which is
 *                       null inside Revit (crashed any script calling ReportProgress). Now uses the
 *                       dispatcher captured at construction, plus a Render-priority pump so the bar
 *                       visibly repaints while the script blocks Revit's UI thread.
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
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using AJTools.AiShell.Models;
using AJTools.AiShell.Services;
using AJTools.AiShell.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;

namespace AJTools.AiShell.ViewModels
{
    public class AiShellViewModel : ObservableObject
    {
        // The one and only system prompt sent to the AI for both code generation and auto-fix
        // requests. Previously this exact text was duplicated in GenerateCodeAsync and
        // RunCodeAsync — kept in sync manually. Now there is exactly one copy; only its rule 2
        // (which Revit API) is filled in per running Revit version - see BuildSystemPrompt.
        private const string RevitVersionRuleToken = "%%REVIT_VERSION_RULE%%";

        private const string SystemPromptTemplate = @"You are generating C# code for a live Revit automation assistant, acting as an expert Revit API developer. Think like a BIM Modeler and a Coding Agent — write clean, concise, and efficient code.

HARD RULES:
1. Generate raw C# script only. Do not create a full add-in project. Do not generate Python, Dynamo, PowerShell, or external application code. Do not use external NuGet packages.
" + RevitVersionRuleToken + @"
3. Use only the provided globals: Document Document, UIDocument UIDocument, Application Application, Action<int,string> ReportProgress. Do not access the network, the registry, or external processes, and do not launch other programs.
4. Do not delete, purge, or bulk-modify elements unless the user's request clearly asked for that.
5. Validate before you use: check elements are not null and IsValidObject, check a Category exists before assuming it, check a Parameter is not null and not read-only before setting it. Never guess a parameter name.
6. Use a Transaction only when the model is actually being modified. Read-only code (counting, checking, reporting) must NOT open a Transaction.
7. Do not use TaskDialog for routine success messages — return a plain summary string instead so it shows in the tool's own output panel.
8. Keep code short, safe, and focused on exactly what was asked.

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

        /// <summary>The system prompt with rule 2 written for the Revit version this add-in is
        /// running in. A script is compiled against THAT version's API, and the unit API changed
        /// under it: Revit 2020 has only DisplayUnitType (no ForgeTypeId/UnitTypeId/SpecTypeId);
        /// Revit 2024 has no DisplayUnitType at all (both checked against the installed
        /// RevitAPI.dll); 2021 still has DisplayUnitType and 2022+ does not. The old fixed "Revit
        /// 2020 API strictly" rule therefore made every unit script fail to compile on 2022+.
        /// Revit 2020 keeps its original rule word for word.</summary>
        private static string BuildSystemPrompt(int revitVersion)
        {
            string rule;
            if (revitVersion <= 2020)
            {
                rule = "2. Target the Revit 2020 API strictly. Do NOT use ForgeTypeId, UnitTypeId, SpecTypeId, Parameter.GetElementId(), or any other Revit 2021+ API. Use only APIs compatible with Revit 2020 and .NET Framework 4.7.2.";
            }
            else
            {
                string framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                string units = revitVersion <= 2021
                    ? "For units use the DisplayUnitType API (e.g. UnitUtils.ConvertFromInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS)) - do NOT use ForgeTypeId, UnitTypeId or SpecTypeId."
                    : $"For units use ForgeTypeId through UnitTypeId / SpecTypeId (e.g. UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)) - DisplayUnitType and the DUT_ values do not exist in Revit {revitVersion}.";
                rule = $"2. Target the Revit {revitVersion} API (the Revit this script runs in). {units} Use only APIs that exist in Revit {revitVersion} and {framework}.";
            }

            return SystemPromptTemplate.Replace(RevitVersionRuleToken, rule);
        }

        // The Revit version this add-in runs in, and the system prompt built for it.
        private readonly int _revitVersion;
        private readonly string _systemPrompt;

        private readonly AiShellConfig _config;
        private readonly IAiProviderService _geminiService;
        private readonly IAiProviderService _openAiService;
        private readonly IAiProviderService _anthropicService;
        private readonly IAiProviderService _nvidiaService;
        private readonly RevitExecutionService _executionService;
        private readonly RevitContextExtractionService _contextService;
        private readonly ReplSessionService _replService;
        private readonly ElementSnoopService _snoopService;
        // Captured at construction on Revit's UI thread — System.Windows.Application.Current is
        // always null inside Revit (Revit hosts WPF without creating a WPF Application object), so
        // it can never be used to reach the UI dispatcher.
        private readonly Dispatcher _uiDispatcher;
        private CancellationTokenSource _cts;

        // Crash/close recovery: a debounced snapshot of the in-progress Prompt + CodeEditorContent,
        // so a Revit crash or a closed pane doesn't lose unsaved work that was never "Save Script"-ed.
        private static readonly string RecoveryFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJTools", "ajai-recovery.json");
        private const int AutoSaveDelayMs = 2000;
        private readonly DispatcherTimer _autoSaveTimer;

        private class RecoverySnapshot
        {
            public string prompt { get; set; }
            public string code { get; set; }
        }

        // Live Console command history, persisted the same best-effort way as the recovery snapshot
        // above, so Up/Down recall still has something to recall after Revit is closed and reopened
        // (the way a command-line shell keeps its own history file).
        private static readonly string ReplHistoryFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJTools", "ajai-console-history.json");
        private const int MaxReplHistoryEntries = 200;

        public AiShellViewModel(
            AiShellConfig config,
            IAiProviderService geminiService,
            IAiProviderService openAiService,
            IAiProviderService anthropicService,
            IAiProviderService nvidiaService,
            RevitExecutionService executionService,
            RevitContextExtractionService contextService,
            ReplSessionService replService,
            ElementSnoopService snoopService,
            int revitVersion)
        {
            _config = config;
            _geminiService = geminiService;
            _openAiService = openAiService;
            _anthropicService = anthropicService;
            _nvidiaService = nvidiaService;
            _executionService = executionService;
            _contextService = contextService;
            _replService = replService;
            _snoopService = snoopService;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _revitVersion = revitVersion;
            _systemPrompt = BuildSystemPrompt(revitVersion);

            History = new ObservableCollection<ChatMessage>();

            // Load settings. The provider goes straight into its backing field: its setter saves
            // the settings file, and nothing has changed yet - saving here rewrote the file on
            // every Revit start, over a file that may just have failed to load. The scripts
            // folder is read through from _config (see ScriptsFolderPath), so it needs no copy.
            _selectedProvider = _config.SelectedProvider;
            GeminiApiKeyInput = _config.GetGeminiApiKey();
            OpenAiApiKeyInput = _config.GetOpenAiApiKey();
            OpenAiModel = _config.OpenAiModel;
            AnthropicApiKeyInput = _config.GetAnthropicApiKey();
            AnthropicModel = _config.AnthropicModel;
            NvidiaApiKeyInput = _config.GetNvidiaApiKey();
            NvidiaModel = _config.NvidiaModel;

            SaveSettingsCommand = new RelayCommand(SaveSettings);
            GenerateCodeCommand = new AsyncRelayCommand(GenerateCodeAsync);
            RunCodeCommand = new AsyncRelayCommand(RunCodeAsync);
            ReviewCodeCommand = new AsyncRelayCommand(ReviewCodeAsync);
            FormatCodeCommand = new RelayCommand(FormatCode);
            SaveScriptCommand = new RelayCommand(SaveScript);
            StopCommand = new RelayCommand(StopProcess);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            SnoopSelectionCommand = new AsyncRelayCommand(SnoopSelectionAsync);
            ReplRunCommand = new AsyncRelayCommand(ReplRunAsync);
            ReplResetCommand = new RelayCommand(ReplReset);
            SendReplToEditorCommand = new RelayCommand(SendReplToEditor);

            _autoSaveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(AutoSaveDelayMs) };
            _autoSaveTimer.Tick += (s, e) => { _autoSaveTimer.Stop(); SaveRecoverySnapshot(); };
            LoadRecoverySnapshotIfAny();
            LoadReplHistoryIfAny();

            // An unreadable settings file means the defaults (no API keys) are in use - say so once
            // rather than letting the keys silently vanish. Set last so it is the message shown.
            if (!string.IsNullOrWhiteSpace(_config.LoadWarning))
            {
                StatusText = _config.LoadWarning;
            }
        }

        private IAiProviderService GetActiveService()
        {
            if (SelectedProvider == "OpenAI") return _openAiService;
            if (SelectedProvider == "Claude") return _anthropicService;
            if (SelectedProvider == "NVIDIA") return _nvidiaService;
            return _geminiService;
        }

        private void ScheduleRecoverySave()
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void SaveRecoverySnapshot()
        {
            try
            {
                var dir = Path.GetDirectoryName(RecoveryFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var snapshot = new RecoverySnapshot { prompt = PromptInput, code = CodeEditorContent };
                File.WriteAllText(RecoveryFilePath, JsonConvert.SerializeObject(snapshot));
            }
            catch { /* best-effort - recovery is a convenience, must never break the tool */ }
        }

        /// <summary>Restores the last unsaved Prompt/CodeEditorContent, if any, so a Revit crash or a
        /// closed pane doesn't lose work that was never explicitly "Save Script"-ed. Sets the backing
        /// fields directly (not the public setters) so this doesn't immediately re-trigger a save of
        /// the exact same data it just loaded.</summary>
        private void LoadRecoverySnapshotIfAny()
        {
            try
            {
                if (!File.Exists(RecoveryFilePath)) return;

                var snapshot = JsonConvert.DeserializeObject<RecoverySnapshot>(File.ReadAllText(RecoveryFilePath));
                if (snapshot == null) return;

                bool hasPrompt = !string.IsNullOrWhiteSpace(snapshot.prompt);
                bool hasCode = !string.IsNullOrWhiteSpace(snapshot.code);
                if (!hasPrompt && !hasCode) return;

                _promptInput = snapshot.prompt ?? string.Empty;
                _codeEditorContent = snapshot.code ?? string.Empty;
                StatusText = "Recovered unsaved work from your last session.";
            }
            catch { /* best-effort - a corrupt/unreadable recovery file must never break startup */ }
        }

        private void SaveReplHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(ReplHistoryFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(ReplHistoryFilePath, JsonConvert.SerializeObject(_replHistory));
            }
            catch { /* best-effort - console history recall is a convenience, must never break the tool */ }
        }

        /// <summary>Restores the Live Console's Up/Down recall history from the previous session, if
        /// any - so it survives Revit being closed and reopened, the same way a terminal's own
        /// history file does.</summary>
        private void LoadReplHistoryIfAny()
        {
            try
            {
                if (!File.Exists(ReplHistoryFilePath)) return;

                var saved = JsonConvert.DeserializeObject<System.Collections.Generic.List<string>>(File.ReadAllText(ReplHistoryFilePath));
                if (saved == null || saved.Count == 0) return;

                _replHistory.Clear();
                _replHistory.AddRange(saved);
                _replHistoryIndex = _replHistory.Count;
            }
            catch { /* best-effort - a corrupt/unreadable history file must never break startup */ }
        }

        private string _promptInput;
        public string PromptInput
        {
            get => _promptInput;
            set
            {
                if (SetProperty(ref _promptInput, value)) ScheduleRecoverySave();
            }
        }

        private string _codeEditorContent;
        public string CodeEditorContent
        {
            get => _codeEditorContent;
            set
            {
                if (SetProperty(ref _codeEditorContent, value)) ScheduleRecoverySave();
            }
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
        private string _selectedProvider;
        public string SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                // Nothing to save when nothing changed (the Settings dropdown pushes the current
                // value back every time the window opens).
                if (!SetProperty(ref _selectedProvider, value)) return;
                _config.SelectedProvider = value;
                _config.Save();
                OnPropertyChanged(nameof(IsGeminiSelected));
                OnPropertyChanged(nameof(IsOpenAiSelected));
                OnPropertyChanged(nameof(IsAnthropicSelected));
                OnPropertyChanged(nameof(IsNvidiaSelected));
                OnPropertyChanged(nameof(ProviderStatusText));
            }
        }

        public bool IsGeminiSelected => SelectedProvider == "Gemini";
        public bool IsOpenAiSelected => SelectedProvider == "OpenAI";
        public bool IsAnthropicSelected => SelectedProvider == "Claude";
        public bool IsNvidiaSelected => SelectedProvider == "NVIDIA";

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

        private string _anthropicApiKeyInput;
        public string AnthropicApiKeyInput
        {
            get => _anthropicApiKeyInput;
            set => SetProperty(ref _anthropicApiKeyInput, value);
        }

        private string _anthropicModel;
        public string AnthropicModel
        {
            get => _anthropicModel;
            set => SetProperty(ref _anthropicModel, value);
        }

        private string _nvidiaApiKeyInput;
        public string NvidiaApiKeyInput
        {
            get => _nvidiaApiKeyInput;
            set => SetProperty(ref _nvidiaApiKeyInput, value);
        }

        /// <summary>Bound to TWO controls in Settings, unlike the OpenAI/Claude model pickers: a fixed
        /// (non-editable) shortlist dropdown and a plain text box, both TwoWay - NVIDIA's catalog is
        /// ~130 models, so any other id can be pasted into the text box.</summary>
        private string _nvidiaModel;
        public string NvidiaModel
        {
            get => _nvidiaModel;
            set => SetProperty(ref _nvidiaModel, value);
        }

        /// <summary>Read straight through from the shared settings rather than kept as a second
        /// copy here: the Saved Scripts window can change the folder too, and Save Script must use
        /// the folder the settings actually hold.</summary>
        public string ScriptsFolderPath
        {
            get => _config.ScriptsFolderPath;
            set
            {
                if (string.Equals(_config.ScriptsFolderPath, value, StringComparison.Ordinal)) return;
                _config.ScriptsFolderPath = value;
                _config.Save();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ChatMessage> History { get; }

        // --- Live Console (interactive shell) ---
        private readonly System.Collections.Generic.List<string> _replHistory = new System.Collections.Generic.List<string>();
        private int _replHistoryIndex;
        // The most recently run console line and, if it failed, its error - feeds SendReplToEditor.
        private string _lastReplCode;
        private string _lastReplErrorMessage;

        private string _replInput = string.Empty;
        public string ReplInput
        {
            get => _replInput;
            set => SetProperty(ref _replInput, value);
        }

        private string _replTranscript =
            "Type a C# statement or expression and press Enter. Available: Document, UIDocument, Application, UIApplication.\n" +
            "Each line auto-commits its own transaction and variables stay alive for the next line - just like a real interactive shell.\n";
        public string ReplTranscript
        {
            get => _replTranscript;
            set => SetProperty(ref _replTranscript, value);
        }

        /// <summary>Fired with (previousCode, newCode) whenever the AI itself rewrites the editor's
        /// code - a successful GenerateCodeAsync, or an auto-fix in RunCodeAsync - so the View can
        /// diff-highlight which lines actually changed. Deliberately NOT raised from FormatCode or
        /// SendReplToEditor - those change the code for reasons unrelated to "what did the AI
        /// change", where a diff highlight would be noisy or meaningless.</summary>
        public event Action<string, string> CodeGenerated;

        public ICommand SaveSettingsCommand { get; }
        public ICommand GenerateCodeCommand { get; }
        public ICommand RunCodeCommand { get; }
        public ICommand ReviewCodeCommand { get; }
        public ICommand FormatCodeCommand { get; }
        public ICommand SaveScriptCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand SnoopSelectionCommand { get; }
        public ICommand ReplRunCommand { get; }
        public ICommand ReplResetCommand { get; }
        public ICommand SendReplToEditorCommand { get; }

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

        private void SaveScript()
        {
            if (string.IsNullOrWhiteSpace(CodeEditorContent)) return;

            if (string.IsNullOrWhiteSpace(ScriptsFolderPath))
            {
                StatusText = "Please select a Scripts Folder in Settings first.";
                return;
            }

            IsBusy = true;
            StatusText = "Saving script...";

            // A read-only, offline or locked folder throws here. Uncaught, that escaped the WPF
            // command and left IsBusy stuck on true - every pane button disabled until Revit restarted.
            try
            {
                if (!Directory.Exists(ScriptsFolderPath))
                {
                    Directory.CreateDirectory(ScriptsFolderPath);
                }

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
                string providerHeader = $"// Provider: {SelectedProvider} (Revit {_revitVersion} target)";
                string fileContent = $"{promptHeader}\n{providerHeader}\n\n{CodeEditorContent}";
                File.WriteAllText(filePath, fileContent);

                StatusText = $"Script saved to {filePath}";
            }
            catch (Exception ex)
            {
                StatusText = $"Could not save the script: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
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

        private void SaveSettings()
        {
            _config.SetGeminiApiKey(GeminiApiKeyInput);
            _config.SetOpenAiApiKey(OpenAiApiKeyInput);
            _config.SetAnthropicApiKey(AnthropicApiKeyInput);
            _config.SetNvidiaApiKey(NvidiaApiKeyInput);
            if (!string.IsNullOrWhiteSpace(OpenAiModel))
            {
                _config.OpenAiModel = OpenAiModel.Trim();
            }
            if (!string.IsNullOrWhiteSpace(AnthropicModel))
            {
                _config.AnthropicModel = AnthropicModel.Trim();
            }
            if (!string.IsNullOrWhiteSpace(NvidiaModel))
            {
                _config.NvidiaModel = NvidiaModel.Trim();
            }
            _config.Save();
            StatusText = "Settings saved securely.";
            OnPropertyChanged(nameof(ProviderStatusText));
        }

        /// <summary>Called when the Settings window closes WITHOUT Save: puts the typed-but-unsaved
        /// API keys and model choices back to what the settings file holds. Without this they stayed
        /// in these fields, showed up again next time Settings opened, and were written by whatever
        /// Save came next. The provider and the scripts folder are not touched - both are saved the
        /// moment they change, so they already match the file.</summary>
        public void RevertUnsavedSettings()
        {
            GeminiApiKeyInput = _config.GetGeminiApiKey();
            OpenAiApiKeyInput = _config.GetOpenAiApiKey();
            AnthropicApiKeyInput = _config.GetAnthropicApiKey();
            NvidiaApiKeyInput = _config.GetNvidiaApiKey();
            OpenAiModel = _config.OpenAiModel;
            AnthropicModel = _config.AnthropicModel;
            NvidiaModel = _config.NvidiaModel;
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

                StatusText = $"Generating code using {activeService.ProviderName}...";

                var messages = new System.Collections.Generic.List<ChatMessage>();
                // "No active document." is the only reply that is the whole context string; with no
                // selection the summary still carries the document and active view, and is sent.
                if (!string.IsNullOrWhiteSpace(contextString) && contextString != "No active document.")
                {
                    messages.Add(new ChatMessage { Role = "user", Content = $"[SYSTEM CONTEXT INJECTION]\n{contextString}\nPlease consider this current context if my request implies operating on selected elements." });
                }
                if (!string.IsNullOrWhiteSpace(CodeEditorContent))
                {
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = "[EXISTING CODE IN THE EDITOR]\n" + CodeEditorContent +
                            "\n\nFirst decide: is my new request below a small, related change to what this existing code " +
                            "already does (e.g. a different color, value, parameter, or category/element target), or is it " +
                            "something substantially different / unrelated? If it is a small related change, return the SAME " +
                            "script with ONLY that specific part changed - keep everything else exactly as it is, do not " +
                            "restructure or rewrite parts that are not affected. If it is unrelated or a substantially " +
                            "different task, ignore the existing code above and write a fresh script for the new request."
                    });
                }
                messages.Add(new ChatMessage { Role = "user", Content = PromptInput });

                string previousCode = CodeEditorContent;
                string response = await activeService.SendMessageAsync(messages, _systemPrompt, _cts.Token);
                string generatedCode = Helpers.CodeExtractionHelper.ExtractCSharpCode(response);

                // A reply with no code must never replace the script in the editor: the editor's
                // Text setter wipes its undo history and the recovery file follows 2 s later, so
                // the old script would be gone for good - while the status still said success.
                if (string.IsNullOrWhiteSpace(generatedCode))
                {
                    StatusText = "The AI returned no code (its reply held an empty code block) - the editor was left as it was.";
                    return;
                }

                CodeEditorContent = generatedCode;
                CodeGenerated?.Invoke(previousCode, CodeEditorContent);

                var safety = GeneratedCodeSafetyValidator.Validate(CodeEditorContent);
                StatusText = safety.HighestLevel == CodeRiskLevel.Safe
                    ? "Code generated successfully."
                    : $"Code generated. Note: {safety.Findings.First().Reason} Review before running.";

                History.Add(new ChatMessage { Role = "user", Content = PromptInput });
                History.Add(new ChatMessage { Role = "model", Content = response });
                TrimHistory();
            }
            catch (OperationCanceledException)
            {
                StatusText = "Stopped.";
            }
            catch (AiEmptyReplyException ex)
            {
                // The provider sent no text at all; its reason (block / refusal / stop) is in the
                // message. The editor was not touched.
                StatusText = $"The AI returned no code - {ex.Message}";
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
                bool autoFixSkipped = false;
                string previousErrorMessage = null;

                // Every risky finding (Delete / Purge / file write) the user has already said Yes to
                // during this run. An auto-fix rewrite can ADD one that the first answer never
                // covered, so the question is asked again whenever a finding is not in here yet.
                var confirmedFindings = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

                Action<int, string> progressCallback = (percent, message) =>
                {
                    Dispatcher dispatcher = _uiDispatcher;
                    if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;

                    if (dispatcher.CheckAccess())
                    {
                        ExecutionProgress = percent;
                        ExecutionProgressText = message;
                        // The script runs ON Revit's UI thread (RevitExecutionService blocks it in
                        // task.Wait()), so WPF gets no chance to repaint on its own until the script
                        // finishes — the bar would stay frozen at 0% even with correct values. Pumping
                        // at Render priority repaints now without processing user input (Input priority
                        // is below Render), so no re-entrancy risk.
                        dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                    }
                    else
                    {
                        dispatcher.BeginInvoke(new Action(() =>
                        {
                            ExecutionProgress = percent;
                            ExecutionProgressText = message;
                        }), DispatcherPriority.Normal);
                    }
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

                    if (safety.RequiresConfirmation && safety.Findings.Any(f => !confirmedFindings.Contains(f.Reason)))
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

                        foreach (var finding in safety.Findings)
                        {
                            confirmedFindings.Add(finding.Reason);
                        }
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

                        var activeService = GetActiveService();
                        if (!activeService.IsConfigured())
                        {
                            // No key = nobody to ask for a fix. Looping on would only re-run the
                            // unchanged script, fail the same way, and then blame the auto-fix.
                            autoFixSkipped = true;
                            ExecutionResults += $"\n\nNo API key - auto-fix skipped. Add a {activeService.ProviderName} API key in Settings to let AJ AI fix a failed script.";
                            StatusText = "Execution failed. No API key - auto-fix skipped.";
                            break;
                        }

                        StatusText = $"Execution failed (Attempt {attempt}/{maxAttempts}). Requesting fix...";

                        if (attempt < maxAttempts && !_cts.IsCancellationRequested)
                        {
                            var errorService = new ErrorCorrectionService(activeService, _revitVersion);
                            string codeBeforeFix = CodeEditorContent;
                            string fixedCode;
                            try
                            {
                                fixedCode = await errorService.RequestFixAsync(CodeEditorContent, result.ErrorMessage, new System.Collections.Generic.List<ChatMessage>(History), _systemPrompt, _cts.Token);
                            }
                            catch (AiEmptyReplyException ex)
                            {
                                // Same outcome as an empty fix before (stop retrying), now with the
                                // provider's reason instead of nothing.
                                ExecutionResults += $"\nThe AI returned no fix - {ex.Message}";
                                break;
                            }

                            if (!string.IsNullOrWhiteSpace(fixedCode))
                            {
                                CodeEditorContent = fixedCode;
                                CodeGenerated?.Invoke(codeBeforeFix, fixedCode);
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
                else if (!success && !blocked && !userCancelledConfirmation && !autoFixSkipped)
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
            if (!activeService.IsConfigured())
            {
                // Same message as Generate - this used to return without a word.
                StatusText = $"Please configure API Key for {activeService.ProviderName} in Settings.";
                return;
            }

            if (string.IsNullOrWhiteSpace(CodeEditorContent)) return;

            History.Add(new ChatMessage { Role = "user", Content = $"Please review the following Revit API C# code for performance, safety, and best practices:\n\n{CodeEditorContent}" });
            TrimHistory();

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                IsBusy = true;
                StatusText = "Reviewing code...";

                var response = await activeService.SendMessageAsync(new System.Collections.Generic.List<ChatMessage>(History), null, _cts.Token);
                History.Add(new ChatMessage { Role = "model", Content = response });
                TrimHistory();

                ExecutionResults = response;
                StatusText = "Review complete.";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Stopped.";
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

        private async Task SnoopSelectionAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusText = "Reading selected element(s)...";
                ExecutionResults = await _snoopService.SnoopSelectionAsync();
                StatusText = "Snoop complete.";
            }
            catch (Exception ex)
            {
                StatusText = $"Snoop failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReplRunAsync()
        {
            if (IsBusy) return;

            string code = ReplInput?.Trim();
            if (string.IsNullOrWhiteSpace(code)) return;

            // Same safety gate as RunCodeAsync (block outright-dangerous patterns, confirm on
            // destructive-but-legitimate ones) - a typed console line is just as capable of touching
            // the live model as an AI-generated script, so it gets the same scrutiny.
            var safety = GeneratedCodeSafetyValidator.Validate(code);
            if (safety.IsBlocked)
            {
                string reasons = string.Join("\n - ", safety.Findings.Where(f => f.Level == CodeRiskLevel.Blocked).Select(f => f.Reason));
                ReplTranscript += $"\n>>> {code}\nBLOCKED - this does something AJ AI does not allow:\n - {reasons}\n";
                return;
            }
            if (safety.RequiresConfirmation)
            {
                string reasons = string.Join("\n - ", safety.Findings.Select(f => f.Reason));
                var confirm = System.Windows.MessageBox.Show(
                    $"This line does the following:\n - {reasons}\n\nThis can only be undone with Ctrl+Z in Revit. Continue?",
                    "AJ AI: Confirm Risky Operation",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes)
                {
                    ReplTranscript += $"\n>>> {code}\nNot run - not confirmed.\n";
                    return;
                }
            }

            _replHistory.Add(code);
            while (_replHistory.Count > MaxReplHistoryEntries) _replHistory.RemoveAt(0);
            _replHistoryIndex = _replHistory.Count;
            SaveReplHistory();
            ReplInput = string.Empty;

            try
            {
                IsBusy = true;
                ReplTranscript += $"\n>>> {code}\n";

                var result = await _replService.ExecuteAsync(code);
                _lastReplCode = code;
                if (result.Success)
                {
                    _lastReplErrorMessage = null;
                    if (!string.IsNullOrEmpty(result.Output)) ReplTranscript += result.Output + "\n";
                }
                else
                {
                    _lastReplErrorMessage = result.ErrorMessage;
                    ReplTranscript += "Error: " + result.ErrorMessage + "\n";
                }
            }
            catch (Exception ex)
            {
                _lastReplCode = code;
                _lastReplErrorMessage = ex.Message;
                ReplTranscript += "Error: " + ex.Message + "\n";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Appends the last-run console line to the Code Editor (never overwrites existing
        /// work there) so a snippet worked out in the Live Console can be kept/extended/saved like
        /// any other script. If that line failed, also pre-fills Prompt with a fix request using the
        /// exact error, so clicking "Generate C# Code" hands it straight to the AI - reuses the
        /// existing incremental-edit Generate flow (v1.5.0) rather than a second AI request path.</summary>
        private void SendReplToEditor()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(_lastReplCode)) return;

            CodeEditorContent = string.IsNullOrEmpty(CodeEditorContent)
                ? _lastReplCode
                : CodeEditorContent + "\n\n" + _lastReplCode;

            if (!string.IsNullOrWhiteSpace(_lastReplErrorMessage))
            {
                PromptInput = $"Fix this error from the line just added to the code above:\n{_lastReplErrorMessage}";
                StatusText = "Sent to Code Editor with the error as the prompt - click \"Generate C# Code\" to ask AI to fix it.";
            }
            else
            {
                StatusText = "Sent to Code Editor - click \"Save Script\" to keep it, or \"Generate C# Code\" to extend it.";
            }
        }

        private void ReplReset()
        {
            if (IsBusy) return;
            _replService.ResetSession();
            _replHistory.Clear();
            _replHistoryIndex = 0;
            ReplTranscript = "Session reset - previous variables are gone, start fresh.\n";
        }

        /// <summary>Up-arrow recall for the console input; called from the View's code-behind.</summary>
        public string RecallPreviousReplCommand()
        {
            if (_replHistory.Count == 0) return ReplInput;
            if (_replHistoryIndex > 0) _replHistoryIndex--;
            return _replHistory[_replHistoryIndex];
        }

        /// <summary>Down-arrow recall for the console input; called from the View's code-behind.</summary>
        public string RecallNextReplCommand()
        {
            if (_replHistory.Count == 0) return string.Empty;
            if (_replHistoryIndex < _replHistory.Count - 1)
            {
                _replHistoryIndex++;
                return _replHistory[_replHistoryIndex];
            }
            _replHistoryIndex = _replHistory.Count;
            return string.Empty;
        }
    }
}
