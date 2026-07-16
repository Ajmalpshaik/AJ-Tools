// ==================================================
// Tool Name    : HVAC Schematic
// Purpose      : Builds HVAC schematic nodes, edges, and network metadata from model selections.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-07
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Selected HVAC element ids, connector data, and resolved levels.
// Output       : Analyzed HVAC networks for schematic layout and drafting-view generation.
// Notes        : Collects supported elements, connector relationships, and unresolved-analysis reports.
// Changelog    : v1.0.0 - Initial production-ready HVAC schematic network analysis with standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using AJTools.Models.HvacSchematic;
using AJTools.Utils;

namespace AJTools.Services.HvacSchematic
{
    internal sealed class NetworkAnalysisService
    {
        internal sealed class AnalysisResult
        {
            public AnalysisResult()
            {
                Nodes = new List<SchematicNode>();
                Edges = new List<SchematicEdge>();
                RejectedSelections = new List<string>();
                MissingConnectorData = new List<string>();
                UnresolvedLevels = new List<string>();
                UnresolvedConnections = new List<string>();
            }

            public IList<SchematicNode> Nodes { get; }
            public IList<SchematicEdge> Edges { get; }
            public IList<string> RejectedSelections { get; }
            public IList<string> MissingConnectorData { get; }
            public IList<string> UnresolvedLevels { get; }
            public IList<string> UnresolvedConnections { get; }
            public int NetworkCount { get; set; }
        }

        private readonly Document _document;
        private readonly LevelResolverService _levelResolver;

        public NetworkAnalysisService(Document document, LevelResolverService levelResolver)
        {
            _document = document;
            _levelResolver = levelResolver;
        }

        public AnalysisResult Analyze(ICollection<ElementId> selectedIds)
        {
            AnalysisResult result = new AnalysisResult();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return result;
            }

            var supportedElementsById = new Dictionary<int, Element>();
            var nodesById = new Dictionary<int, SchematicNode>();

            foreach (ElementId selectedId in selectedIds)
            {
                Element element = _document.GetElement(selectedId);
                if (element == null)
                {
                    continue;
                }

                string rejectionReason;
                SchematicNode node;
                if (!TryCreateNode(element, out node, out rejectionReason))
                {
                    result.RejectedSelections.Add(DescribeElement(element) + " - " + rejectionReason);
                    continue;
                }

                AddNodeIfMissing(element, node, supportedElementsById, nodesById, result);
            }

            if (nodesById.Count == 0)
            {
                return result;
            }

            var edgeAccumulators = new Dictionary<string, EdgeAccumulator>(StringComparer.Ordinal);
            var supportedQueue = new Queue<Element>(supportedElementsById.Values.OrderBy(element => element.Id.IntValue()));
            var exploredSupportedIds = new HashSet<int>();

            while (supportedQueue.Count > 0)
            {
                Element currentElement = supportedQueue.Dequeue();
                if (!exploredSupportedIds.Add(currentElement.Id.IntValue()))
                {
                    continue;
                }

                if (!nodesById[currentElement.Id.IntValue()].HasConnectorData)
                {
                    continue;
                }

                CollectNeighborEdges(currentElement, supportedElementsById, nodesById, supportedQueue, edgeAccumulators, result);
            }

            foreach (EdgeAccumulator accumulator in edgeAccumulators.Values
                .OrderBy(item => item.FirstElementId)
                .ThenBy(item => item.SecondElementId))
            {
                SchematicEdge edge = accumulator.ToEdge();
                result.Edges.Add(edge);

                if (!edge.HasDirectionHint)
                {
                    result.UnresolvedConnections.Add(BuildConnectionDescription(edge, nodesById));
                }
            }

            result.Nodes.Clear();
            foreach (SchematicNode node in nodesById.Values.OrderBy(node => node.ElementId.IntValue()))
            {
                result.Nodes.Add(node);
            }

            AssignNetworks(result.Nodes, result.Edges);
            UpdateEdgeMetadata(result.Nodes, result.Edges);

            result.NetworkCount = result.Nodes
                .Select(node => node.NetworkIndex)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            return result;
        }

