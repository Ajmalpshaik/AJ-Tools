#region Metadata
/*
 * Tool Name     : Connect MEP Elements (Smart Connect)
 * File Name     : SmartConnectCommand.cs
 * Purpose       : Connects MEP elements with a routed run, using the saved Connect MEP Elements
 *                 settings. Works either on a pre-selection (batch) or by picking pairs one by one.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.0.0
 *
 * Created Date  : 2026-03-25
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.SmartConnect, AJTools.UI, AJTools.Utils
 *
 * Input         : Active project. Either a pre-selection of MEP elements, or two picked elements per
 *                 connection (Esc to finish). All behaviour comes from the saved settings.
 * Output        : A connecting run built between each pair, plus a summary of what was and was not done.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document before picking.
 * - No settings dialog: the ribbon's "Connect MEP Elements Settings" button owns that.
 * - Esc during a pick ends the session; the summary still reports what was completed.
 * - Batch runs honour the single-undo setting, so a whole batch can be undone in one step.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-25) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Connect behaviour unchanged.
 * v2.0.0 (2026-08-15) - Settings moved to their own ribbon button, so this command runs straight
 *                       away. Added batch connect from a pre-selection, nearest open-end pairing,
 *                       optional single undo for a whole batch, and one end-of-run summary in place
 *                       of a popup per failure.
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
        private const int MaxPromptErrorLength = 110;

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

                List<Element> preSelection = CollectSupportedSelection(uiDocument, settings);
                if (settings.BatchFromSelection && preSelection.Count >= 2)
                {
                    return RunBatch(uiDocument, settings, preSelection);
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
        // Batch mode
        // ------------------------------------------------------------------

        private Result RunBatch(UIDocument uiDocument, SmartConnectSettings settings, List<Element> candidates)
        {
            Document document = uiDocument.Document;
            List<ElementPair> pairs = BuildNearestPairs(candidates, settings);

            if (pairs.Count == 0)
            {
                DialogHelper.ShowInfo(
                    ToolTitle,
                    "None of the selected elements could be paired up." + Environment.NewLine + Environment.NewLine +
                    "They need free open ends, matching categories, and open ends no further apart than " +
                    settings.MaxPairDistanceMm.ToString("0.##") + " mm.");
                return Result.Cancelled;
            }

            var outcomes = new List<ConnectionOutcome>();
            var routeBuilder = new SmartConnectRouteBuilder(document);

            if (settings.SingleUndoForBatch)
            {
                using (Transaction transaction = new Transaction(document, "Connect MEP Elements (batch)"))
                {
                    transaction.Start();

                    foreach (ElementPair pair in pairs)
                    {
                        outcomes.Add(Connect(routeBuilder, pair, settings));
                    }

                    if (outcomes.Any(outcome => outcome.Success))
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.RollBack();
                    }
                }
            }
            else
            {
                foreach (ElementPair pair in pairs)
                {
                    using (Transaction transaction = new Transaction(document, "Connect MEP Elements"))
                    {
                        transaction.Start();

                        ConnectionOutcome outcome = Connect(routeBuilder, pair, settings);
                        outcomes.Add(outcome);

                        if (outcome.Success)
                        {
                            transaction.Commit();
                        }
                        else
                        {
                            transaction.RollBack();
                        }
                    }
                }
            }

            // Selected elements that never made it into a pair are not failures, but staying quiet
            // about them would read as "all done" when part of the selection was simply ignored.
            var extraNotes = new List<string>();
            int unpaired = candidates.Count - (pairs.Count * 2);
            if (unpaired > 0)
            {
                extraNotes.Add(string.Format(
                    "{0} selected element{1} left alone - no free end within {2:0.##} mm of a matching one.",
                    unpaired,
                    unpaired == 1 ? " was" : "s were",
                    settings.MaxPairDistanceMm));
            }

            // A batch always reports through the summary - one popup per failed pair would be
            // unusable on a selection of any size.
            ShowSummary(outcomes, extraNotes);
            return outcomes.Any(outcome => outcome.Success) ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// Greedy nearest open-end matching: the closest compatible pair is taken first, and each
        /// element is used once, so a selected run never gets pulled in two directions at a time.
        /// </summary>
        private static List<ElementPair> BuildNearestPairs(List<Element> candidates, SmartConnectSettings settings)
        {
            double maxDistance = RevitCompat.MmToInternal(settings.MaxPairDistanceMm);
            var scored = new List<ElementPair>();

            // Read each element's open ends ONCE. Reading them inside the pair loop meant every
            // element's connectors were walked n-1 times, which is what made a large selection crawl.
            var openEnds = new List<XYZ[]>(candidates.Count);
            foreach (Element candidate in candidates)
            {
                IList<Connector> connectors = SmartConnectConnectorUtils.GetOpenConnectors(candidate);
                var origins = new XYZ[connectors.Count];
                for (int index = 0; index < connectors.Count; index++)
                {
                    origins[index] = connectors[index].Origin;
                }

                openEnds.Add(origins);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (openEnds[i].Length == 0)
                {
                    continue;
                }

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (openEnds[j].Length == 0)
                    {
                        continue;
                    }

                    Element first = candidates[i];
                    Element second = candidates[j];

                    string compatibilityError;
                    if (!SmartConnectSelectionFilter.AreCompatible(first, second, out compatibilityError))
                    {
                        continue;
                    }

                    double distance = ClosestDistance(openEnds[i], openEnds[j]);
                    if (distance > maxDistance)
                    {
                        continue;
                    }

                    scored.Add(new ElementPair(first, second, distance));
                }
            }

            var used = new HashSet<ElementId>();
            var result = new List<ElementPair>();

            foreach (ElementPair pair in scored.OrderBy(item => item.Distance))
            {
                if (used.Contains(pair.First.Id) || used.Contains(pair.Second.Id))
                {
                    continue;
                }

                used.Add(pair.First.Id);
                used.Add(pair.Second.Id);
                result.Add(pair);
            }

            return result;
        }

        private static double ClosestDistance(XYZ[] firstEnds, XYZ[] secondEnds)
        {
            double best = double.MaxValue;

            foreach (XYZ firstOrigin in firstEnds)
            {
                foreach (XYZ secondOrigin in secondEnds)
                {
                    double candidate = firstOrigin.DistanceTo(secondOrigin);
                    if (candidate < best)
                    {
                        best = candidate;
                    }
                }
            }

            return best;
        }

        // ------------------------------------------------------------------
        // Interactive mode
        // ------------------------------------------------------------------

        private Result RunInteractive(UIDocument uiDocument, SmartConnectSettings settings)
        {
            Document document = uiDocument.Document;
            var routeBuilder = new SmartConnectRouteBuilder(document);
            var outcomes = new List<ConnectionOutcome>();
            string carriedMessage = string.Empty;

            while (true)
            {
                Element firstElement;
                Element secondElement;
                string pickError;

                try
                {
                    if (!TryPickPair(uiDocument, settings, carriedMessage, out firstElement, out secondElement, out pickError))
                    {
                        carriedMessage = ReportOrCarry(pickError, settings);
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

                    ConnectionOutcome outcome = Connect(routeBuilder, new ElementPair(firstElement, secondElement, 0), settings);
                    outcomes.Add(outcome);

                    if (outcome.Success)
                    {
                        transaction.Commit();
                        carriedMessage = string.Empty;
                    }
                    else
                    {
                        transaction.RollBack();
                        carriedMessage = ReportOrCarry(outcome.Message, settings);
                    }
                }
            }

            if (outcomes.Count == 0)
            {
                return Result.Cancelled;
            }

            // With the setting off, every failure already popped its own dialog during the loop -
            // listing them again at the end would report the same problem twice.
            if (settings.ShowSummaryReport)
            {
                ShowSummary(outcomes);
            }

            return outcomes.Any(outcome => outcome.Success) ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// With summaries on, a failure is carried into the next pick prompt instead of interrupting
        /// with a popup; with summaries off it is shown straight away.
        /// </summary>
        private static string ReportOrCarry(string errorMessage, SmartConnectSettings settings)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return string.Empty;
            }

            if (settings.ShowSummaryReport)
            {
                return errorMessage;
            }

            DialogHelper.ShowError(ToolTitle, errorMessage);
            return string.Empty;
        }

        private static bool TryPickPair(
            UIDocument uiDocument,
            SmartConnectSettings settings,
            string carriedMessage,
            out Element firstElement,
            out Element secondElement,
            out string errorMessage)
        {
            firstElement = null;
            secondElement = null;
            errorMessage = string.Empty;

            string prefix = string.IsNullOrWhiteSpace(carriedMessage)
                ? string.Empty
                : "Last attempt: " + Shorten(carriedMessage) + " | ";

            Reference firstReference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new SmartConnectSelectionFilter(settings),
                prefix + "Select the first element (" +
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
                Label = label,
                Message = result.Success ? string.Empty : label + " - " + result.ErrorMessage
            };

            foreach (string warning in result.Warnings)
            {
                outcome.Warnings.Add(label + " - " + warning);
            }

            if (result.Success && result.AngleWasSubstituted && !result.Warnings.Any())
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
        /// Reports what happened. A clean run says nothing at all, matching the rest of AJ Tools;
        /// anything skipped or worth a second look gets one dialog.
        /// </summary>
        private static void ShowSummary(List<ConnectionOutcome> outcomes)
        {
            ShowSummary(outcomes, null);
        }

        private static void ShowSummary(List<ConnectionOutcome> outcomes, List<string> extraNotes)
        {
            if (outcomes.Count == 0)
            {
                return;
            }

            int succeeded = outcomes.Count(outcome => outcome.Success);
            int failed = outcomes.Count - succeeded;
            List<string> warnings = outcomes.SelectMany(outcome => outcome.Warnings).ToList();
            if (extraNotes != null)
            {
                warnings.AddRange(extraNotes);
            }

            if (failed == 0 && warnings.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Connected " + succeeded + " of " + outcomes.Count + ".");

            if (failed > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Not connected:");
                AppendLines(builder, outcomes.Where(outcome => !outcome.Success).Select(outcome => outcome.Message));
            }

            if (warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Worth checking:");
                AppendLines(builder, warnings);
            }

            if (failed > 0)
            {
                DialogHelper.ShowError(ToolTitle, builder.ToString().TrimEnd());
            }
            else
            {
                DialogHelper.ShowInfo(ToolTitle, builder.ToString().TrimEnd());
            }
        }

        private static void AppendLines(StringBuilder builder, IEnumerable<string> lines)
        {
            var list = lines.ToList();
            foreach (string line in list.Take(MaxSummaryLines))
            {
                builder.AppendLine("  - " + line);
            }

            int remaining = list.Count - MaxSummaryLines;
            if (remaining > 0)
            {
                builder.AppendLine("  - ...and " + remaining + " more.");
            }
        }

        private static string Describe(Element element)
        {
            if (element == null)
            {
                return "Element";
            }

            return SmartConnectSelectionFilter.GetElementDisplayName(element) + " " + element.Id.IntValue();
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxPromptErrorLength)
            {
                return text;
            }

            return text.Substring(0, MaxPromptErrorLength - 3) + "...";
        }

        private sealed class ElementPair
        {
            public ElementPair(Element first, Element second, double distance)
            {
                First = first;
                Second = second;
                Distance = distance;
            }

            public Element First { get; private set; }

            public Element Second { get; private set; }

            public double Distance { get; private set; }
        }

        private sealed class ConnectionOutcome
        {
            public bool Success { get; set; }

            public string Label { get; set; }

            public string Message { get; set; }

            public List<string> Warnings { get; private set; }

            public ConnectionOutcome()
            {
                Warnings = new List<string>();
            }
        }
    }
}
