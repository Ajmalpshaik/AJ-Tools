// ==================================================
// Tool Name    : HVAC Schematic
// Purpose      : Generates drafting-view graphics for HVAC schematic nodes, edges, and level bands.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-05-07
// Last Updated : 2026-07-24
// Target       : Revit 2020 - latest
// Framework    : .NET Framework 4.7.2 baseline (per-version build configurations)
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Laid-out schematic nodes and edges.
// Output       : A populated Revit drafting view representing the HVAC schematic.
// Notes        : Creates detail lines and text notes only inside a valid Revit transaction.
// Changelog    : v1.1.0 - Connections now follow the layout tree hierarchy (hints are fallback only);
//                         equipment drawn on its laid-out row instead of always the trunk row;
//                         risers enter/exit through the edge facing the other element.
//                v1.0.0 - Initial production-ready HVAC schematic drafting builder with standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.HvacSchematic;

using AJTools.Utils;
namespace AJTools.Services.HvacSchematic
{
    internal sealed class DraftingViewBuilder
    {
        internal sealed class BuildResult
        {
            public ViewDrafting View { get; set; }
            public int DetailCurveCount { get; set; }
            public int TextNoteCount { get; set; }
            public int LevelTransitionCount { get; set; }
        }

        private const double EquipmentHalfWidth = 2.4;
        private const double EquipmentHalfHeight = 2.0;
        private const double DuctTickHalfWidth = 1.6;
        private const double DuctTickHalfHeight = 0.6;
        private const double TerminalHalfWidth = 1.0;
        private const double TerminalHeight = 1.4;
        private const double LevelLabelOffsetX = 6.0;
        private const double LevelLabelOffsetY = 0.8;
        private const double LevelGuideMargin = 8.0;
        private const double DuctLabelOffsetY = 1.2;
        private const double TerminalLabelOffsetY = 1.6;
        private const double EquipmentLabelOffsetY = 0.8;
        private const double Tolerance = 1e-6;

        private readonly Document _document;

        public DraftingViewBuilder(Document document)
        {
            _document = document;
        }

        public BuildResult Build(IList<SchematicNode> nodes, IList<SchematicEdge> edges)
        {
            if (nodes == null || nodes.Count == 0)
            {
                throw new ArgumentException("No schematic nodes were supplied.", nameof(nodes));
            }

            ViewFamilyType draftingViewType = new FilteredElementCollector(_document)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(type => type.ViewFamily == ViewFamily.Drafting);

            if (draftingViewType == null)
            {
                throw new InvalidOperationException("No Drafting View type is available in this project.");
            }

            ViewDrafting view = ViewDrafting.Create(_document, draftingViewType.Id);
            view.Name = "HVAC Schematic " + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

            ElementId textTypeId = _document.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            Dictionary<int, SchematicNode> nodeById = nodes.ToDictionary(node => node.ElementId.IntValue());
            BuildResult result = new BuildResult { View = view };

            DrawLevelBands(view, textTypeId, nodes, result);

            List<SchematicEdge> treeEdges = edges
                .Where(edge => edge.IsTreeEdge)
                .OrderBy(edge => edge.NetworkIndex)
                .ToList();

            for (int i = 0; i < treeEdges.Count; i++)
            {
                SchematicEdge edge = treeEdges[i];
                SchematicNode parent;
                SchematicNode child;
                if (!TryResolveParentChild(edge, nodeById, out parent, out child))
                {
                    continue;
                }

                DrawConnection(view, parent, child, edge.IsLevelTransition, result);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                DrawNode(view, textTypeId, nodes[i], result);
            }

            return result;
        }

        private void DrawLevelBands(ViewDrafting view, ElementId textTypeId, IList<SchematicNode> nodes, BuildResult result)
        {
            double minX = nodes.Min(node => node.Position.X) - LevelGuideMargin;
            double maxX = nodes.Max(node => node.Position.X) + LevelGuideMargin;

            List<LevelBand> bands = nodes
                .GroupBy(CreateBandKey)
                .Select(group =>
                {
                    SchematicNode anchor = group.First();
                    return new LevelBand(anchor.LevelName, anchor.GuideY);
                })
                .OrderByDescending(band => band.GuideY)
                .ToList();

            for (int i = 0; i < bands.Count; i++)
            {
                LevelBand band = bands[i];
                XYZ start = new XYZ(minX, band.GuideY, 0);
                XYZ end = new XYZ(maxX, band.GuideY, 0);
                CreateDetailLine(view, start, end, result);
                CreateTextNote(
                    view,
                    textTypeId,
                    new XYZ(minX - LevelLabelOffsetX, band.GuideY + LevelLabelOffsetY, 0),
                    band.Label,
                    result);
            }
        }