        private void CollectNeighborEdges(
            Element startElement,
            IDictionary<int, Element> supportedElementsById,
            IDictionary<int, SchematicNode> nodesById,
            Queue<Element> supportedQueue,
            IDictionary<string, EdgeAccumulator> edgeAccumulators,
            AnalysisResult result)
        {
            IList<Connector> startConnectors = GetUsableConnectors(startElement);
            for (int startConnectorIndex = 0; startConnectorIndex < startConnectors.Count; startConnectorIndex++)
            {
                Connector startConnector = startConnectors[startConnectorIndex];
                var queue = new Queue<TraversalState>();
                var visitedOwnerIds = new HashSet<int> { startElement.Id.IntValue() };

                queue.Enqueue(new TraversalState(startElement, startConnector, 0));

                while (queue.Count > 0)
                {
                    TraversalState state = queue.Dequeue();
                    IList<Connector> connectors = GetTraversalConnectors(startElement, state);
                    for (int connectorIndex = 0; connectorIndex < connectors.Count; connectorIndex++)
                    {
                        Connector connector = connectors[connectorIndex];
                        foreach (Connector referenceConnector in connector.AllRefs)
                        {
                            if (referenceConnector == null || !referenceConnector.IsValidObject)
                            {
                                continue;
                            }

                            Element owner = referenceConnector.Owner;
                            if (owner == null || owner.Id.IntValue() == state.CurrentElement.Id.IntValue())
                            {
                                continue;
                            }

                            int ownerId = owner.Id.IntValue();
                            if (TryResolveNodeType(owner, out SchematicNodeType _))
                            {
                                if (!supportedElementsById.ContainsKey(ownerId))
                                {
                                    string rejectionReason;
                                    SchematicNode node;
                                    if (TryCreateNode(owner, out node, out rejectionReason))
                                    {
                                        AddNodeIfMissing(owner, node, supportedElementsById, nodesById, result);
                                        supportedQueue.Enqueue(owner);
                                    }
                                }

                                if (ownerId != startElement.Id.IntValue())
                                {
                                    AddEdgeEvidence(
                                        startElement.Id.IntValue(),
                                        ownerId,
                                        state.SourceConnector,
                                        referenceConnector,
                                        state.HopCount + 1,
                                        nodesById,
                                        edgeAccumulators);
                                }

                                continue;
                            }

                            if (visitedOwnerIds.Add(ownerId) && GetUsableConnectors(owner).Count > 0)
                            {
                                queue.Enqueue(new TraversalState(owner, state.SourceConnector, state.HopCount + 1));
                            }
                        }
                    }
                }
            }
        }

        private static void AssignNetworks(IList<SchematicNode> nodes, IList<SchematicEdge> edges)
        {
            var adjacency = new Dictionary<int, List<int>>();
            foreach (SchematicNode node in nodes)
            {
                adjacency[node.ElementId.IntValue()] = new List<int>();
            }

            foreach (SchematicEdge edge in edges)
            {
                int fromId = edge.FromElementId.IntValue();
                int toId = edge.ToElementId.IntValue();

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

            var nodeById = nodes.ToDictionary(node => node.ElementId.IntValue());
            var visited = new HashSet<int>();
            int networkIndex = 0;

            foreach (SchematicNode node in nodes.OrderBy(GetNetworkOrder).ThenBy(n => n.ElementId.IntValue()))
            {
                int nodeId = node.ElementId.IntValue();
                if (!visited.Add(nodeId))
                {
                    continue;
                }

                var queue = new Queue<int>();
                queue.Enqueue(nodeId);

                while (queue.Count > 0)
                {
                    int currentId = queue.Dequeue();
                    nodeById[currentId].NetworkIndex = networkIndex;

                    List<int> neighbors = adjacency[currentId];
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        int neighborId = neighbors[i];
                        if (visited.Add(neighborId))
                        {
                            queue.Enqueue(neighborId);
                        }
                    }
                }

                networkIndex++;
            }

            foreach (SchematicEdge edge in edges)
            {
                SchematicNode sourceNode;
                if (nodeById.TryGetValue(edge.FromElementId.IntValue(), out sourceNode))
                {
                    edge.NetworkIndex = sourceNode.NetworkIndex;
                }
            }
        }

