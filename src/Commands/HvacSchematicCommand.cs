#region Metadata
/*
 * Tool Name     : HVAC Schematic
 * File Name     : HvacSchematicCommand.cs
 * Purpose       : Builds a logical HVAC schematic in a new drafting view from the selected ducts,
 *                 air terminals, and mechanical equipment, using their connector network and levels.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-07
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.HvacSchematic, AJTools.Utils
 *
 * Input         : Selection - ducts, air terminals, and mechanical equipment in an editable project.
 * Output        : A new drafting view with the schematic (detail lines, risers, text notes); final report
 *                 of accepted elements, connections, rejected/unresolved items.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document and a non-empty selection first.
 * - All view/geometry creation runs in ONE transaction, so a single Ctrl+Z removes the schematic.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-07) - Initial production-ready HVAC schematic command.
 * v1.0.0 (2026-07-01) - Refactor/audit: standardized metadata block. Schematic behaviour unchanged.
 * v1.1.0 (2026-07-28) - Error dialogs now include the exception type and the failing AJ Tools
 *                       method/line (trimmed stack trace), so a live crash pinpoints its source
 *                       instead of showing only a bare message ("key not present" debugging).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.HvacSchematic;
using AJTools.Utils;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class HvacSchematicCommand : IExternalCommand
    {
        private const string ToolTitle = "Create HVAC Schematic From Model";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application?.ActiveUIDocument;
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

            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                DialogHelper.ShowError(
                    ToolTitle,
                    "Select one or more Duct, Air Terminal, or Mechanical Equipment elements before running this tool.");
                return Result.Cancelled;
            }

            try
            {
                var levelResolver = new LevelResolverService(document);
                var analysisService = new NetworkAnalysisService(document, levelResolver);
                NetworkAnalysisService.AnalysisResult analysis = analysisService.Analyze(selectedIds);

                if (analysis.Nodes.Count == 0)
                {
                    DialogHelper.ShowError(ToolTitle, BuildNoValidSelectionMessage(analysis.RejectedSelections));
                    return Result.Cancelled;
                }

                var layoutEngine = new SchematicLayoutEngine();
                layoutEngine.Layout(analysis.Nodes, analysis.Edges);

                DraftingViewBuilder.BuildResult buildResult;
                using (Transaction transaction = new Transaction(document, ToolTitle))
                {
                    try
                    {
                        transaction.Start();
                        var builder = new DraftingViewBuilder(document);
                        buildResult = builder.Build(analysis.Nodes, analysis.Edges);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        if (transaction.HasStarted() && !transaction.HasEnded())
                        {
                            transaction.RollBack();
                        }

                        message = ex.Message;
                        DialogHelper.ShowError(ToolTitle, "Failed to create the drafting view.\n\n" + BuildExceptionReport(ex));
                        return Result.Failed;
                    }
                }

                DialogHelper.ShowInfo(ToolTitle, BuildSuccessSummary(analysis, buildResult));
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, "An unexpected error occurred.\n\n" + BuildExceptionReport(ex));
                return Result.Failed;
            }
        }

        /// <summary>
        /// Builds a compact diagnostic block for an error dialog: exception type, message, and the
        /// AJ Tools stack frames (method + line) so a live crash pinpoints its exact source. Inner
        /// exceptions are included because the outermost message alone has proven too vague to act
        /// on (e.g. a bare "The given key was not present in the dictionary." with no location).
        /// </summary>
        private static string BuildExceptionReport(Exception exception)
        {
            var builder = new StringBuilder();

            Exception current = exception;
            int depth = 0;
            while (current != null && depth < 4)
            {
                if (depth > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Caused by:");
                }

                builder.AppendLine(current.GetType().Name + ": " + current.Message);
                AppendRelevantStackFrames(builder, current.StackTrace);

                current = current.InnerException;
                depth++;
            }

            return builder.ToString().Trim();
        }

        private static void AppendRelevantStackFrames(StringBuilder builder, string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return;
            }

            string[] lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Prefer this add-in's own frames (they carry file + line numbers via the deployed PDB);
            // if none match - the throw happened wholly inside Revit/.NET - fall back to the top frames.
            List<string> relevant = lines.Where(line => line.Contains("AJTools")).Take(6).ToList();
            if (relevant.Count == 0)
            {
                relevant = lines.Take(4).ToList();
            }

            for (int i = 0; i < relevant.Count; i++)
            {
                builder.AppendLine(relevant[i].Trim());
            }
        }

        private static string BuildNoValidSelectionMessage(IEnumerable<string> rejectedSelections)
        {
            var builder = new StringBuilder();
            builder.AppendLine("No valid HVAC elements were found in the current selection.");
            builder.AppendLine();
            builder.AppendLine("Supported categories:");
            builder.AppendLine("Duct");
            builder.AppendLine("Air Terminal");
            builder.AppendLine("Mechanical Equipment");

            List<string> rejected = rejectedSelections == null
                ? new List<string>()
                : rejectedSelections.Take(5).ToList();

            if (rejected.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Rejected selections:");
                for (int i = 0; i < rejected.Count; i++)
                {
                    builder.AppendLine("- " + rejected[i]);
                }
            }

            return builder.ToString().Trim();
        }

        private static string BuildSuccessSummary(
            NetworkAnalysisService.AnalysisResult analysis,
            DraftingViewBuilder.BuildResult buildResult)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Drafting view created successfully.");
            builder.AppendLine();
            builder.AppendLine("View: " + buildResult.View.Name);
            builder.AppendLine("Networks: " + analysis.NetworkCount);
            builder.AppendLine("Accepted HVAC elements: " + analysis.Nodes.Count);
            builder.AppendLine("Logical connections drawn: " + analysis.Edges.Count(edge => edge.IsTreeEdge));
            builder.AppendLine("Vertical risers drawn: " + buildResult.LevelTransitionCount);
            builder.AppendLine("Detail lines created: " + buildResult.DetailCurveCount);
            builder.AppendLine("Text notes created: " + buildResult.TextNoteCount);

            AppendSummarySection(builder, "Rejected selections", analysis.RejectedSelections);
            AppendSummarySection(builder, "Elements with no connector data", analysis.MissingConnectorData);
            AppendSummarySection(builder, "Elements with unresolved levels", analysis.UnresolvedLevels);
            AppendSummarySection(builder, "Connections with unresolved branch hierarchy", analysis.UnresolvedConnections);

            return builder.ToString().Trim();
        }

        private static void AppendSummarySection(StringBuilder builder, string title, IList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine(title + ": " + items.Count);

            int previewCount = Math.Min(items.Count, 5);
            for (int i = 0; i < previewCount; i++)
            {
                builder.AppendLine("- " + items[i]);
            }

            if (items.Count > previewCount)
            {
                builder.AppendLine("- ...");
            }
        }
    }
}