        private void DrawConnection(
            ViewDrafting view,
            SchematicNode parent,
            SchematicNode child,
            bool isLevelTransition,
            BuildResult result)
        {
            if (parent == null || child == null)
            {
                return;
            }

            if (isLevelTransition)
            {
                result.LevelTransitionCount++;
            }

            XYZ start = GetParentExitPoint(parent, child);
            XYZ end = GetChildEntryPoint(child, parent);

            bool sameY = Math.Abs(start.Y - end.Y) < Tolerance;
            bool sameX = Math.Abs(start.X - end.X) < Tolerance;

            // Tree-root hierarchy: continuation = straight line, branch = single L.
            // The elbow sits at (child.X, parent.Y) so the parent's row stays a
            // clean horizontal spine and the branch drops vertically to the child.
            // No Z-shape, no up-over-down, no imitation of elbow or take-off fittings.
            if (sameY || sameX)
            {
                CreateDetailLine(view, start, end, result);
                return;
            }

            XYZ elbow = new XYZ(end.X, start.Y, 0);
            CreateDetailLine(view, start, elbow, result);
            CreateDetailLine(view, elbow, end, result);
        }

        private void DrawNode(ViewDrafting view, ElementId textTypeId, SchematicNode node, BuildResult result)
        {
            switch (node.NodeType)
            {
                case SchematicNodeType.Equipment:
                    DrawEquipment(view, node, result);
                    CreateTextNote(
                        view,
                        textTypeId,
                        new XYZ(node.Position.X - EquipmentHalfWidth, node.Position.Y + EquipmentHalfHeight + EquipmentLabelOffsetY, 0),
                        node.Label,
                        result);
                    break;

                case SchematicNodeType.AirTerminal:
                    DrawTerminal(view, node, result);
                    string terminalText = ComposeTerminalAnnotation(node);
                    if (!string.IsNullOrWhiteSpace(terminalText))
                    {
                        CreateTextNote(
                            view,
                            textTypeId,
                            new XYZ(node.Position.X - TerminalHalfWidth, node.Position.Y - TerminalHeight - TerminalLabelOffsetY, 0),
                            terminalText,
                            result);
                    }
                    break;

                default:
                    DrawDuct(view, node, result);
                    string ductText = ComposeDuctAnnotation(node);
                    if (!string.IsNullOrWhiteSpace(ductText))
                    {
                        CreateTextNote(
                            view,
                            textTypeId,
                            new XYZ(node.Position.X - DuctTickHalfWidth, node.Position.Y + DuctTickHalfHeight + DuctLabelOffsetY, 0),
                            ductText,
                            result);
                    }
                    break;
            }
        }

        private void DrawEquipment(ViewDrafting view, SchematicNode node, BuildResult result)
        {
            // Draw at the laid-out row, not the trunk row: equipment on a branch
            // (VAV, fan coil) must sit on its branch, only main-run equipment
            // (root or in-line continuation) actually lands on the trunk row.
            double centerY = node.Position.Y;
            XYZ topLeft = new XYZ(node.Position.X - EquipmentHalfWidth, centerY + EquipmentHalfHeight, 0);
            XYZ topRight = new XYZ(node.Position.X + EquipmentHalfWidth, centerY + EquipmentHalfHeight, 0);
            XYZ bottomRight = new XYZ(node.Position.X + EquipmentHalfWidth, centerY - EquipmentHalfHeight, 0);
            XYZ bottomLeft = new XYZ(node.Position.X - EquipmentHalfWidth, centerY - EquipmentHalfHeight, 0);

            CreateDetailLine(view, topLeft, topRight, result);
            CreateDetailLine(view, topRight, bottomRight, result);
            CreateDetailLine(view, bottomRight, bottomLeft, result);
            CreateDetailLine(view, bottomLeft, topLeft, result);
        }