        private static int GetNetworkOrder(SchematicNode node)
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

        private static void UpdateEdgeMetadata(IList<SchematicNode> nodes, IList<SchematicEdge> edges)
        {
            var nodeById = nodes.ToDictionary(node => node.ElementId.IntValue());
            foreach (SchematicEdge edge in edges)
            {
                SchematicNode fromNode;
                SchematicNode toNode;
                if (!nodeById.TryGetValue(edge.FromElementId.IntValue(), out fromNode) ||
                    !nodeById.TryGetValue(edge.ToElementId.IntValue(), out toNode))
                {
                    edge.IsLevelTransition = false;
                    continue;
                }

                edge.IsLevelTransition = !CreateLevelKey(fromNode).Equals(CreateLevelKey(toNode), StringComparison.Ordinal);
            }
        }

        private static IList<Connector> GetTraversalConnectors(Element startElement, TraversalState state)
        {
            if (startElement == null || state == null || state.CurrentElement == null)
            {
                return new List<Connector>();
            }

            if (state.CurrentElement.Id.IntValue() == startElement.Id.IntValue())
            {
                return new List<Connector> { state.SourceConnector };
            }

            return GetUsableConnectors(state.CurrentElement);
        }

        private static void AddEdgeEvidence(
            int sourceElementId,
            int targetElementId,
            Connector sourceConnector,
            Connector targetConnector,
            int hopCount,
            IDictionary<int, SchematicNode> nodesById,
            IDictionary<string, EdgeAccumulator> edgeAccumulators)
        {
            string edgeKey = CreateEdgeKey(sourceElementId, targetElementId);
            EdgeAccumulator accumulator;
            if (!edgeAccumulators.TryGetValue(edgeKey, out accumulator))
            {
                accumulator = new EdgeAccumulator(sourceElementId, targetElementId);
                edgeAccumulators[edgeKey] = accumulator;
            }

            SchematicNode sourceNode;
            SchematicNode targetNode;
            nodesById.TryGetValue(sourceElementId, out sourceNode);
            nodesById.TryGetValue(targetElementId, out targetNode);

            accumulator.AddEvidence(
                sourceElementId,
                targetElementId,
                sourceNode != null ? sourceNode.NodeType : SchematicNodeType.Duct,
                targetNode != null ? targetNode.NodeType : SchematicNodeType.Duct,
                sourceConnector,
                targetConnector,
                hopCount);
        }

        private void AddNodeIfMissing(
            Element element,
            SchematicNode node,
            IDictionary<int, Element> supportedElementsById,
            IDictionary<int, SchematicNode> nodesById,
            AnalysisResult result)
        {
            int key = element.Id.IntValue();
            if (supportedElementsById.ContainsKey(key))
            {
                return;
            }

            supportedElementsById[key] = element;
            nodesById[key] = node;

            if (!node.IsLevelResolved)
            {
                result.UnresolvedLevels.Add(DescribeElement(element));
            }

            if (!node.HasConnectorData)
            {
                result.MissingConnectorData.Add(DescribeElement(element));
            }
        }

        private bool TryCreateNode(Element element, out SchematicNode node, out string rejectionReason)
        {
            node = null;
            rejectionReason = string.Empty;

            SchematicNodeType nodeType;
            if (!TryResolveNodeType(element, out nodeType))
            {
                rejectionReason = "Only Duct, Air Terminal, and Mechanical Equipment are supported.";
                return false;
            }

            LevelResolverService.LevelResolution levelResolution = _levelResolver.Resolve(element);
            node = new SchematicNode(element.Id, nodeType)
            {
                Label = BuildNodeLabel(element, nodeType),
                SizeLabel = nodeType == SchematicNodeType.Duct ? GetDuctSizeLabel(element) : string.Empty,
                FlowLabel = GetFlowLabel(element, nodeType),
                LevelName = levelResolution.Label,
                LevelElevation = levelResolution.Elevation,
                IsLevelResolved = levelResolution.IsResolved,
                HasConnectorData = GetUsableConnectors(element).Count > 0,
                IsPrimaryEquipment = nodeType == SchematicNodeType.Equipment && IsPrimaryEquipment(element as FamilyInstance)
            };

            return true;
        }

