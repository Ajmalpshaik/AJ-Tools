// ==================================================
// Tool Name    : HVAC Schematic
// Purpose      : Assigns drafting-view positions for HVAC schematic nodes and edges.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-07
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Analyzed schematic nodes and edges.
// Output       : Logical HVAC schematic layout coordinates and hierarchy metadata.
// Notes        : Uses tree-root layout bands and continuation/branch ordering for clean drafting output.
// Changelog    : v1.0.0 - Initial production-ready HVAC schematic layout engine with standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.HvacSchematic;

namespace AJTools.Services.HvacSchematic
{
    internal sealed class SchematicLayoutEngine
    {
        // Tree-root layout: one trunk row, branches drop to rows below, sub-branches
        // drop again. No stepped elbows, no fake geometry. Pure logical hierarchy.
        public const double ColumnSpacing = 10.0;
        public const double LevelBandSpacing = 34.0;
        public const double TrunkOffsetFromGuide = 8.0;
        public const double TreeRowSpacing = 5.0;
        public const double NetworkGap = 18.0;
        public const int MaxBranchRow = 8;
        public const int SiblingGapColumns = 1;

        public void Layout(IList<SchematicNode> nodes, IList<SchematicEdge> edges)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var nodeById = nodes.ToDictionary(node => AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId));
            var adjacency = BuildAdjacency(nodes, edges);
            var bandIndices = BuildGlobalBandIndices(nodes);

            foreach (SchematicEdge edge in edges)
            {
                edge.IsTreeEdge = false;
            }

            double componentStartX = 0;
            IEnumerable<int> networkIndices = nodes
                .Select(node => node.NetworkIndex)
                .Distinct()
                .OrderBy(index => index);

            foreach (int networkIndex in networkIndices)
            {
                List<SchematicNode> networkNodes = nodes
                    .Where(node => node.NetworkIndex == networkIndex)
                    .OrderBy(node => AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId))
                    .ToList();
                if (networkNodes.Count == 0)
                {
                    continue;
                }

                ResetNetworkState(networkNodes);

                SchematicNode root = ChooseRoot(networkNodes, adjacency, edges);
                root.IsRoot = true;
                root.ParentElementId = ElementId.InvalidElementId;

                Dictionary<int, int> parentById = BuildDirectedTree(root, adjacency, nodeById, edges);
                Dictionary<int, List<int>> childrenByParent = BuildChildrenMap(networkNodes, parentById);
                AssignDepths(AJTools.Utils.ElementIdHelper.GetIntegerValue(root.ElementId), 0, childrenByParent, nodeById);

                Dictionary<int, int> subtreeScores = new Dictionary<int, int>();
                CalculateSubtreeScore(AJTools.Utils.ElementIdHelper.GetIntegerValue(root.ElementId), childrenByParent, nodeById, subtreeScores);

                Dictionary<int, int> leafCounts = new Dictionary<int, int>();
                CalculateLeafCount(AJTools.Utils.ElementIdHelper.GetIntegerValue(root.ElementId), childrenByParent, leafCounts);

