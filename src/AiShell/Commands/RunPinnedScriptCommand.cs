#region Metadata
/*
 * Tool Name     : C#
 * File Name     : RunPinnedScriptCommand.cs
 * Purpose       : Standalone "Run Pinned Script" ribbon button - runs whichever saved script the
 *                 modeler last pinned from the C# pane's Saved Scripts History (AiShellConfig.
 *                 PinnedScriptPath), with no code panel needed. This is the safe, C#-native
 *                 equivalent of RevitPythonShell's "deploy script as ribbon button" feature: RPS
 *                 does that by emitting a brand-new compiled type per script at runtime via
 *                 System.Reflection.Emit IL generation (one dynamic assembly per pinned script).
 *                 That has no compiler safety net and cannot be verified without a live Revit +
 *                 Visual Studio test loop, so instead of porting it, this is one single, always-
 *                 present, statically-compiled button that reads whichever script is currently
 *                 pinned and runs it through the exact same RoslynService + safety-validator path
 *                 the C# pane's own Run Code button already uses.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-07-23
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
 *
 * Changelog     :
 * v1.0.1 (2026-07-23) - Fixed the same misreported-failure bug as RevitExecutionService v1.4.0:
 *                       RefreshActiveView() ran inside the same try as group.Commit(), so a throw
 *                       there reported an already-committed pinned script as Failed. The refresh
 *                       now has its own try/catch (TryRefreshActiveView).
 * v1.0.0 (2026-07-21) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.IO;
using System.Linq;
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
            var config = AiShellConfig.Load();
            var path = config.PinnedScriptPath;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                TaskDialog.Show(
                    "AJ AI: Run Pinned Script",
                    "No script is pinned yet.\n\nOpen the C# pane, expand \"Saved Scripts History\", and click \"📌 Pin\" on a saved script.");
                return Result.Cancelled;
            }

            string code;
            try
            {
                code = StripSavedScriptHeader(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                message = $"Could not read the pinned script ({path}): {ex.Message}";
                return Result.Failed;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                message = "The pinned script file is empty.";
                return Result.Failed;
            }

            var safety = GeneratedCodeSafetyValidator.Validate(code);
            if (safety.IsBlocked)
            {
                string blockedReasons = string.Join("\n - ", safety.Findings.Where(f => f.Level == CodeRiskLevel.Blocked).Select(f => f.Reason));
                TaskDialog.Show("AJ AI: Pinned Script Blocked", $"This pinned script does something AJ AI does not allow:\n - {blockedReasons}");
                return Result.Cancelled;
            }

            if (safety.RequiresConfirmation)
            {
                string reasons = string.Join("\n - ", safety.Findings.Select(f => f.Reason));
                var confirm = TaskDialog.Show(
                    "AJ AI: Confirm Risky Operation",
                    $"This pinned script does the following:\n - {reasons}\n\nThis can only be undone with Ctrl+Z in Revit. Continue?",
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

            var globals = new RevitScriptGlobals
            {
                UIApplication = commandData.Application,
                Application = commandData.Application.Application,
                UIDocument = uidoc,
                Document = uidoc.Document,
                ReportProgress = null
            };

            using (var group = new TransactionGroup(globals.Document, "AJ AI Pinned Script"))
            {
                group.Start();

                try
                {
                    var roslynService = new RoslynService();
                    var task = roslynService.ExecuteAsync(code, globals);
                    task.Wait();
                    var result = task.Result;

                    if (result.Success)
                    {
                        group.Commit();
                        TryRefreshActiveView(commandData.Application);
                        TaskDialog.Show("AJ AI: Pinned Script Ran Successfully", result.Output ?? "Done.");
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
