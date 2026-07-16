// ==================================================
// Tool Name    : HVAC Schematic
// Purpose      : Represents a logical connection between two schematic nodes.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-07
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Connected schematic element ids and inferred hierarchy hints.
// Output       : Structured edge data for network analysis and drafting layout.
// Notes        : Tracks inferred parent-child direction and level-transition metadata.
// Changelog    : v1.0.0 - Initial production-ready HVAC schematic model with standardized metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using Autodesk.Revit.DB;

using AJTools.Utils;
namespace AJTools.Models.HvacSchematic
{
    internal sealed class SchematicEdge
    {
        public SchematicEdge(ElementId fromElementId, ElementId toElementId)
        {
            FromElementId = fromElementId ?? ElementId.InvalidElementId;
            ToElementId = toElementId ?? ElementId.InvalidElementId;
            NetworkIndex = -1;
            PreferredParentElementId = ElementId.InvalidElementId;
            PreferredChildElementId = ElementId.InvalidElementId;
            DirectionConfidence = 0;
        }

        public ElementId FromElementId { get; }
        public ElementId ToElementId { get; }
        public int NetworkIndex { get; set; }
        public bool IsTreeEdge { get; set; }
        public bool IsLevelTransition { get; set; }
        public ElementId PreferredParentElementId { get; set; }
        public ElementId PreferredChildElementId { get; set; }
        public int DirectionConfidence { get; set; }

        public bool HasDirectionHint
        {
            get
            {
                return PreferredParentElementId != null &&
                       PreferredChildElementId != null &&
                       PreferredParentElementId.IntValue() != ElementId.InvalidElementId.IntValue() &&
                       PreferredChildElementId.IntValue() != ElementId.InvalidElementId.IntValue();
            }
        }

        public bool Connects(ElementId first, ElementId second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return (FromElementId.IntValue() == first.IntValue() && ToElementId.IntValue() == second.IntValue()) ||
                   (FromElementId.IntValue() == second.IntValue() && ToElementId.IntValue() == first.IntValue());
        }

        public int GetHierarchyPreference(int parentElementId, int childElementId)
        {
            if (!HasDirectionHint)
            {
                return 0;
            }

            if (PreferredParentElementId.IntValue() == parentElementId &&
                PreferredChildElementId.IntValue() == childElementId)
            {
                return Math.Max(1, DirectionConfidence);
            }

            if (PreferredParentElementId.IntValue() == childElementId &&
                PreferredChildElementId.IntValue() == parentElementId)
            {
                return -Math.Max(1, DirectionConfidence);
            }

            return 0;
        }
    }
}