                Dictionary<string, SchematicEdge> edgeByKey = BuildEdgeLookup(edges);
                Dictionary<int, int> continuationChildByParent = new Dictionary<int, int>();
                foreach (KeyValuePair<int, List<int>> pair in childrenByParent)
                {
                    int continuationChildId = ResolveContinuationChild(
                        pair.Key,
                        pair.Value,
                        nodeById,
                        subtreeScores,
                        leafCounts,
                        edgeByKey);
                    if (continuationChildId != AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId))
                    {
                        continuationChildByParent[pair.Key] = continuationChildId;
                    }
                }

                foreach (KeyValuePair<int, List<int>> pair in childrenByParent.ToList())
                {
                    pair.Value.Sort((left, right) => CompareChildOrder(
                        nodeById[pair.Key],
                        nodeById[left],
                        nodeById[right],
                        continuationChildByParent,
                        subtreeScores,
                        leafCounts,
                        edgeByKey));
                }

                foreach (KeyValuePair<int, int> pair in parentById)
                {
                    nodeById[pair.Key].ParentElementId = pair.Value == AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId)
                        ? ElementId.InvalidElementId
                        : new ElementId(pair.Value);
                }

                int maxColumn = AssignTreePositions(
                    AJTools.Utils.ElementIdHelper.GetIntegerValue(root.ElementId),
                    0,
                    0,
                    childrenByParent,
                    nodeById,
                    continuationChildByParent);

                for (int i = 0; i < networkNodes.Count; i++)
                {
                    SchematicNode node = networkNodes[i];
                    int bandIndex = bandIndices[CreateLevelBandKey(node)];
                    double guideY = 0 - (bandIndex * LevelBandSpacing);
                    double trunkY = guideY + TrunkOffsetFromGuide;
                    node.GuideY = guideY;
                    node.TrunkY = trunkY;
                    double x = componentStartX + (node.ColumnIndex * ColumnSpacing);
                    node.Position = new XYZ(x, GetNodeY(node, trunkY), 0);
                }

                componentStartX += ((maxColumn + 1) * ColumnSpacing) + NetworkGap;
            }
        }

        private static Dictionary<int, int> BuildDirectedTree(
            SchematicNode root,
            IDictionary<int, List<int>> adjacency,
            IDictionary<int, SchematicNode> nodeById,
            IList<SchematicEdge> edges)
        {
            Dictionary<string, SchematicEdge> edgeByKey = BuildEdgeLookup(edges);
            Dictionary<int, int> parentById = new Dictionary<int, int>();
            parentById[AJTools.Utils.ElementIdHelper.GetIntegerValue(root.ElementId)] = AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId);
            Dictionary<int, double> costById = new Dictionary<int, double>();
            costById[AJTools.Utils.ElementIdHelper.GetIntegerValue(root.ElementId)] = 0;
            var settled = new HashSet<int>();

            while (settled.Count < adjacency.Count)
            {
                int currentId = SelectLowestCostNode(costById, settled);
                if (currentId == AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId))
                {
                    break;
                }

                settled.Add(currentId);

                List<int> neighbors = adjacency[currentId];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighborId = neighbors[i];
                    if (settled.Contains(neighborId))
                    {
                        continue;
                    }

                    SchematicEdge edge = GetEdge(edgeByKey, currentId, neighborId);
                    double candidateCost = costById[currentId] + GetTraversalCost(nodeById[currentId], nodeById[neighborId], edge);

                    double existingCost;
                    if (!costById.TryGetValue(neighborId, out existingCost) ||
                        candidateCost < existingCost - 1e-6 ||
                        (Math.Abs(candidateCost - existingCost) < 1e-6 &&
                         IsBetterParent(currentId, parentById.ContainsKey(neighborId) ? parentById[neighborId] : AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId), nodeById, edgeByKey, neighborId)))
                    {
                        costById[neighborId] = candidateCost;
                        parentById[neighborId] = currentId;
                    }
                }
            }

            foreach (KeyValuePair<int, int> pair in parentById)
            {
                if (pair.Value == AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId))
                {
                    continue;
                }

                MarkTreeEdge(edges, pair.Key, pair.Value);
            }

            return parentById;
        }

        private static Dictionary<int, List<int>> BuildChildrenMap(
            IList<SchematicNode> networkNodes,
            Dictionary<int, int> parentById)
        {
            Dictionary<int, List<int>> childrenByParent = new Dictionary<int, List<int>>();
            foreach (SchematicNode node in networkNodes)
            {
                childrenByParent[AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId)] = new List<int>();
            }

            foreach (KeyValuePair<int, int> pair in parentById)
            {
                if (pair.Value == AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId))
                {
                    continue;
                }

                childrenByParent[pair.Value].Add(pair.Key);
            }

            return childrenByParent;
        }

        private static Dictionary<int, List<int>> BuildAdjacency(IList<SchematicNode> nodes, IList<SchematicEdge> edges)
        {
            Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
            foreach (SchematicNode node in nodes)
            {
                adjacency[AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId)] = new List<int>();
            }

            foreach (SchematicEdge edge in edges)
            {
                int fromId = AJTools.Utils.ElementIdHelper.GetIntegerValue(edge.FromElementId);
                int toId = AJTools.Utils.ElementIdHelper.GetIntegerValue(edge.ToElementId);

                List<int> fromNeighbors;
                if (adjacency.TryGetValue(fromId, out fromNeighbors) && !fromNeighbors.Contains(toId))
                {
                    fromNeighbors.Add(toId);
                }

                List<int> toNeighbors;
                if (adjacency.TryGetValue(toId, out toNeighbors) && !toNeighbors.Contains(fromId))
                {
                    toNeighbors.Add(fromId);
                }
            }

            return adjacency;
        }

        private static Dictionary<string, int> BuildGlobalBandIndices(IList<SchematicNode> nodes)
        {
            return nodes
                .GroupBy(CreateLevelBandKey)
                .Select(group =>
                {
                    SchematicNode anchor = group.First();
                    return new
                    {
                        Key = group.Key,
                        Label = anchor.LevelName ?? "Unresolved Level",
                        Elevation = anchor.LevelElevation,
                        IsResolved = anchor.IsLevelResolved
                    };
                })
                .OrderByDescending(item => item.IsResolved)
                .ThenByDescending(item => item.Elevation ?? double.MinValue)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .Select((item, index) => new { item.Key, Index = index })
                .ToDictionary(item => item.Key, item => item.Index, StringComparer.Ordinal);
        }

        private static void ResetNetworkState(IList<SchematicNode> networkNodes)
        {
            for (int i = 0; i < networkNodes.Count; i++)
            {
                SchematicNode node = networkNodes[i];
                node.IsRoot = false;
                node.Depth = int.MaxValue;
                node.ColumnIndex = int.MaxValue;
                node.ParentElementId = ElementId.InvalidElementId;
                node.BranchTier = 0;
                node.IsTrunk = false;
                node.GuideY = 0;
                node.TrunkY = 0;
                node.Position = new XYZ();
            }
        }

        private static SchematicNode ChooseRoot(
            IList<SchematicNode> networkNodes,
            IDictionary<int, List<int>> adjacency,
            IList<SchematicEdge> edges)
        {
            Dictionary<string, SchematicEdge> edgeByKey = BuildEdgeLookup(edges);
            return networkNodes
                .OrderBy(node => GetRootOrder(node))
                .ThenBy(node => GetIncomingPreference(node, adjacency, edgeByKey))
                .ThenByDescending(node => GetOutgoingPreference(node, adjacency, edgeByKey))
                .ThenByDescending(node => adjacency[AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId)].Count)
                .ThenByDescending(node => node.LevelElevation ?? double.MinValue)
                .ThenBy(node => AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId))
                .First();
        }

        private static int GetRootOrder(SchematicNode node)
        {
            if (node.IsPrimaryEquipment)
            {
                return 0;
            }

            switch (node.NodeType)
            {
                case SchematicNodeType.Equipment:
                    return 1;
                case SchematicNodeType.Duct:
                    return 2;
                default:
                    return 3;
            }
        }

        private static void AssignDepths(
            int nodeId,
            int depth,
            IDictionary<int, List<int>> childrenByParent,
            IDictionary<int, SchematicNode> nodeById)
        {
            SchematicNode node = nodeById[nodeId];
            node.Depth = depth;

            List<int> children = childrenByParent[nodeId];
            for (int i = 0; i < children.Count; i++)
            {
                AssignDepths(children[i], depth + 1, childrenByParent, nodeById);
            }
        }

        private static int AssignTreePositions(
            int nodeId,
            int row,
            int startColumn,
            IDictionary<int, List<int>> childrenByParent,
            IDictionary<int, SchematicNode> nodeById,
            IDictionary<int, int> continuationChildByParent)
        {
            SchematicNode node = nodeById[nodeId];
            int clampedRow = Math.Max(0, Math.Min(row, MaxBranchRow));
            node.BranchTier = clampedRow;
            node.IsTrunk = clampedRow == 0;
            node.ColumnIndex = Math.Max(0, startColumn);

            List<int> children = childrenByParent[nodeId];
            int continuationChildId = AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId);
            continuationChildByParent.TryGetValue(nodeId, out continuationChildId);
            int nextColumn = node.ColumnIndex + 1;
            int maxColumn = node.ColumnIndex;

            for (int i = 0; i < children.Count; i++)
            {
                int childId = children[i];
                if (childId == continuationChildId)
                {
                    continue;
                }

                SchematicNode child = nodeById[childId];
                int childRow = ResolveChildRow(node, child, childId == continuationChildId, clampedRow);
                int childMaxColumn = AssignTreePositions(
                    childId,
                    childRow,
                    nextColumn,
                    childrenByParent,
                    nodeById,
                    continuationChildByParent);
                maxColumn = Math.Max(maxColumn, childMaxColumn);
                nextColumn = childMaxColumn + 1 + SiblingGapColumns;
            }

            if (continuationChildId != AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId))
            {
                SchematicNode continuationChild = nodeById[continuationChildId];
                int continuationRow = ResolveChildRow(node, continuationChild, true, clampedRow);
                int continuationMaxColumn = AssignTreePositions(
                    continuationChildId,
                    continuationRow,
                    nextColumn,
                    childrenByParent,
                    nodeById,
                    continuationChildByParent);
                maxColumn = Math.Max(maxColumn, continuationMaxColumn);
            }

            return maxColumn;
        }

        private static int CalculateSubtreeScore(
            int nodeId,
            IDictionary<int, List<int>> childrenByParent,
            IDictionary<int, SchematicNode> nodeById,
            IDictionary<int, int> subtreeScores)
        {
            SchematicNode node = nodeById[nodeId];
            int score = GetNodeScore(node);

            List<int> children = childrenByParent[nodeId];
            for (int i = 0; i < children.Count; i++)
            {
                score += CalculateSubtreeScore(children[i], childrenByParent, nodeById, subtreeScores);
            }

            subtreeScores[nodeId] = score;
            return score;
        }

        private static int CalculateLeafCount(
            int nodeId,
            IDictionary<int, List<int>> childrenByParent,
            IDictionary<int, int> leafCounts)
        {
            List<int> children = childrenByParent[nodeId];
            if (children.Count == 0)
            {
                leafCounts[nodeId] = 1;
                return 1;
            }

            int count = 0;
            for (int i = 0; i < children.Count; i++)
            {
                count += CalculateLeafCount(children[i], childrenByParent, leafCounts);
            }

            leafCounts[nodeId] = Math.Max(1, count);
            return leafCounts[nodeId];
        }

        private static int GetNodeScore(SchematicNode node)
        {
            switch (node.NodeType)
            {
                case SchematicNodeType.Equipment:
                    return 1000;
                case SchematicNodeType.Duct:
                    return 100;
                default:
                    return 1;
            }
        }

        private static int ResolveContinuationChild(
            int parentId,
            IList<int> children,
            IDictionary<int, SchematicNode> nodeById,
            IDictionary<int, int> subtreeScores,
            IDictionary<int, int> leafCounts,
            IDictionary<string, SchematicEdge> edgeByKey)
        {
            int bestChildId = AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId);
            double bestScore = double.MinValue;
            SchematicNode parent = nodeById[parentId];

            for (int i = 0; i < children.Count; i++)
            {
                int childId = children[i];
                SchematicNode child = nodeById[childId];
                if (child.NodeType != SchematicNodeType.Duct)
                {
                    continue;
                }

                double score = subtreeScores[childId] * 10.0;
                int leafCount;
                if (leafCounts.TryGetValue(childId, out leafCount))
                {
                    score += leafCount * 20.0;
                }

                if (IsLevelTransition(parent, child))
                {
                    score -= 30.0;
                }
                else
                {
                    score += 150.0;
                }

                SchematicEdge edge = GetEdge(edgeByKey, parentId, childId);
                if (edge != null)
                {
                    score += edge.GetHierarchyPreference(parentId, childId) * 12.0;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestChildId = childId;
                }
            }

            return bestChildId;
        }

        private static void MarkTreeEdge(IList<SchematicEdge> edges, int firstId, int secondId)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Connects(new ElementId(firstId), new ElementId(secondId)))
                {
                    edges[i].IsTreeEdge = true;
                    return;
                }
            }
        }

        private static int ResolveChildRow(
            SchematicNode parent,
            SchematicNode child,
            bool isContinuation,
            int parentRow)
        {
            if (IsLevelTransition(parent, child))
            {
                return 0;
            }

            return isContinuation && child.NodeType == SchematicNodeType.Duct
                ? parentRow
                : parentRow + 1;
        }

        private static int CompareChildOrder(
            SchematicNode parent,
            SchematicNode left,
            SchematicNode right,
            IDictionary<int, int> continuationChildByParent,
            IDictionary<int, int> subtreeScores,
            IDictionary<int, int> leafCounts,
            IDictionary<string, SchematicEdge> edgeByKey)
        {
            int leftOrder = GetChildOrder(parent, left, continuationChildByParent);
            int rightOrder = GetChildOrder(parent, right, continuationChildByParent);
            if (leftOrder != rightOrder)
            {
                return leftOrder.CompareTo(rightOrder);
            }

            int leftLeafCount = leafCounts.ContainsKey(AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId)) ? leafCounts[AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId)] : 1;
            int rightLeafCount = leafCounts.ContainsKey(AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId)) ? leafCounts[AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId)] : 1;
            int leafComparison = rightLeafCount.CompareTo(leftLeafCount);
            if (leafComparison != 0)
            {
                return leafComparison;
            }

            int leftScore = subtreeScores.ContainsKey(AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId)) ? subtreeScores[AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId)] : 0;
            int rightScore = subtreeScores.ContainsKey(AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId)) ? subtreeScores[AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId)] : 0;
            int scoreComparison = rightScore.CompareTo(leftScore);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            SchematicEdge leftEdge = GetEdge(edgeByKey, AJTools.Utils.ElementIdHelper.GetIntegerValue(parent.ElementId), AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId));
            SchematicEdge rightEdge = GetEdge(edgeByKey, AJTools.Utils.ElementIdHelper.GetIntegerValue(parent.ElementId), AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId));
            int leftDirection = leftEdge != null ? leftEdge.GetHierarchyPreference(AJTools.Utils.ElementIdHelper.GetIntegerValue(parent.ElementId), AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId)) : 0;
            int rightDirection = rightEdge != null ? rightEdge.GetHierarchyPreference(AJTools.Utils.ElementIdHelper.GetIntegerValue(parent.ElementId), AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId)) : 0;
            int directionComparison = rightDirection.CompareTo(leftDirection);
            if (directionComparison != 0)
            {
                return directionComparison;
            }

            int levelComparison = (right.LevelElevation ?? double.MinValue).CompareTo(left.LevelElevation ?? double.MinValue);
            if (levelComparison != 0)
            {
                return levelComparison;
            }

            int labelComparison = string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
            if (labelComparison != 0)
            {
                return labelComparison;
            }

            return AJTools.Utils.ElementIdHelper.GetIntegerValue(left.ElementId).CompareTo(AJTools.Utils.ElementIdHelper.GetIntegerValue(right.ElementId));
        }

        private static int GetChildOrder(
            SchematicNode parent,
            SchematicNode child,
            IDictionary<int, int> continuationChildByParent)
        {
            int continuationChildId;
            bool isContinuation = continuationChildByParent.TryGetValue(AJTools.Utils.ElementIdHelper.GetIntegerValue(parent.ElementId), out continuationChildId) &&
                                  continuationChildId == AJTools.Utils.ElementIdHelper.GetIntegerValue(child.ElementId);
            bool crossesLevel = IsLevelTransition(parent, child);

            if (isContinuation)
            {
                return crossesLevel ? 5 : 4;
            }

            if (child.NodeType == SchematicNodeType.Duct)
            {
                return crossesLevel ? 2 : 0;
            }

            if (child.NodeType == SchematicNodeType.AirTerminal)
            {
                return crossesLevel ? 3 : 1;
            }

            return crossesLevel ? 6 : 3;
        }

        private static Dictionary<string, SchematicEdge> BuildEdgeLookup(IList<SchematicEdge> edges)
        {
            Dictionary<string, SchematicEdge> edgeByKey = new Dictionary<string, SchematicEdge>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                SchematicEdge edge = edges[i];
                edgeByKey[CreateEdgeKey(AJTools.Utils.ElementIdHelper.GetIntegerValue(edge.FromElementId), AJTools.Utils.ElementIdHelper.GetIntegerValue(edge.ToElementId))] = edge;
            }

            return edgeByKey;
        }

        private static SchematicEdge GetEdge(IDictionary<string, SchematicEdge> edgeByKey, int firstId, int secondId)
        {
            if (edgeByKey == null)
            {
                return null;
            }

            SchematicEdge edge;
            edgeByKey.TryGetValue(CreateEdgeKey(firstId, secondId), out edge);
            return edge;
        }

        private static int SelectLowestCostNode(IDictionary<int, double> costById, ISet<int> settled)
        {
            double bestCost = double.MaxValue;
            int bestNodeId = AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId);

            foreach (KeyValuePair<int, double> pair in costById)
            {
                if (settled.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value < bestCost - 1e-6 ||
                    (Math.Abs(pair.Value - bestCost) < 1e-6 && pair.Key < bestNodeId))
                {
                    bestCost = pair.Value;
                    bestNodeId = pair.Key;
                }
            }

            return bestNodeId;
        }

        private static double GetTraversalCost(SchematicNode parent, SchematicNode child, SchematicEdge edge)
        {
            double cost = 100.0;

            if (parent.NodeType == SchematicNodeType.AirTerminal)
            {
                cost += 500.0;
            }

            if (parent.NodeType == SchematicNodeType.Equipment && child.NodeType == SchematicNodeType.Duct)
            {
                cost -= 25.0;
            }

            if (parent.NodeType == SchematicNodeType.Duct && child.NodeType == SchematicNodeType.Duct)
            {
                cost -= 12.0;
            }

            if (child.NodeType == SchematicNodeType.AirTerminal)
            {
                cost += 18.0;
            }

            if (edge != null)
            {
                int directionPreference = edge.GetHierarchyPreference(AJTools.Utils.ElementIdHelper.GetIntegerValue(parent.ElementId), AJTools.Utils.ElementIdHelper.GetIntegerValue(child.ElementId));
                if (directionPreference > 0)
                {
                    cost -= 35.0 + Math.Min(20.0, directionPreference * 4.0);
                }
                else if (directionPreference < 0)
                {
                    cost += 60.0;
                }

                if (edge.IsLevelTransition)
                {
                    cost += 10.0;
                }
            }

            return Math.Max(1.0, cost);
        }

        private static bool IsBetterParent(
            int candidateParentId,
            int currentParentId,
            IDictionary<int, SchematicNode> nodeById,
            IDictionary<string, SchematicEdge> edgeByKey,
            int childId)
        {
            if (currentParentId == AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId))
            {
                return true;
            }

            SchematicNode candidateParent = nodeById[candidateParentId];
            SchematicNode currentParent = nodeById[currentParentId];

            SchematicEdge candidateEdge = GetEdge(edgeByKey, candidateParentId, childId);
            SchematicEdge currentEdge = GetEdge(edgeByKey, currentParentId, childId);
            int candidateDirection = candidateEdge != null
                ? candidateEdge.GetHierarchyPreference(candidateParentId, childId)
                : 0;
            int currentDirection = currentEdge != null
                ? currentEdge.GetHierarchyPreference(currentParentId, childId)
                : 0;

            if (candidateDirection != currentDirection)
            {
                return candidateDirection > currentDirection;
            }

            int candidateOrder = GetRootOrder(candidateParent);
            int currentOrder = GetRootOrder(currentParent);
            if (candidateOrder != currentOrder)
            {
                return candidateOrder < currentOrder;
            }

            return AJTools.Utils.ElementIdHelper.GetIntegerValue(candidateParent.ElementId) < AJTools.Utils.ElementIdHelper.GetIntegerValue(currentParent.ElementId);
        }

        private static int GetIncomingPreference(
            SchematicNode node,
            IDictionary<int, List<int>> adjacency,
            IDictionary<string, SchematicEdge> edgeByKey)
        {
            int score = 0;
            List<int> neighbors = adjacency[AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId)];
            for (int i = 0; i < neighbors.Count; i++)
            {
                SchematicEdge edge = GetEdge(edgeByKey, AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId), neighbors[i]);
                if (edge == null)
                {
                    continue;
                }

                int preference = edge.GetHierarchyPreference(AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId), neighbors[i]);
                if (preference < 0)
                {
                    score += -preference;
                }
            }

            return score;
        }

        private static int GetOutgoingPreference(
            SchematicNode node,
            IDictionary<int, List<int>> adjacency,
            IDictionary<string, SchematicEdge> edgeByKey)
        {
            int score = 0;
            List<int> neighbors = adjacency[AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId)];
            for (int i = 0; i < neighbors.Count; i++)
            {
                SchematicEdge edge = GetEdge(edgeByKey, AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId), neighbors[i]);
                if (edge == null)
                {
                    continue;
                }

                int preference = edge.GetHierarchyPreference(AJTools.Utils.ElementIdHelper.GetIntegerValue(node.ElementId), neighbors[i]);
                if (preference > 0)
                {
                    score += preference;
                }
            }

            return score;
        }

        private static bool IsLevelTransition(SchematicNode first, SchematicNode second)
        {
            return !CreateLevelBandKey(first).Equals(CreateLevelBandKey(second), StringComparison.Ordinal);
        }

        private static double GetNodeY(SchematicNode node, double trunkY)
        {
            int row = Math.Max(0, Math.Min(node.BranchTier, MaxBranchRow));
            return trunkY - (row * TreeRowSpacing);
        }

        private static string CreateLevelBandKey(SchematicNode node)
        {
            if (node.IsLevelResolved && node.LevelElevation.HasValue)
            {
                return node.LevelElevation.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "|" + node.LevelName;
            }

            return "UNRESOLVED|" + (node.LevelName ?? "Unresolved Level");
        }

        private static string CreateEdgeKey(int firstId, int secondId)
        {
            if (firstId < secondId)
            {
                return firstId + ":" + secondId;
            }

            return secondId + ":" + firstId;
        }
    }
}