        private static bool TryResolveNodeType(Element element, out SchematicNodeType nodeType)
        {
            nodeType = SchematicNodeType.Duct;

            if (element is Duct)
            {
                nodeType = SchematicNodeType.Duct;
                return true;
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance == null || familyInstance.Category == null)
            {
                return false;
            }

            BuiltInCategory category = (BuiltInCategory)familyInstance.Category.Id.IntValue();
            if (category == BuiltInCategory.OST_DuctTerminal)
            {
                nodeType = SchematicNodeType.AirTerminal;
                return true;
            }

            if (category == BuiltInCategory.OST_MechanicalEquipment)
            {
                nodeType = SchematicNodeType.Equipment;
                return true;
            }

            return false;
        }

        private static IList<Connector> GetUsableConnectors(Element element)
        {
            var connectors = new List<Connector>();
            ConnectorManager connectorManager = GetConnectorManager(element);
            if (connectorManager == null)
            {
                return connectors;
            }

            foreach (Connector connector in connectorManager.Connectors)
            {
                if (connector == null || !connector.IsValidObject)
                {
                    continue;
                }

                if (connector.ConnectorType == ConnectorType.End)
                {
                    connectors.Add(connector);
                }
            }

            return connectors;
        }

        private static ConnectorManager GetConnectorManager(Element element)
        {
            MEPCurve mepCurve = element as MEPCurve;
            if (mepCurve != null)
            {
                return mepCurve.ConnectorManager;
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance?.MEPModel != null)
            {
                return familyInstance.MEPModel.ConnectorManager;
            }

            return null;
        }

        private static string BuildNodeLabel(Element element, SchematicNodeType nodeType)
        {
            switch (nodeType)
            {
                case SchematicNodeType.Equipment:
                    return GetEquipmentLabel(element as FamilyInstance);
                case SchematicNodeType.AirTerminal:
                    return GetAirTerminalLabel(element);
                default:
                    return "Duct";
            }
        }

        private static string GetEquipmentLabel(FamilyInstance familyInstance)
        {
            if (familyInstance == null)
            {
                return "Mechanical Equipment";
            }

            string familyName = familyInstance.Symbol != null ? familyInstance.Symbol.FamilyName : string.Empty;
            string typeName = familyInstance.Name ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(familyName) &&
                !string.IsNullOrWhiteSpace(typeName) &&
                !familyName.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                return familyName + " - " + typeName;
            }

