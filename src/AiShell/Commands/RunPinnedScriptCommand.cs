#region Metadata
/*
 * Tool Name     : C#
 * File Name     : RunPinnedScriptCommand.cs
 * Purpose       : Standalone "Run Pinned Script" ribbon button - runs whichever saved script the
 *                 modeler pinned with "📌 Pin" in the Saved Scripts window (AiShellConfig.
 *                 PinnedScriptPath), with no code panel needed. Deliberately ONE always-present,
 *                 statically-compiled button that reads whichever script is pinned right now,
 *                 rather than a new button type generated per script at runtime - generated
 *                 command types have no compiler safety net and cannot be checked without a live
 *                 Revit test loop. The script goes through the same safety validator, the same
 *                 RoslynService compile/loop protection and the same 60 s limit with a bounded
 *                 wait as the C# pane's Run Code button; unlike the pane it runs directly on
 *                 Revit's command thread (no ExternalEvent) and without the pane's AI auto-fix.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.2
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : AiShellConfig, RoslynService, GeneratedCodeSafetyValidator
 *
 * Input         : Ribbon button click (no arguments - reads AiShellConfig.PinnedScriptPath).
 * Output        : Runs the pinned script's code against the active document; TaskDialog with the
 *                 result (or a friendly "nothing pinned yet" message).
 *
 * Notes         :
 * - Unlike the C# pane (which marshals onto Revit's API thread via ExternalEvent because it's
 *   called from a WPF pane running independently of Revit's command dispatch), this class IS an
 *   IExternalCommand - Revit already invokes Execute() on its own API thread, so no ExternalEvent
 *   is needed here; RoslynService is called directly.
 * - Wrapped in a TransactionGroup (not a single Transaction) for the same reason
 *   RevitExecutionService uses one: a pinned script may open its own Transaction(s) internally,
 *   same as any AI-generated script saved from the C# pane.
 * - Saved script files carry a "// Prompt: ..." / "// Provider: ..." header (written by
 *   AiShellViewModel.SaveScript) before the actual code - stripped here the same way, kept as an
 *   independent small copy rather than shared with the ViewModel: this class must not depend on
 *   the WPF ViewModel layer.
 * - RunScriptFile() is the shared "strip header, validate, confirm, run" path - also called by
 *   SavedScriptsWindow's "▶ Run" button (2026-07-21), so both entry points share one execution
 *   path instead of two copies that could drift.
 *
 * Changelog     :
 * v1.3.2 (2026-10-12) - Loop protection for Run Pinned / Saved Scripts Run: the script now gets the
 *                       same 60 s CancellationToken and the same bounded wait (and timeout message)
 *                       as the C# pane, shared from RevitExecutionService. Before, the token was
 *                       never set and task.Wait() had no limit, so a script stuck in a loop froze
 *                       Revit with no way out. Reads the session's shared settings
 *                       (AiShellConfig.Shared). Purpose text corrected: pinning lives in the Saved
 *                       Scripts window, and the run path is shared with the pane, not identical.
 *                       Also: a pinned script that ran cleanly shows its own output (what it printed)
 *                       only when it printed something; no bare "Done." box.
 * v1.3.1 (2026-07-25) - Merged in the GitHub-side v1.0.1 fix below (TryRefreshActiveView), which
 *                       had been made on the pre-RunScriptFile layout of this file.
 * v1.3.0 (2026-07-21) - Reverted v1.2.0 below - Run Pinned's default face is now permanently fixed
 *                       (RibbonManager.cs v1.12.0, IsSynchronizedWithCurrentItem = false), so Execute()
 *                       no longer needs to set CurrentButton itself.
 * v1.2.0 (2026-07-21) - Now sets the ribbon split button's CurrentButton to itself as the first thing
 *                       Execute() does (App.RunPinnedSplitButton / RunPinnedButton) - Run Pinned and
 *                       Saved Scripts are now one split button (RibbonManager.cs v1.11.0) instead of
 *                       two separate top-level buttons; this keeps its default face in sync. Also
 *                       corrected this header's own Version field, which had drifted to "1.0.0" even
 *                       though v1.1.0 below already shipped.
 * v1.1.0 (2026-07-21) - Extracted the run logic into public static RunScriptFile() so
 *                       SavedScriptsWindow (the new standalone "Saved Scripts" tool) can run a
 *                       script the same way without duplicating the safety/transaction handling.
 * v1.0.1 (2026-07-23) - (GitHub line) Fixed the same misreported-failure bug as RevitExecutionService
 *                       v1.4.0: RefreshActiveView() ran inside the same try as group.Commit(), so a
 *                       throw there reported an already-committed pinned script as Failed. The
 *                       refresh now has its own try/catch (TryRefreshActiveView).
 * v1.0.0 (2026-07-21) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.AiShell.Configuration;
using AJTools.AiShell.Models;
using AJTools.AiShell.Services;

namespace AJTools.AiShell.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RunPinnedScriptCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var config = AiShellConfig.Shared;
            var path = config.PinnedScriptPath;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                TaskDialog.Show(
                    "AJ AI: Run Pinned Script",
                    "No script is pinned yet.\n\nOpen \"Saved Scripts\" (AI Assistant panel) and click \"📌 Pin\" on a saved script.");
                return Result.Cancelled;
            }

            return RunScriptFile(commandData, path, ref message);
        }

        /// <summary>Shared by RunPinnedScriptCommand and SavedScriptsWindow's "▶ Run" button: strips
        /// the saved-script header, runs the safety validator, confirms risky operations, then
        /// executes via RoslynService inside a TransactionGroup and reports the result.</summary>
        public static Result RunScriptFile(ExternalCommandData commandData, string path, ref string message)
        {
            string code;
            try
            {
                code = StripSavedScriptHeader(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                message = $"Could not read the script ({path}): {ex.Message}";
                return Result.Failed;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                message = "The script file is empty.";
                return Result.Failed;
            }

            var safety = GeneratedCodeSafetyValidator.Validate(code);
            if (safety.IsBlocked)
            {
                string blockedReasons = string.Join("\n - ", safety.Findings.Where(f => f.Level == CodeRiskLevel.Blocked).Select(f => f.Reason));
                TaskDialog.Show("AJ AI: Script Blocked", $"This script does something AJ AI does not allow:\n - {blockedReasons}");
                return Result.Cancelled;
            }

            if (safety.RequiresConfirmation)
            {
                string reasons = string.Join("\n - ", safety.Findings.Select(f => f.Reason));
                var confirm = TaskDialog.Show(
                    "AJ AI: Confirm Risky Operation",
                    $"This script does the following:\n - {reasons}\n\nThis can only be undone with Ctrl+Z in Revit. Continue?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirm != TaskDialogResult.Yes)
                {
                    return Result.Cancelled;
                }
            }

            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "No active document is open in Revit. Please open a project first.";
                return Result.Failed;
            }

            // Same loop protection as the C# pane (RevitExecutionService): the script's loop
            // checkpoints watch this token, which cancels itself after MaxLoopRuntime, and the wait
            // below gives up after HardWaitTimeout so a script that ignores the token cannot hold
            // Revit's UI thread forever.
            using (var timeoutCts = new CancellationTokenSource(RevitExecutionService.MaxLoopRuntime))
            {
                var globals = new RevitScriptGlobals
                {
                    UIApplication = commandData.Application,
                    Application = commandData.Application.Application,
                    UIDocument = uidoc,
                    Document = uidoc.Document,
                    CancellationToken = timeoutCts.Token,
                    ReportProgress = null
                };

                using (var group = new TransactionGroup(globals.Document, "AJ AI Script"))
                {
                    group.Start();

                    try
                    {
                        var roslynService = new RoslynService();
                        var task = roslynService.ExecuteAsync(code, globals);
                        if (!task.Wait(RevitExecutionService.HardWaitTimeout))
                        {
                            // Same choice as RevitExecutionService: the script may still be running,
                            // so the group is not touched from here - leaving the using block lets
                            // Dispose() roll it back.
                            message = RevitExecutionService.TimeoutMessage;
                            return Result.Failed;
                        }

                        var result = task.Result;

                        if (result.Success)
                        {
                            group.Commit();
                            TryRefreshActiveView(commandData.Application);
                            // Show what the script printed - that is its answer. A script that printed
                            // nothing ends quietly (house rule: no bare "Done." box).
                            if (!string.IsNullOrWhiteSpace(result.Output))
                            {
                                TaskDialog.Show("AJ AI: Script Ran Successfully", result.Output);
                            }

                            return Result.Succeeded;
                        }

                        group.RollBack();
                        message = result.ErrorMessage;
                        return Result.Failed;
                    }
                    catch (Exception ex)
                    {
                        try { group.RollBack(); } catch { /* group may already be in a terminal state */ }
                        message = ex.InnerException?.Message ?? ex.Message;
                        return Result.Failed;
                    }
                }
            }
        }

        // RefreshActiveView() is a UI nicety, not part of the result - if it throws after
        // group.Commit() already succeeded above, letting that exception reach the outer catch
        // would report an already-committed change as Failed and try to RollBack() a group that
        // is no longer rollback-able (same bug class fixed in RevitExecutionService v1.4.0).
        private static void TryRefreshActiveView(UIApplication app)
        {
            try { app.ActiveUIDocument?.RefreshActiveView(); }
            catch { /* cosmetic only - never turn an already-committed success into a reported failure */ }
        }

        private static string StripSavedScriptHeader(string rawContent)
        {
            string remaining = rawContent ?? string.Empty;
            remaining = ConsumeHeaderLineIfPresent(remaining, "// Prompt: ");
            remaining = ConsumeHeaderLineIfPresent(remaining, "// Provider: ");
            return remaining.TrimStart('\r', '\n');
        }

        private static string ConsumeHeaderLineIfPresent(string content, string prefix)
        {
            if (string.IsNullOrEmpty(content) || !content.StartsWith(prefix))
            {
                return content;
            }

            int lineEnd = content.IndexOf('\n');
            return lineEnd < 0 ? string.Empty : content.Substring(lineEnd + 1);
        }
    }
}
