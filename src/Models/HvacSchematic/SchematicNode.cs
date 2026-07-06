// ==================================================
// Tool Name    : HVAC Schematic
// Purpose      : Represents a schematic node derived from an HVAC model element.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-07
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : HVAC element identity, type, and resolved analysis data.
// Output       : Structured node data for schematic analysis and drafting layout.
// Notes        : Stores resolved level, branch, and layout state for drafting-view generation.
// Changelog    : v1.0.0 - Initial production-ready HVAC schematic model with standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using Autodesk.Revit.DB;

namespace AJTools.Models.HvacSchematic
{
    internal enum SchematicNodeType
    {
        Equipment,
        Duct,
        AirTerminal
    }

    internal sealed class SchematicNode
    {
        public SchematicNode(ElementId elementId, SchematicNodeType nodeType)
        {
            ElementId = elementId ?? ElementId.InvalidElementId;
            NodeType = nodeType;
            LevelName = "Unresolved Level";
            NetworkIndex = -1;
            Depth = int.MaxValue;
            ColumnIndex = int.MaxValue;
            ParentElementId = ElementId.InvalidElementId;
            BranchTier = 0;
            IsTrunk = false;
            GuideY = 0;
            TrunkY = 0;
            Position = new XYZ();
        }

        public ElementId ElementId { get; }
        public SchematicNodeType NodeType { get; }
        public string Label { get; set; }
        public string SizeLabel { get; set; }
        public string FlowLabel { get; set; }
        public string LevelName { get; set; }
        public double? LevelElevation { get; set; }
        public bool IsLevelResolved { get; set; }
        public bool HasConnectorData { get; set; }
        public bool IsRoot { get; set; }
        public bool IsPrimaryEquipment { get; set; }
        public int NetworkIndex { get; set; }
        public int Depth { get; set; }
        public int ColumnIndex { get; set; }
        public ElementId ParentElementId { get; set; }
        public int BranchTier { get; set; }
        public bool IsTrunk { get; set; }
        public double GuideY { get; set; }
        public double TrunkY { get; set; }
        public XYZ Position { get; set; }

        public override string ToString()
        {
            return NodeType + ":" + AJTools.Utils.ElementIdHelper.GetIntegerValue(ElementId);
        }
    }
}
