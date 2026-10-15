#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Linked Model Elements
 * File Name     : SmartTagLinkedElementCollector.cs
 * Purpose       : Finds the elements inside linked models that are shown in the active view, for the
 *                 categories Smart MEP Tags is set to tag, so they can be tagged from the host model.
 *                 Also lists which linked elements already carry a tag in the view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-14
 * Last Updated  : 2026-10-14
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (TagCompat, ElementIdExtensions),
 *                 AJTools.Services.ViewCrop (ViewCropGeometryProjectionHelper)
 *
 * Input         : Host document, active view, the enabled Smart MEP Tag categories.
 * Output        : Linked elements with their link and link-to-host transform. Read-only - no transaction.
 *
 * Notes         :
 * - Only LOADED links that are shown in the view are read. A view-scoped collector over
 *   RevitLinkInstance returns just the instances visible in the view (hidden link, hidden workset,
 *   RVT Links category off), so those are left out the same way host elements are.
 * - Revit 2023 and later: Revit itself answers "which linked elements does this view show" through
 *   the link-aware view collector FilteredElementCollector(hostDoc, viewId, linkInstanceId). Checked
 *   in the real Revit API files: present in 2023, 2024 and 2027, absent in 2020 and 2022.
 * - Revit 2020 - 2022 have no such collector, so the view is ESTIMATED: the host view must not hide
 *   the category, the element must sit inside the view's depth (plan: view depth up to the cut plane,
 *   ceiling plan: cut plane up to the top; section/elevation: behind the cut line, and in front of the
 *   far clip when it is on), and the crop region is checked afterwards by the normal Smart Tag filter.
 *   Elements in a secondary design option of the link are left out. A linked element hidden one by
 *   one in the view, or by a view filter, cannot be seen by this estimate - Revit 2023+ can.
 * - Identity across documents is (link instance id, element id), never a bare ElementId: ids repeat
 *   between models, so a bare id would mix a linked fan coil up with a host duct.
 *
 * Changelog     :
 * v1.0.0 (2026-10-14) - Initial release (Smart MEP Tags "also tag elements in linked models").
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Services.ViewCrop;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// One element inside a linked model, with the link it is seen through.
    /// </summary>
    internal sealed class SmartTagLinkedElement
    {
        public SmartTagLinkedElement(
            Element element,
            BuiltInCategory category,
            RevitLinkInstance link,
            Transform toHost,
            string linkName,
            BoundingBoxXYZ hostBox)
        {
            Element = element;
            Category = category;
            Link = link;
            ToHost = toHost ?? Transform.Identity;
            LinkName = linkName ?? string.Empty;
            HostBox = hostBox;
        }

        /// <summary>The element, as it lives in the LINKED document.</summary>
        public Element Element { get; }

        public BuiltInCategory Category { get; }

        /// <summary>The link instance in the host model the element is seen through.</summary>
        public RevitLinkInstance Link { get; }

        /// <summary>Linked-model coordinates to host-model coordinates.</summary>
        public Transform ToHost { get; }

        /// <summary>Link name for the report, e.g. "MEP-Model".</summary>
        public string LinkName { get; }

        /// <summary>The element's box in HOST coordinates; null when it has no box.</summary>
        public BoundingBoxXYZ HostBox { get; }
    }

    /// <summary>
    /// What one scan of the linked models found.
    /// </summary>
    internal sealed class SmartTagLinkedScan
    {
        public List<SmartTagLinkedElement> Elements { get; } = new List<SmartTagLinkedElement>();

        /// <summary>Loaded links shown in the view.</summary>
        public int LinksInView { get; set; }

        /// <summary>
        /// True when the view had to be estimated (Revit 2020 - 2022, or a view Revit refused to
        /// answer for), so the report can say the linked list is an estimate.
        /// </summary>
        public bool UsedViewEstimate { get; set; }
    }

    /// <summary>
    /// Reads the linked-model elements Smart MEP Tags may tag.
    /// </summary>
    internal static class SmartTagLinkedElementCollector
    {
        // Small allowance when comparing an element against a view-range plane, so an element sitting
        // exactly on the cut plane is not lost to rounding. About 3 mm.
        private const double DepthTolerance = 0.01;

        /// <summary>
        /// Finds the linked elements of the given categories that are shown in the view.
        /// Never throws: a link or category that cannot be read contributes nothing.
        /// </summary>
        internal static SmartTagLinkedScan Collect(
            Document doc,
            View view,
            IEnumerable<BuiltInCategory> categories)
        {
            var scan = new SmartTagLinkedScan();
            if (doc == null || view == null || categories == null)
                return scan;

            List<BuiltInCategory> categoryList = categories.ToList();
            if (categoryList.Count == 0)
                return scan;

            List<RevitLinkInstance> links = GetLoadedLinksShownInView(doc, view);
            scan.LinksInView = links.Count;
            if (links.Count == 0)
                return scan;

            // Built on first need only - Revit 2023+ normally never needs it.
            ViewDepthEstimate estimate = null;
            var hiddenInView = new Dictionary<BuiltInCategory, bool>();

            foreach (RevitLinkInstance link in links)
            {
                Document linkDoc;
                Transform toHost;
                try
                {
                    linkDoc = link.GetLinkDocument();
                    toHost = link.GetTotalTransform();
                }
                catch (Exception)
                {
                    continue;
                }

                if (linkDoc == null || toHost == null)
                    continue;

                string linkName = GetLinkName(link, linkDoc);

                foreach (BuiltInCategory category in categoryList)
                {
                    // Each entry carries its box in host coordinates, worked out once here.
                    IList<(Element Element, BoundingBoxXYZ HostBox)> shown = null;
#if REVIT2023_OR_GREATER
                    IList<Element> fromRevit = TryCollectShownInView(doc, view, link, category);
                    if (fromRevit != null)
                    {
                        shown = fromRevit
                            .Where(element => element != null)
                            .Select(element => (element, GetHostBoundingBox(element, toHost)))
                            .ToList();
                    }
#endif
                    if (shown == null)
                    {
                        scan.UsedViewEstimate = true;

                        if (!hiddenInView.TryGetValue(category, out bool hidden))
                        {
                            hidden = IsCategoryHiddenInView(view, category);
                            hiddenInView[category] = hidden;
                        }

                        if (hidden)
                            continue;

                        if (estimate == null)
                            estimate = ViewDepthEstimate.Create(doc, view);

                        shown = CollectByEstimate(linkDoc, category, toHost, estimate);
                    }

                    foreach ((Element element, BoundingBoxXYZ hostBox) in shown)
                    {
                        if (element != null)
                            scan.Elements.Add(new SmartTagLinkedElement(element, category, link, toHost, linkName, hostBox));
                    }
                }
            }

            return scan;
        }

        /// <summary>
        /// (link instance, linked element) pairs that already carry a tag in the view.
        /// </summary>
        /// <remarks>
        /// A tag on a linked element points at a LINK reference: its ElementId is the link instance and
        /// its LinkedElementId is the element inside the link. Every reference a tag holds is read, so a
        /// multi-reference tag (Revit 2022+) counts for each element it tags.
        /// </remarks>
        internal static HashSet<(long LinkId, long ElementId)> CollectAlreadyTaggedLinkedKeys(Document doc, View view)
        {
            var keys = new HashSet<(long LinkId, long ElementId)>();
            if (doc == null || view == null)
                return keys;

            try
            {
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element tagElement in tags)
                {
                    if (!(tagElement is IndependentTag tag))
                        continue;

                    IList<Reference> references;
                    try
                    {
                        references = TagCompat.GetTaggedReferences(tag);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    foreach (Reference reference in references)
                    {
                        if (reference == null)
                            continue;

                        ElementId linkedId = reference.LinkedElementId;
                        if (linkedId == null || linkedId == ElementId.InvalidElementId)
                            continue;

                        keys.Add(MakeKey(reference.ElementId, linkedId));
                    }
                }
            }
            catch (Exception)
            {
                // No list means every linked element is treated as untagged - the safe direction here
                // is the same as for host elements in CollectAlreadyTaggedElementIds.
            }

            return keys;
        }

        /// <summary>The cross-document identity of a linked element.</summary>
        internal static (long LinkId, long ElementId) MakeKey(ElementId linkInstanceId, ElementId elementId)
        {
            return (linkInstanceId == null ? -1L : linkInstanceId.LongValue(),
                    elementId == null ? -1L : elementId.LongValue());
        }

        /// <summary>
        /// The element's box in HOST coordinates: its own model box, walked corner by corner through
        /// the link transform so a rotated link still gives the right extents. Null when the element
        /// has no box (nothing drawn).
        /// </summary>
        internal static BoundingBoxXYZ GetHostBoundingBox(Element element, Transform toHost)
        {
            if (element == null)
                return null;

            try
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                XYZ[] corners = ViewCropGeometryProjectionHelper.GetBoundingBoxCorners(box);
                if (corners.Length == 0)
                    return null;

                Transform transform = toHost ?? Transform.Identity;
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (XYZ corner in corners)
                {
                    XYZ p = transform.OfPoint(corner);
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                    minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                }

                return new BoundingBoxXYZ
                {
                    Min = new XYZ(minX, minY, minZ),
                    Max = new XYZ(maxX, maxY, maxZ)
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Elements of each category across every loaded link in the model - for the settings window's
        /// "In links" column. Counts every link instance, since each placed copy can be tagged.
        /// </summary>
        internal static Dictionary<BuiltInCategory, int> CountInLoadedLinks(
            Document doc,
            IEnumerable<BuiltInCategory> categories)
        {
            var counts = new Dictionary<BuiltInCategory, int>();
            List<BuiltInCategory> categoryList = (categories ?? Enumerable.Empty<BuiltInCategory>()).ToList();
            foreach (BuiltInCategory category in categoryList)
                counts[category] = 0;

            if (doc == null || categoryList.Count == 0)
                return counts;

            List<Document> linkDocs = new List<Document>();
            try
            {
                foreach (Element element in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)))
                {
                    try
                    {
                        Document linkDoc = (element as RevitLinkInstance)?.GetLinkDocument();
                        if (linkDoc != null)
                            linkDocs.Add(linkDoc);
                    }
                    catch (Exception)
                    {
                        // An unloaded or broken link simply has nothing to count.
                    }
                }
            }
            catch (Exception)
            {
                return counts;
            }

            foreach (Document linkDoc in linkDocs)
            {
                foreach (BuiltInCategory category in categoryList)
                {
                    try
                    {
                        counts[category] += new FilteredElementCollector(linkDoc)
                            .OfCategory(category)
                            .WhereElementIsNotElementType()
                            .GetElementCount();
                    }
                    catch (Exception)
                    {
                        // A category the link cannot be asked about adds nothing.
                    }
                }
            }

            return counts;
        }

        // ─────────────────────────────────────────────
        // Links in the view
        // ─────────────────────────────────────────────

        private static List<RevitLinkInstance> GetLoadedLinksShownInView(Document doc, View view)
        {
            var links = new List<RevitLinkInstance>();

            IList<Element> found;
            try
            {
                // Scoped to the view: a link hidden in the view, on a hidden workset or with the RVT
                // Links category switched off is not returned.
                found = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(RevitLinkInstance))
                    .ToElements();
            }
            catch (Exception)
            {
                return links;
            }

            foreach (Element element in found)
            {
                if (!(element is RevitLinkInstance link))
                    continue;

                try
                {
                    // Unloaded links have no document to read.
                    if (link.GetLinkDocument() == null)
                        continue;

                    if (link.IsHidden(view))
                        continue;
                }
                catch (Exception)
                {
                    continue;
                }

                links.Add(link);
            }

            return links;
        }

        private static string GetLinkName(RevitLinkInstance link, Document linkDoc)
        {
            string name = null;
            try
            {
                name = !string.IsNullOrWhiteSpace(linkDoc?.Title) ? linkDoc.Title : link?.Name;
            }
            catch (Exception)
            {
                // Fall through to the generic name.
            }

            if (string.IsNullOrWhiteSpace(name))
                return "Linked model";

            // An instance name reads "File.rvt : 1 : location ..." - the part before the colon is enough.
            int colon = name.IndexOf(':');
            if (colon > 0)
                name = name.Substring(0, colon).Trim();

            if (name.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);

            return name;
        }

        // ─────────────────────────────────────────────
        // Revit 2023+: Revit says what the view shows
        // ─────────────────────────────────────────────

