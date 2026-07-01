#region Metadata
/*
 * Tool Name     : HVAC Schematic
 * File Name     : HvacSchematicCommand.cs
 * Purpose       : Builds a logical HVAC schematic in a new drafting view from the selected ducts,
 *                 air terminals, and mechanical equipment, using their connector network and levels.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
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
                        DialogHelper.ShowError(ToolTitle, "Failed to create the drafting view.\n\n" + ex.Message);
                        return Result.Failed;
                    }
                }

                DialogHelper.ShowInfo(ToolTitle, BuildSuccessSummary(analysis, buildResult));
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, "An unexpected error occurred.\n\n" + ex.Message);
                return Result.Failed;
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
