#region Metadata
/*
 * Tool Name     : Connect MEP Elements (Smart Connect)
 * File Name     : SmartConnectCommand.cs
 * Purpose       : Connects MEP elements with a routed run, using the saved Connect MEP Elements
 *                 settings. Connects an exact pre-selected pair directly, or picks pairs one by one.
 *
 * Author        : Ajmal P.S.
 * Version       : 3.0.0
 *
 * Created Date  : 2026-03-25
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.SmartConnect, AJTools.UI, AJTools.Utils
 *
 * Input         : Active project. Either exactly two elements pre-selected, or two picked elements
 *                 per connection (Esc to finish). All behaviour comes from the saved settings.
 * Output        : A connecting run. A failure is reported as it happens when "Show failed report" is
 *                 on; anything merely worth a second look is collected and shown once at the end.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document before picking.
 * - No settings dialog: the ribbon's "Connect MEP Elements Settings" button owns that.
 * - Esc during a pick ends the session.
 * - Selecting exactly two elements first connects them directly, no matching or pairing involved.
 *   Selecting more than two asks the user to narrow it down to two, rather than guessing pairs.
 * - Failures are never listed twice: ReportFailure shows them as they happen, ShowWarnings at the
 *   end deliberately carries warnings only.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-25) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Connect behaviour unchanged.
 * v2.0.0 (2026-08-15) - Settings moved to their own ribbon button, so this command runs straight
 *                       away. Added batch connect from a pre-selection, nearest open-end pairing,
 *                       optional single undo for a whole batch, and one end-of-run summary in place
 *                       of a popup per failure.
 * v2.1.0 (2026-08-16) - Removed the nearest-open-end auto-pairing algorithm (BuildNearestPairs) and
 *                       its distance setting, at Ajmal's request: "that connection method no need,
 *                       you can remove - if i need i will select the pipe or what elements then it
 *                       will connect." A pre-selection of exactly two elements now connects them
 *                       directly, no matching involved; more than two asks the user to narrow it
 *                       down instead of guessing. Removed the now-unneeded single-undo-for-batch
 *                       setting along with it, since there is no longer a multi-pair batch to group.
 * v3.0.0 (2026-08-16) - "Show failed report" replaced the old carry-into-the-next-prompt behaviour:
 *                       a failure is now a popup naming the reason, or nothing at all when the box is
 *                       unticked. ShowSummary became ShowWarnings and no longer lists failures, since
 *                       they have already been reported as they happened - listing them again was
 *                       reporting the same problem twice. The carried-message plumbing (Shorten,
 *                       MaxPromptErrorLength, the prompt prefix) went with it.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AJTools.Models;
using AJTools.Services.SmartConnect;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Entry command for the Connect MEP Elements workflow.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SmartConnectCommand : IExternalCommand
    {
        private const string ToolTitle = "Connect MEP Elements";
        private const int MaxSummaryLines = 12;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application == null ? null : commandData.Application.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uiDocument, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;
            if (!ValidationHelper.ValidateEditableDocument(document, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            try
            {
                SmartConnectSettings settings = new SmartConnectSettingsService().Load();

                if (settings.BatchFromSelection)
                {
                    List<Element> preSelection = CollectSupportedSelection(uiDocument, settings);
                    if (preSelection.Count == 2)
                    {
                        return RunDirectPair(uiDocument, settings, preSelection[0], preSelection[1]);
                    }

                    if (preSelection.Count > 2)
                    {
                        DialogHelper.ShowInfo(
                            ToolTitle,
                            "Select exactly two elements to connect them directly, or none to pick pairs on screen.");
                        return Result.Cancelled;
                    }
                }

                return RunInteractive(uiDocument, settings);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, "An unexpected error occurred:" + Environment.NewLine + ex.Message);
                return Result.Failed;
            }
        }

        // ------------------------------------------------------------------
        // Direct pair (exactly two elements pre-selected)
        // ------------------------------------------------------------------

        /// <summary>
        /// Connects exactly the two elements the user already selected - no matching, no guessing
        /// which pairs with which. One decision (what to select) belongs to the user; the tool just
        /// connects what it was handed.
        /// </summary>
        private Result RunDirectPair(UIDocument uiDocument, SmartConnectSettings settings, Element first, Element second)
        {
            string compatibilityError;
            if (!SmartConnectSelectionFilter.AreCompatible(first, second, out compatibilityError))
            {
                ReportFailure(compatibilityError, settings);
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;
            var routeBuilder = new SmartConnectRouteBuilder(document);
            ConnectionOutcome outcome;

            using (Transaction transaction = new Transaction(document, "Connect MEP Elements"))
            {
                transaction.Start();

                outcome = Connect(routeBuilder, new ElementPair(first, second), settings);

                if (outcome.Success)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }

            if (!outcome.Success)
            {
                ReportFailure(outcome.Message, settings);
                return Result.Cancelled;
            }

            ShowWarnings(new List<ConnectionOutcome> { outcome });
            return Result.Succeeded;
        }

        // ------------------------------------------------------------------
        // Interactive mode
        // ------------------------------------------------------------------

        private Result RunInteractive(UIDocument uiDocument, SmartConnectSettings settings)
        {
            Document document = uiDocument.Document;
            var routeBuilder = new SmartConnectRouteBuilder(document);
            var outcomes = new List<ConnectionOutcome>();

            while (true)
            {
                Element firstElement;
                Element secondElement;
                string pickError;

                try
                {
                    if (!TryPickPair(uiDocument, settings, out firstElement, out secondElement, out pickError))
                    {
                        ReportFailure(pickError, settings);
                        continue;
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                using (Transaction transaction = new Transaction(document, "Connect MEP Elements"))
                {
                    transaction.Start();

                    ConnectionOutcome outcome = Connect(routeBuilder, new ElementPair(firstElement, secondElement), settings);
                    outcomes.Add(outcome);

                    if (outcome.Success)
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.RollBack();
                        ReportFailure(outcome.Message, settings);
                    }
                }
            }

            if (outcomes.Count == 0)
            {
                return Result.Cancelled;
            }

            // Failures have already been reported one by one as they happened, so only the things
            // worth a second look are left to show.
            ShowWarnings(outcomes);

            return outcomes.Any(outcome => outcome.Success) ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// Shows why an attempt failed, as a popup, when the user has asked for failed reports.
        /// With the setting off nothing is shown at all and the user simply tries again.
        /// </summary>
        private static void ReportFailure(string errorMessage, SmartConnectSettings settings)
        {
            if (string.IsNullOrWhiteSpace(errorMessage) || !settings.ShowFailedReport)
            {
                return;
            }

            DialogHelper.ShowError(ToolTitle, errorMessage);
        }

        private static bool TryPickPair(
            UIDocument uiDocument,
            SmartConnectSettings settings,
            out Element firstElement,
            out Element secondElement,
            out string errorMessage)
        {
            firstElement = null;
            secondElement = null;
            errorMessage = string.Empty;

            Reference firstReference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new SmartConnectSelectionFilter(settings),
                "Select the first element (" +
                SmartConnectSelectionFilter.DescribeSupportedCategories(settings) + "). Esc to finish.");

            firstElement = uiDocument.Document.GetElement(firstReference);
            if (firstElement == null)
            {
                errorMessage = "The first selection is not usable.";
                return false;
            }

            Reference secondReference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new SmartConnectSelectionFilter(settings, firstElement),
                "Select the element to connect to this " +
                SmartConnectSelectionFilter.GetElementDisplayName(firstElement) + ".");

            secondElement = uiDocument.Document.GetElement(secondReference);
            if (secondElement == null)
            {
                errorMessage = "The second selection is not usable.";
                return false;
            }

            return SmartConnectSelectionFilter.AreCompatible(firstElement, secondElement, out errorMessage);
        }

        // ------------------------------------------------------------------
        // Shared
        // ------------------------------------------------------------------

        private static ConnectionOutcome Connect(
            SmartConnectRouteBuilder routeBuilder,
            ElementPair pair,
            SmartConnectSettings settings)
        {
            string label = Describe(pair.First) + " + " + Describe(pair.Second);
            SmartConnectRouteResult result = routeBuilder.BuildRoute(pair.First, pair.Second, settings);

            var outcome = new ConnectionOutcome
            {
                Success = result.Success,
                Message = result.Success ? string.Empty : label + " - " + result.ErrorMessage
            };

            foreach (string warning in result.Warnings)
            {
                outcome.Warnings.Add(label + " - " + warning);
            }

            // No !Warnings.Any() guard here: the builder sets AngleWasSubstituted only when the
            // angle was NOT geometry-fixed, and adds its own geometry warning only when it WAS, so
            // the two can never both fire for one route. Guarding on it only suppressed this notice
            // whenever an unrelated insulation or clash warning happened to be present.
            if (result.Success && result.AngleWasSubstituted)
            {
                outcome.Warnings.Add(string.Format(
                    "{0} - built at {1:0.##}° instead of the chosen {2:0.##}°.",
                    label,
                    result.AngleUsedDegrees,
                    settings.SelectedAngleDegrees));
            }

            return outcome;
        }

        private static List<Element> CollectSupportedSelection(UIDocument uiDocument, SmartConnectSettings settings)
        {
            var result = new List<Element>();

            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return result;
            }

            foreach (ElementId id in selectedIds)
            {
                Element element = uiDocument.Document.GetElement(id);
                if (SmartConnectSelectionFilter.IsSupported(element, settings))
                {
                    result.Add(element);
                }
            }

            return result;
        }

        /// <summary>
        /// Shows anything worth a second look - a route built at a different angle, insulation that
        /// would not copy, a clash. Failures are NOT listed here: they are reported as they happen,
        /// so repeating them at the end would report the same problem twice. A clean run says nothing
        /// at all, matching the rest of AJ Tools.
        /// </summary>
        private static void ShowWarnings(List<ConnectionOutcome> outcomes)
        {
            List<string> warnings = outcomes.SelectMany(outcome => outcome.Warnings).ToList();
            if (warnings.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Worth checking:");

            foreach (string line in warnings.Take(MaxSummaryLines))
            {
                builder.AppendLine("  - " + line);
            }

            int remaining = warnings.Count - MaxSummaryLines;
            if (remaining > 0)
            {
                builder.AppendLine("  - ...and " + remaining + " more.");
            }

            DialogHelper.ShowInfo(ToolTitle, builder.ToString().TrimEnd());
        }

        private static string Describe(Element element)
        {
            if (element == null)
            {
                return "Element";
            }

            return SmartConnectSelectionFilter.GetElementDisplayName(element) + " " + element.Id.IntValue();
        }

        private sealed class ElementPair
        {
            public ElementPair(Element first, Element second)
            {
                First = first;
                Second = second;
            }

            public Element First { get; private set; }

            public Element Second { get; private set; }
        }

        private sealed class ConnectionOutcome
        {
            public bool Success { get; set; }

            public string Message { get; set; }

            public List<string> Warnings { get; private set; }

            public ConnectionOutcome()
            {
                Warnings = new List<string>();
            }
        }
    }
}