        private void DrawDuct(ViewDrafting view, SchematicNode node, BuildResult result)
        {
            double y = node.Position.Y;
            XYZ left = new XYZ(node.Position.X - DuctTickHalfWidth, y, 0);
            XYZ right = new XYZ(node.Position.X + DuctTickHalfWidth, y, 0);
            XYZ tickTop = new XYZ(node.Position.X, y + DuctTickHalfHeight, 0);
            XYZ tickBottom = new XYZ(node.Position.X, y - DuctTickHalfHeight, 0);

            CreateDetailLine(view, left, right, result);
            CreateDetailLine(view, tickTop, tickBottom, result);
        }

        private void DrawTerminal(ViewDrafting view, SchematicNode node, BuildResult result)
        {
            // Triangle with tip at the connection point pointing up toward the parent branch.
            XYZ tip = new XYZ(node.Position.X, node.Position.Y, 0);
            XYZ baseLeft = new XYZ(node.Position.X - TerminalHalfWidth, node.Position.Y - TerminalHeight, 0);
            XYZ baseRight = new XYZ(node.Position.X + TerminalHalfWidth, node.Position.Y - TerminalHeight, 0);

            CreateDetailLine(view, tip, baseRight, result);
            CreateDetailLine(view, baseRight, baseLeft, result);
            CreateDetailLine(view, baseLeft, tip, result);
        }

        private static bool TryResolveParentChild(
            SchematicEdge edge,
            IDictionary<int, SchematicNode> nodeById,
            out SchematicNode parent,
            out SchematicNode child)
        {
            parent = null;
            child = null;

            SchematicNode first;
            SchematicNode second;
            if (!nodeById.TryGetValue(edge.FromElementId.IntValue(), out first) ||
                !nodeById.TryGetValue(edge.ToElementId.IntValue(), out second))
            {
                return false;
            }

            // The layout engine already placed every node from its tree hierarchy, so the
            // drawn line must follow that same hierarchy. Flow-direction hints are only a
            // fallback: letting them override the tree draws lines against the layout
            // (crossing through symbols, entering elements from the wrong side).
            if (first.ParentElementId.IntValue() == second.ElementId.IntValue())
            {
                parent = second;
                child = first;
                return true;
            }

            if (second.ParentElementId.IntValue() == first.ElementId.IntValue())
            {
                parent = first;
                child = second;
                return true;
            }

            if (edge.HasDirectionHint)
            {
                if (nodeById.TryGetValue(edge.PreferredParentElementId.IntValue(), out parent) &&
                    nodeById.TryGetValue(edge.PreferredChildElementId.IntValue(), out child))
                {
                    return true;
                }
            }

            if (first.Depth <= second.Depth)
            {
                parent = first;
                child = second;
            }
            else
            {
                parent = second;
                child = first;
            }

            return true;
        }

        private static XYZ GetParentExitPoint(SchematicNode parent, SchematicNode child)
        {
            // Exit on the side of the parent that faces the child. When the child sits
            // directly above or below (rare, but possible when columns coincide) leave
            // through the facing edge instead of overshooting the shape.
            bool childIsAbove = child.Position.Y > parent.Position.Y + Tolerance;
            switch (parent.NodeType)
            {
                case SchematicNodeType.Equipment:
                    if (child.Position.X > parent.Position.X + Tolerance)
                    {
                        return new XYZ(parent.Position.X + EquipmentHalfWidth, parent.Position.Y, 0);
                    }
                    if (child.Position.X < parent.Position.X - Tolerance)
                    {
                        return new XYZ(parent.Position.X - EquipmentHalfWidth, parent.Position.Y, 0);
                    }
                    return childIsAbove
                        ? new XYZ(parent.Position.X, parent.Position.Y + EquipmentHalfHeight, 0)
                        : new XYZ(parent.Position.X, parent.Position.Y - EquipmentHalfHeight, 0);

                case SchematicNodeType.Duct:
                    if (child.Position.X > parent.Position.X + Tolerance)
                    {
                        return new XYZ(parent.Position.X + DuctTickHalfWidth, parent.Position.Y, 0);
                    }
                    if (child.Position.X < parent.Position.X - Tolerance)
                    {
                        return new XYZ(parent.Position.X - DuctTickHalfWidth, parent.Position.Y, 0);
                    }
                    return childIsAbove
                        ? new XYZ(parent.Position.X, parent.Position.Y + DuctTickHalfHeight, 0)
                        : new XYZ(parent.Position.X, parent.Position.Y - DuctTickHalfHeight, 0);

                default:
                    return new XYZ(parent.Position.X, parent.Position.Y, 0);
            }
        }