#if REVIT2023_OR_GREATER
        /// <summary>
        /// The link's elements of one category that Revit draws in this view. Null when Revit refuses
        /// the question (some view kinds cannot list drawn elements), so the caller can estimate instead.
        /// </summary>
        private static IList<Element> TryCollectShownInView(
            Document hostDoc,
            View view,
            RevitLinkInstance link,
            BuiltInCategory category)
        {
            try
            {
                return new FilteredElementCollector(hostDoc, view.Id, link.Id)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch (Exception)
            {
                return null;
            }
        }
#endif

        // ─────────────────────────────────────────────
        // Revit 2020 - 2022: estimate what the view shows
        // ─────────────────────────────────────────────

        private static IList<(Element Element, BoundingBoxXYZ HostBox)> CollectByEstimate(
            Document linkDoc,
            BuiltInCategory category,
            Transform toHost,
            ViewDepthEstimate estimate)
        {
            var shown = new List<(Element Element, BoundingBoxXYZ HostBox)>();

            try
            {
                foreach (Element element in new FilteredElementCollector(linkDoc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType())
                {
                    if (element == null || element.ViewSpecific)
                        continue;

                    if (IsInSecondaryDesignOption(element))
                        continue;

                    BoundingBoxXYZ hostBox = GetHostBoundingBox(element, toHost);
                    if (hostBox == null)
                        continue;

                    if (estimate != null && !estimate.Overlaps(hostBox))
                        continue;

                    shown.Add((element, hostBox));
                }
            }
            catch (Exception)
            {
                // A category the link cannot be asked about contributes nothing.
            }

            return shown;
        }

        /// <summary>
        /// A host view shows a link's main model and its PRIMARY design options only.
        /// </summary>
        private static bool IsInSecondaryDesignOption(Element element)
        {
            try
            {
                DesignOption option = element.DesignOption;
                return option != null && !option.IsPrimary;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsCategoryHiddenInView(View view, BuiltInCategory category)
        {
            try
            {
                return view.GetCategoryHidden(new ElementId(category));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The depth a view shows, estimated from its view range (plans) or its crop box (sections and
        /// elevations). Anything that cannot be read is left open, so the estimate only ever narrows
        /// what is clearly outside the view and never drops everything because one value was missing.
        /// </summary>
        private sealed class ViewDepthEstimate
        {
            // Plans: a band of world height. Sections/elevations: a band along the view direction,
            // measured in the crop box's own coordinates (Z points towards the viewer).
            private bool _isPlan;
            private double _minZ = double.NegativeInfinity;
            private double _maxZ = double.PositiveInfinity;
            private Transform _modelToView;

            internal static ViewDepthEstimate Create(Document doc, View view)
            {
                var estimate = new ViewDepthEstimate();

                try
                {
                    if (view is ViewPlan plan)
                    {
                        estimate._isPlan = true;
                        estimate.ReadPlanRange(doc, plan);
                    }
                    else
                    {
                        estimate.ReadSectionDepth(view);
                    }
                }
                catch (Exception)
                {
                    return new ViewDepthEstimate { _isPlan = true };
                }

                return estimate;
            }

            internal bool Overlaps(BoundingBoxXYZ hostBox)
            {
                if (hostBox == null || hostBox.Min == null || hostBox.Max == null)
                    return true;

                double low;
                double high;

                if (_isPlan)
                {
                    low = Math.Min(hostBox.Min.Z, hostBox.Max.Z);
                    high = Math.Max(hostBox.Min.Z, hostBox.Max.Z);
                }
                else
                {
                    if (_modelToView == null)
                        return true;

                    low = double.MaxValue;
                    high = double.MinValue;
                    foreach (XYZ corner in ViewCropGeometryProjectionHelper.GetBoundingBoxCorners(hostBox))
                    {
                        double z = _modelToView.OfPoint(corner).Z;
                        low = Math.Min(low, z);
                        high = Math.Max(high, z);
                    }

                    if (low > high)
                        return true;
                }

                return high >= _minZ - DepthTolerance && low <= _maxZ + DepthTolerance;
            }

            /// <summary>
            /// Floor plan: from the bottom of the view depth up to the cut plane - anything above the
            /// cut plane is not drawn in a plan. Ceiling plan: it looks up, so from the cut plane to
            /// the top of the range.
            /// </summary>
            private void ReadPlanRange(Document doc, ViewPlan plan)
            {
                PlanViewRange range = plan.GetViewRange();
                if (range == null)
                    return;

                Level viewLevel = plan.GenLevel;
                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(level => level.ProjectElevation)
                    .ToList();

                if (plan.ViewType == ViewType.CeilingPlan)
                {
                    _minZ = ResolvePlaneZ(doc, range, PlanViewPlane.CutPlane, viewLevel, levels, double.NegativeInfinity);

                    double top = ResolvePlaneZ(doc, range, PlanViewPlane.TopClipPlane, viewLevel, levels, double.NegativeInfinity);
                    double depth = ResolvePlaneZ(doc, range, PlanViewPlane.ViewDepthPlane, viewLevel, levels, double.NegativeInfinity);
                    double highest = Math.Max(top, depth);
                    _maxZ = double.IsNegativeInfinity(highest) ? double.PositiveInfinity : highest;
                }
                else
                {
                    _minZ = ResolvePlaneZ(doc, range, PlanViewPlane.ViewDepthPlane, viewLevel, levels, double.NegativeInfinity);
                    _maxZ = ResolvePlaneZ(doc, range, PlanViewPlane.CutPlane, viewLevel, levels, double.PositiveInfinity);
                }

                // A range that reads upside down means something was misread - leave it open.
                if (_minZ > _maxZ)
                {
                    _minZ = double.NegativeInfinity;
                    _maxZ = double.PositiveInfinity;
                }
            }

            private static double ResolvePlaneZ(
                Document doc,
                PlanViewRange range,
                PlanViewPlane plane,
                Level viewLevel,
                List<Level> levels,
                double whenOpen)
            {
                try
                {
                    ElementId levelId = range.GetLevelId(plane);
                    double offset = range.GetOffset(plane);

                    if (levelId == null
                        || levelId == ElementId.InvalidElementId
                        || levelId == PlanViewRange.Unlimited)
                    {
                        return whenOpen;
                    }

                    Level level;
                    if (levelId == PlanViewRange.LevelAbove)
                        level = NeighbourLevel(levels, viewLevel, above: true);
                    else if (levelId == PlanViewRange.LevelBelow)
                        level = NeighbourLevel(levels, viewLevel, above: false);
                    else if (levelId == PlanViewRange.Current)
                        level = viewLevel;
                    else
                        level = doc.GetElement(levelId) as Level;

                    return level == null ? whenOpen : level.ProjectElevation + offset;
                }
                catch (Exception)
                {
                    return whenOpen;
                }
            }

            private static Level NeighbourLevel(List<Level> levels, Level viewLevel, bool above)
            {
                if (viewLevel == null || levels == null)
                    return null;

                double elevation = viewLevel.ProjectElevation;
                return above
                    ? levels.FirstOrDefault(level => level.ProjectElevation > elevation + DepthTolerance)
                    : levels.LastOrDefault(level => level.ProjectElevation < elevation - DepthTolerance);
            }

            /// <summary>
            /// Section or elevation: the crop box is the view's 3D volume, with Z pointing towards the
            /// viewer. Its near face is the cut line - nothing in front of it is drawn. Its far face only
            /// counts when far clipping is switched on.
            /// </summary>
            private void ReadSectionDepth(View view)
            {
                BoundingBoxXYZ crop = view.CropBox;
                if (crop == null || crop.Min == null || crop.Max == null || crop.Transform == null)
                    return;

                _modelToView = crop.Transform.Inverse;
                _maxZ = Math.Max(crop.Min.Z, crop.Max.Z);

                if (IsFarClipOn(view))
                    _minZ = Math.Min(crop.Min.Z, crop.Max.Z);
            }

            private static bool IsFarClipOn(View view)
            {
                try
                {
                    Parameter farClip = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING);
                    return farClip != null
                        && farClip.HasValue
                        && farClip.StorageType == StorageType.Integer
                        && farClip.AsInteger() != 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
