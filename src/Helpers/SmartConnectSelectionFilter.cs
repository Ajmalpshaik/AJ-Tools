// Tool Name: Connect MEP Elements (Smart Connect) - Selection Filter
// Description: Restricts selection to supported and compatible MEP elements.
// Author: Ajmal P.S.
// Version: 2.0.0
// Last Updated: 2026-08-15
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI.Selection, AJTools.Models

using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using AJTools.Models;

namespace AJTools.Utils
{
    /// <summary>
    /// Selection filter for Connect MEP Elements supported categories.
    /// </summary>
    /// <remarks>
    /// Straight runs (Pipe, Duct, Cable Tray, Conduit) are always candidates. Flex curves and
    /// connector-bearing family instances (equipment, air terminals, fittings, accessories) are
    /// candidates only when the matching setting is on. A family instance qualifies by actually
    /// having an open End connector, not by belonging to a hard-coded category list.
    /// </remarks>
    internal sealed class SmartConnectSelectionFilter : ISelectionFilter
    {
        private static readonly HashSet<BuiltInCategory> CoreCurveCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_CableTray
        };

        private static readonly HashSet<BuiltInCategory> ConduitCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_Conduit
        };

        private static readonly HashSet<BuiltInCategory> FlexCurveCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_FlexDuctCurves
        };

        private readonly SmartConnectSettings _settings;
        private readonly Element _referenceElement;

        /// <summary>First-pick filter: anything the current settings allow.</summary>
        public SmartConnectSelectionFilter(SmartConnectSettings settings)
            : this(settings, null)
        {
        }

        /// <summary>Second-pick filter: only elements compatible with <paramref name="referenceElement"/>.</summary>
        public SmartConnectSelectionFilter(SmartConnectSettings settings, Element referenceElement)
        {
            _settings = settings ?? new SmartConnectSettings();
            _referenceElement = referenceElement;
        }

        public bool AllowElement(Element elem)
        {
            if (!IsSupported(elem, _settings))
            {
                return false;
            }

            if (_referenceElement == null)
            {
                return true;
            }

            if (_referenceElement.Id == elem.Id)
            {
                return false;
            }

            string reason;
            return AreCompatible(_referenceElement, elem, out reason);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

        /// <summary>
        /// True when the element is a category the tool can route from, given the current settings.
        /// </summary>
        public static bool IsSupported(Element element, SmartConnectSettings settings)
        {
            if (element == null)
            {
                return false;
            }

            SmartConnectSettings effective = settings ?? new SmartConnectSettings();

            BuiltInCategory category;
            if (TryGetBuiltInCategory(element, out category))
            {
                if (CoreCurveCategories.Contains(category))
                {
                    return element is MEPCurve;
                }

                if (ConduitCategories.Contains(category))
                {
                    return effective.AllowConduit && element is MEPCurve;
                }

                if (FlexCurveCategories.Contains(category))
                {
                    return effective.AllowFlexCurves && element is MEPCurve;
                }
            }

            if (!effective.AllowFamilyInstanceConnectors)
            {
                return false;
            }

            return HasOpenEndConnector(element);
        }

        /// <summary>
        /// True when two supported elements may be connected to each other.
        /// </summary>
        /// <remarks>
        /// Two straight runs must share a category - a cable tray and a conduit share a Revit domain
        /// but cannot physically join. Once a family instance is involved, matching the connector
        /// domain is the right test, because a duct legitimately connects to an air terminal.
        /// </remarks>
        public static bool AreCompatible(Element first, Element second, out string reason)
        {
            reason = string.Empty;

            if (first == null || second == null)
            {
                reason = "One of the selected elements is invalid.";
                return false;
            }

            if (first.Id == second.Id)
            {
                reason = "Please select two different elements.";
                return false;
            }

            bool firstIsCurve = first is MEPCurve;
            bool secondIsCurve = second is MEPCurve;

            if (firstIsCurve && secondIsCurve)
            {
                BuiltInCategory firstCategory;
                BuiltInCategory secondCategory;
                if (!TryGetBuiltInCategory(first, out firstCategory) ||
                    !TryGetBuiltInCategory(second, out secondCategory))
                {
                    reason = "Could not read the category of the selected elements.";
                    return false;
                }

                // Flex sits in its own category, so comparing categories raw would refuse the
                // perfectly ordinary "flex duct into rigid duct" pick and leave the whole flex
                // path unreachable. Compare on the rigid equivalent instead.
                BuiltInCategory firstRigid = ToRigidEquivalent(firstCategory);
                BuiltInCategory secondRigid = ToRigidEquivalent(secondCategory);

                if (firstRigid != secondRigid)
                {
                    reason = "A " + GetCategoryDisplayName(firstCategory) + " and a " +
                             GetCategoryDisplayName(secondCategory) + " cannot be joined.";
                    return false;
                }

                return true;
            }

            Domain firstDomain;
            Domain secondDomain;
            bool hasFirstDomain = TryGetElementDomain(first, out firstDomain);
            bool hasSecondDomain = TryGetElementDomain(second, out secondDomain);

            if (!hasFirstDomain || !hasSecondDomain)
            {
                reason = "One of the selected elements has no usable open connector.";
                return false;
            }

            if (firstDomain == Domain.DomainUndefined || secondDomain == Domain.DomainUndefined)
            {
                return true;
            }

            if (firstDomain != secondDomain)
            {
                reason = "The selected elements belong to different MEP domains and cannot be joined.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Kept for compatibility with existing callers: reports the built-in category of a straight run.
        /// </summary>
        public static bool TryGetSupportedCategory(Element element, out BuiltInCategory category)
        {
            category = BuiltInCategory.INVALID;

            BuiltInCategory resolved;
            if (!TryGetBuiltInCategory(element, out resolved))
            {
                return false;
            }

            if (!CoreCurveCategories.Contains(resolved) &&
                !ConduitCategories.Contains(resolved) &&
                !FlexCurveCategories.Contains(resolved))
            {
                return false;
            }

            category = resolved;
            return true;
        }

        /// <summary>
        /// Maps a flex category onto the rigid category it can legitimately join, so that
        /// "flex duct to duct" and "flex pipe to pipe" are treated as compatible picks.
        /// </summary>
        private static BuiltInCategory ToRigidEquivalent(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_FlexDuctCurves:
                    return BuiltInCategory.OST_DuctCurves;
                case BuiltInCategory.OST_FlexPipeCurves:
                    return BuiltInCategory.OST_PipeCurves;
                default:
                    return category;
            }
        }

        public static string GetCategoryDisplayName(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_PipeCurves:
                    return "Pipe";
                case BuiltInCategory.OST_DuctCurves:
                    return "Duct";
                case BuiltInCategory.OST_CableTray:
                    return "Cable Tray";
                case BuiltInCategory.OST_Conduit:
                    return "Conduit";
                case BuiltInCategory.OST_FlexPipeCurves:
                    return "Flex Pipe";
                case BuiltInCategory.OST_FlexDuctCurves:
                    return "Flex Duct";
                default:
                    return "Unsupported";
            }
        }

        /// <summary>
        /// Friendly name for prompts: the category name for a straight run, otherwise the family name.
        /// </summary>
        public static string GetElementDisplayName(Element element)
        {
            if (element == null)
            {
                return "element";
            }

            BuiltInCategory category;
            if (TryGetBuiltInCategory(element, out category) && element is MEPCurve)
            {
                string name = GetCategoryDisplayName(category);
                if (name != "Unsupported")
                {
                    return name;
                }
            }

            Category elementCategory = element.Category;
            if (elementCategory != null && !string.IsNullOrWhiteSpace(elementCategory.Name))
            {
                return elementCategory.Name;
            }

            return "element";
        }

        /// <summary>
        /// Text describing what may be picked, used in the Revit status prompt.
        /// </summary>
        public static string DescribeSupportedCategories(SmartConnectSettings settings)
        {
            SmartConnectSettings effective = settings ?? new SmartConnectSettings();

            var parts = new List<string> { "Pipe", "Duct", "Cable Tray" };
            if (effective.AllowConduit)
            {
                parts.Add("Conduit");
            }

            if (effective.AllowFlexCurves)
            {
                parts.Add("Flex");
            }

            if (effective.AllowFamilyInstanceConnectors)
            {
                parts.Add("MEP equipment");
            }

            return string.Join(", ", parts);
        }

        private static bool HasOpenEndConnector(Element element)
        {
            ConnectorManager manager = GetConnectorManager(element);
            if (manager == null)
            {
                return false;
            }

            foreach (Connector connector in manager.Connectors)
            {
                if (connector == null || !connector.IsValidObject)
                {
                    continue;
                }

                if (connector.ConnectorType != ConnectorType.End)
                {
                    continue;
                }

                if (!connector.IsConnected)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetElementDomain(Element element, out Domain domain)
        {
            domain = Domain.DomainUndefined;

            ConnectorManager manager = GetConnectorManager(element);
            if (manager == null)
            {
                return false;
            }

            bool foundOpenConnector = false;
            foreach (Connector connector in manager.Connectors)
            {
                if (connector == null || !connector.IsValidObject)
                {
                    continue;
                }

                if (connector.ConnectorType != ConnectorType.End || connector.IsConnected)
                {
                    continue;
                }

                foundOpenConnector = true;

                // First defined domain wins; undefined connectors stay compatible with anything.
                if (connector.Domain != Domain.DomainUndefined)
                {
                    domain = connector.Domain;
                    return true;
                }
            }

            return foundOpenConnector;
        }

        private static ConnectorManager GetConnectorManager(Element element)
        {
            MEPCurve mepCurve = element as MEPCurve;
            if (mepCurve != null)
            {
                return mepCurve.ConnectorManager;
            }

            FamilyInstance familyInstance = element as FamilyInstance;
            if (familyInstance != null && familyInstance.MEPModel != null)
            {
                return familyInstance.MEPModel.ConnectorManager;
            }

            return null;
        }

        private static bool TryGetBuiltInCategory(Element element, out BuiltInCategory category)
        {
            category = BuiltInCategory.INVALID;

            Category elementCategory = element == null ? null : element.Category;
            if (elementCategory == null)
            {
                return false;
            }

            int categoryId = elementCategory.Id.IntValue();
            if (!ElementIdHelper.IsDefinedBuiltInCategory(categoryId))
            {
                return false;
            }

            category = (BuiltInCategory)categoryId;
            return true;
        }
    }
}