            if (!string.IsNullOrWhiteSpace(typeName))
            {
                return typeName;
            }

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                return familyName;
            }

            return "Mechanical Equipment";
        }

        private static string GetAirTerminalLabel(Element element)
        {
            string mark = GetParameterText(element, BuiltInParameter.ALL_MODEL_MARK);
            if (!string.IsNullOrWhiteSpace(mark))
            {
                return mark;
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance != null)
            {
                string familyName = familyInstance.Symbol != null ? familyInstance.Symbol.FamilyName : string.Empty;
                if (!string.IsNullOrWhiteSpace(familyName))
                {
                    return familyName;
                }
            }

            return element?.Name ?? "Air Terminal";
        }

        private static string GetDuctSizeLabel(Element element)
        {
            BuiltInParameter[] builtInParameters =
            {
                BuiltInParameter.RBS_DUCT_SIZE_FORMATTED_PARAM,
                BuiltInParameter.RBS_DUCT_CALCULATED_SIZE,
                BuiltInParameter.RBS_CALCULATED_SIZE
            };

            for (int i = 0; i < builtInParameters.Length; i++)
            {
                string text = GetParameterText(element, builtInParameters[i]);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private string GetFlowLabel(Element element, SchematicNodeType nodeType)
        {
            if (element == null || nodeType == SchematicNodeType.Equipment)
            {
                return string.Empty;
            }

            BuiltInParameter[] builtInParameters =
            {
                BuiltInParameter.RBS_DUCT_FLOW_PARAM,
                BuiltInParameter.RBS_ADDITIONAL_FLOW
            };

            for (int i = 0; i < builtInParameters.Length; i++)
            {
                string text = GetParameterText(element, builtInParameters[i]);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return GetConnectorFlowLabel(element);
        }

        private string GetConnectorFlowLabel(Element element)
        {
            IList<Connector> connectors = GetUsableConnectors(element);
            if (connectors.Count == 0)
            {
                return string.Empty;
            }

            double bestMagnitude = 0;
            for (int i = 0; i < connectors.Count; i++)
            {
                Connector connector = connectors[i];
                double flow = 0;

                try
                {
                    flow = Math.Abs(connector.AssignedFlow);
                }
                catch
                {
                    flow = 0;
                }

                if (flow <= 0)
                {
                    try
                    {
                        flow = Math.Abs(connector.Flow);
                    }
                    catch
                    {
                        flow = 0;
                    }
                }

                if (flow > bestMagnitude)
                {
                    bestMagnitude = flow;
                }
            }

            if (bestMagnitude <= 0)
            {
                return string.Empty;
            }

            return RevitCompat.FormatHvacAirflow(_document, bestMagnitude);
        }

        private static bool IsPrimaryEquipment(FamilyInstance familyInstance)
        {
            if (familyInstance?.MEPModel == null)
            {
                return false;
            }

            ConnectorManager connectorManager = familyInstance.MEPModel.ConnectorManager;
            if (connectorManager == null)
            {
                return false;
            }

            foreach (Connector connector in connectorManager.Connectors)
            {
                if (connector == null || !connector.IsValidObject)
                {
                    continue;
                }

                MEPSystem system = connector.MEPSystem;
                FamilyInstance baseEquipment = system?.BaseEquipment;
                if (baseEquipment != null && baseEquipment.Id.IntValue() == familyInstance.Id.IntValue())
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetParameterText(Element element, BuiltInParameter builtInParameter)
        {
            if (element == null)
            {
                return string.Empty;
            }

            try
            {
                Parameter parameter = element.get_Parameter(builtInParameter);
                if (parameter == null || !parameter.HasValue)
                {
                    return string.Empty;
                }

                string valueString = parameter.AsValueString();
                if (!string.IsNullOrWhiteSpace(valueString))
                {
                    return valueString;
                }

                string stringValue = parameter.AsString();
                return stringValue ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string DescribeElement(Element element)
        {
            if (element == null)
            {
                return "<Missing Element>";
            }

            string categoryName = element.Category != null ? element.Category.Name : element.GetType().Name;
            return categoryName + " [" + element.Id.IntValue() + "]";
        }

        private static string BuildConnectionDescription(
            SchematicEdge edge,
            IDictionary<int, SchematicNode> nodesById)
        {
            if (edge == null)
            {
                return "<Unresolved Connection>";
            }

            SchematicNode firstNode;
            SchematicNode secondNode;
            nodesById.TryGetValue(edge.FromElementId.IntValue(), out firstNode);
            nodesById.TryGetValue(edge.ToElementId.IntValue(), out secondNode);

            return DescribeNode(firstNode, edge.FromElementId) + " <-> " + DescribeNode(secondNode, edge.ToElementId);
        }

        private static string DescribeNode(SchematicNode node, ElementId fallbackId)
        {
            if (node == null)
            {
                return "Element [" + (fallbackId != null ? fallbackId.IntValue().ToString() : "?") + "]";
            }

            string typeLabel;
            switch (node.NodeType)
            {
                case SchematicNodeType.Equipment:
                    typeLabel = "Mechanical Equipment";
                    break;
                case SchematicNodeType.AirTerminal:
                    typeLabel = "Air Terminal";
                    break;
                default:
                    typeLabel = "Duct";
                    break;
            }

            return typeLabel + " [" + node.ElementId.IntValue() + "]";
        }

        private static bool TryGetDirectionalFlow(Connector connector, out FlowDirectionType flowDirection)
        {
            flowDirection = FlowDirectionType.Bidirectional;
            if (connector == null || !connector.IsValidObject)
            {
                return false;
            }

            try
            {
                flowDirection = connector.Direction;
            }
            catch
            {
                return false;
            }

            return flowDirection == FlowDirectionType.In || flowDirection == FlowDirectionType.Out;
        }

        private static string CreateLevelKey(SchematicNode node)
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

        private sealed class TraversalState
        {
            public TraversalState(Element currentElement, Connector sourceConnector, int hopCount)
            {
                CurrentElement = currentElement;
                SourceConnector = sourceConnector;
                HopCount = hopCount;
            }

            public Element CurrentElement { get; }
            public Connector SourceConnector { get; }
            public int HopCount { get; }
        }

        private sealed class EdgeAccumulator
        {
            private int _forwardVotes;
            private int _reverseVotes;

            public EdgeAccumulator(int firstElementId, int secondElementId)
            {
                if (firstElementId <= secondElementId)
                {
                    FirstElementId = firstElementId;
                    SecondElementId = secondElementId;
                }
                else
                {
                    FirstElementId = secondElementId;
                    SecondElementId = firstElementId;
                }
            }

            public int FirstElementId { get; }
            public int SecondElementId { get; }

            public void AddEvidence(
                int sourceElementId,
                int targetElementId,
                SchematicNodeType sourceType,
                SchematicNodeType targetType,
                Connector sourceConnector,
                Connector targetConnector,
                int hopCount)
            {
                int direction = InferDirection(sourceType, targetType, sourceConnector, targetConnector);
                if (direction == 0)
                {
                    return;
                }

                int preferredParentId = direction > 0 ? sourceElementId : targetElementId;
                int preferredChildId = direction > 0 ? targetElementId : sourceElementId;
                int weight = hopCount <= 1 ? 3 : 2;

                if (preferredParentId == FirstElementId && preferredChildId == SecondElementId)
                {
                    _forwardVotes += weight;
                }
                else if (preferredParentId == SecondElementId && preferredChildId == FirstElementId)
                {
                    _reverseVotes += weight;
                }
            }

            public SchematicEdge ToEdge()
            {
                var edge = new SchematicEdge(ElementIdHelper.FromInt(FirstElementId), ElementIdHelper.FromInt(SecondElementId));
                if (_forwardVotes > _reverseVotes)
                {
                    edge.PreferredParentElementId = ElementIdHelper.FromInt(FirstElementId);
                    edge.PreferredChildElementId = ElementIdHelper.FromInt(SecondElementId);
                    edge.DirectionConfidence = _forwardVotes - _reverseVotes;
                }
                else if (_reverseVotes > _forwardVotes)
                {
                    edge.PreferredParentElementId = ElementIdHelper.FromInt(SecondElementId);
                    edge.PreferredChildElementId = ElementIdHelper.FromInt(FirstElementId);
                    edge.DirectionConfidence = _reverseVotes - _forwardVotes;
                }

                return edge;
            }

            private static int InferDirection(
                SchematicNodeType sourceType,
                SchematicNodeType targetType,
                Connector sourceConnector,
                Connector targetConnector)
            {
                int forwardScore = 0;
                int reverseScore = 0;

                FlowDirectionType sourceFlow;
                if (TryGetDirectionalFlow(sourceConnector, out sourceFlow))
                {
                    if (sourceFlow == FlowDirectionType.Out)
                    {
                        forwardScore += 3;
                    }
                    else if (sourceFlow == FlowDirectionType.In)
                    {
                        reverseScore += 3;
                    }
                }

                FlowDirectionType targetFlow;
                if (TryGetDirectionalFlow(targetConnector, out targetFlow))
                {
                    if (targetFlow == FlowDirectionType.In)
                    {
                        forwardScore += 3;
                    }
                    else if (targetFlow == FlowDirectionType.Out)
                    {
                        reverseScore += 3;
                    }
                }

                if (sourceType == SchematicNodeType.Equipment && targetType != SchematicNodeType.Equipment)
                {
                    forwardScore += 1;
                }

                if (targetType == SchematicNodeType.Equipment && sourceType != SchematicNodeType.Equipment)
                {
                    reverseScore += 1;
                }

                if (sourceType == SchematicNodeType.Duct && targetType == SchematicNodeType.AirTerminal)
                {
                    forwardScore += 1;
                }

                if (sourceType == SchematicNodeType.AirTerminal && targetType == SchematicNodeType.Duct)
                {
                    reverseScore += 1;
                }

                if (forwardScore == reverseScore)
                {
                    return 0;
                }

                return forwardScore > reverseScore ? 1 : -1;
            }
        }
    }
}
