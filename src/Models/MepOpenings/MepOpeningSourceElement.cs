#region Metadata
/*
 * Tool Name     : Create Openings
 * File Name     : MepOpeningSourceElement.cs
 * Purpose       : Represents a selected MEP source element from the current model or a linked model.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-04
 * Last Updated  : 2026-07-04
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Current-model or linked-model selected MEP element.
 * Output        : Source element plus transform into the active model coordinate system.
 *
 * Notes         :
 * - Linked elements are read only; this object only carries geometry context.
 * - Direct openings are still created only in editable current-model hosts.
 *
 * Changelog     :
 * v1.0.0 (2026-07-04) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models.MepOpenings
{
    internal sealed class MepOpeningSourceElement
    {
        private MepOpeningSourceElement()
        {
        }

        public Document SourceDocument { get; private set; }

        public Element Element { get; private set; }

        public Transform TransformToHost { get; private set; }

        public bool IsLinked { get; private set; }

        public ElementId LinkInstanceId { get; private set; }

        public string SourceLabel { get; private set; }

        public static MepOpeningSourceElement FromCurrent(Document doc, Element element)
        {
            return new MepOpeningSourceElement
            {
                SourceDocument = doc,
                Element = element,
                TransformToHost = Transform.Identity,
                IsLinked = false,
                LinkInstanceId = ElementId.InvalidElementId,
                SourceLabel = "Current Model"
            };
        }

        public static MepOpeningSourceElement FromLinked(
            Document linkDoc,
            Element element,
            RevitLinkInstance linkInstance,
            string linkName)
        {
            return new MepOpeningSourceElement
            {
                SourceDocument = linkDoc,
                Element = element,
                TransformToHost = linkInstance == null ? Transform.Identity : linkInstance.GetTotalTransform(),
                IsLinked = true,
                LinkInstanceId = linkInstance == null ? ElementId.InvalidElementId : linkInstance.Id,
                SourceLabel = string.IsNullOrWhiteSpace(linkName) ? "Linked Model" : linkName
            };
        }
    }
}