        private static XYZ GetChildEntryPoint(SchematicNode child, SchematicNode parent)
        {
            // Tree layout: when the child sits on another row, the connection enters
            // from the edge facing the parent - top when the line drops from above
            // (branch take-off look), bottom when a riser arrives from below. Same-row
            // continuations enter from the left/right side, producing a straight line.
            bool parentIsAbove = parent.Position.Y > child.Position.Y + Tolerance;
            switch (child.NodeType)
            {
                case SchematicNodeType.Equipment:
                    if (Math.Abs(parent.Position.Y - child.Position.Y) < Tolerance)
                    {
                        if (parent.Position.X < child.Position.X)
                        {
                            return new XYZ(child.Position.X - EquipmentHalfWidth, child.Position.Y, 0);
                        }
                        return new XYZ(child.Position.X + EquipmentHalfWidth, child.Position.Y, 0);
                    }
                    return parentIsAbove
                        ? new XYZ(child.Position.X, child.Position.Y + EquipmentHalfHeight, 0)
                        : new XYZ(child.Position.X, child.Position.Y - EquipmentHalfHeight, 0);

                case SchematicNodeType.Duct:
                    if (Math.Abs(parent.Position.Y - child.Position.Y) < Tolerance)
                    {
                        if (parent.Position.X < child.Position.X)
                        {
                            return new XYZ(child.Position.X - DuctTickHalfWidth, child.Position.Y, 0);
                        }
                        return new XYZ(child.Position.X + DuctTickHalfWidth, child.Position.Y, 0);
                    }
                    return parentIsAbove
                        ? new XYZ(child.Position.X, child.Position.Y + DuctTickHalfHeight, 0)
                        : new XYZ(child.Position.X, child.Position.Y - DuctTickHalfHeight, 0);

                default:
                    // Air terminal: tip is the entry, already at (X, Y) pointing up.
                    return new XYZ(child.Position.X, child.Position.Y, 0);
            }
        }

        private void CreateDetailLine(ViewDrafting view, XYZ start, XYZ end, BuildResult result)
        {
            if (start == null || end == null || start.IsAlmostEqualTo(end))
            {
                return;
            }

            Line line = Line.CreateBound(start, end);
            _document.Create.NewDetailCurve(view, line);
            result.DetailCurveCount++;
        }

        private static string ComposeDuctAnnotation(SchematicNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(node.SizeLabel) && !string.IsNullOrWhiteSpace(node.FlowLabel))
            {
                return node.SizeLabel + Environment.NewLine + node.FlowLabel;
            }

            if (!string.IsNullOrWhiteSpace(node.SizeLabel))
            {
                return node.SizeLabel;
            }

            return node.FlowLabel ?? string.Empty;
        }

        private static string ComposeTerminalAnnotation(SchematicNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            // Terminal annotation carries only the flow value (rule 9). The mark/label
            // remains on the duct side if the user needs it; keeping the terminal clean
            // prevents overcrowding at the end of each branch.
            return node.FlowLabel ?? string.Empty;
        }

        private static string CreateBandKey(SchematicNode node)
        {
            if (node.IsLevelResolved && node.LevelElevation.HasValue)
            {
                return node.LevelElevation.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "|" + node.LevelName;
            }

            return "UNRESOLVED|" + (node.LevelName ?? "Unresolved Level");
        }

        private static void CreateTextNote(
            ViewDrafting view,
            ElementId textTypeId,
            XYZ position,
            string text,
            BuildResult result)
        {
            if (position == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            TextNote.Create(view.Document, view.Id, position, text, textTypeId);
            result.TextNoteCount++;
        }

        private sealed class LevelBand
        {
            public LevelBand(string label, double guideY)
            {
                Label = label ?? "Unresolved Level";
                GuideY = guideY;
            }

            public string Label { get; }
            public double GuideY { get; }
        }
    }
}
