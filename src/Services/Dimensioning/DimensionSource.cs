#region Metadata
/*
 * Tool Name     : Dimensioning (shared)
 * File Name     : DimensionSource.cs
 * Purpose       : Carries a reference element's origin - open project or a specific Revit link - together
 *                 with the transform into host coordinates, and owns every link-aware operation the
 *                 dimension tools need: enumerating loaded links, converting a link-local Reference into
 *                 a host Reference, building a cross-document identity key, and resolving a picked
 *                 reference that landed inside a link.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (ElementIdExtensions)
 *
 * Input         : Host Document, optional RevitLinkInstance, References and Elements from either.
 * Output        : Host-space References, host-space transforms, stable cross-document keys.
 *
 * Notes         :
 * - Reference.CreateLinkReference(RevitLinkInstance) was verified present in the real installed
 *   RevitAPI.dll for Revit 2020 and 2024, so linked dimensioning is available on every supported
 *   version - it is not a newer-Revit-only feature.
 * - BUT CreateLinkReference alone does NOT produce a reference NewDimension will take: it throws
 *   "the references are not geometric references". The reference must be rebuilt from its stable
 *   representation with "RVTLINK/<linkTypeUniqueId>" reduced to "RVTLINK" - see PrepareForDimensioning.
 *   That representation is undocumented internal Revit structure, so the rewrite is fully guarded and
 *   degrades to the plain link reference rather than failing the run.
 * - Autodesk's own guidance is that dimensions anchored to a linked element can be dropped when the
 *   link is reloaded or the host is reopened. Both settings windows say so where links are switched on.
 * - GetTotalTransform() is used, never GetTransform(): only the former accumulates nested links.
 * - Element ids are NOT unique across documents. Every identity in these tools is the pair
 *   (link instance id, element id), never a bare ElementId - see BuildKey.
 * - A dimension anchored to a linked element depends on that link staying loaded; if the link is
 *   unloaded or removed, Revit drops the dimension. That is Revit behaviour, not a tool limitation.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.Dimensioning
{
    /// <summary>
    /// Where a reference element lives, and how to get it into host coordinates.
    /// One instance is created per document being read (the host, plus one per loaded link).
    /// </summary>
    internal sealed class DimensionSource
    {
        /// <summary>The document the elements are read from - the host document, or a link document.</summary>
        public Document Document { get; private set; }

        /// <summary>Always the open project. Stable representations must be built against this.</summary>
        public Document HostDocument { get; private set; }

        /// <summary>Null for the open project; the link instance otherwise.</summary>
        public RevitLinkInstance LinkInstance { get; private set; }

        /// <summary>Transform.Identity for the open project; the link's total transform otherwise.</summary>
        public Transform ToHost { get; private set; }

        /// <summary>Display name used in reports ("Current model" or the link's clean name).</summary>
        public string DisplayName { get; private set; }

        /// <summary>True when this source is a Revit link.</summary>
        public bool IsLinked
        {
            get { return LinkInstance != null; }
        }

        private DimensionSource() { }

        /// <summary>Creates the source for the open project.</summary>
        internal static DimensionSource ForHost(Document hostDocument)
        {
            if (hostDocument == null)
                throw new ArgumentNullException(nameof(hostDocument));

            return new DimensionSource
            {
                Document = hostDocument,
                HostDocument = hostDocument,
                LinkInstance = null,
                ToHost = Transform.Identity,
                DisplayName = "Current model"
            };
        }

        /// <summary>
        /// Creates the source for a loaded link. Returns null when the link is unloaded or its
        /// document cannot be read - callers skip those cleanly rather than failing the run.
        /// </summary>
        internal static DimensionSource ForLink(Document hostDocument, RevitLinkInstance linkInstance)
        {
            if (hostDocument == null || linkInstance == null)
                return null;

            Document linkDocument;
            try
            {
                linkDocument = linkInstance.GetLinkDocument();
            }
            catch
            {
                return null;
            }

            if (linkDocument == null)
                return null;

            Transform toHost;
            try
            {
                // GetTotalTransform, not GetTransform: only this one accumulates nested links.
                toHost = linkInstance.GetTotalTransform() ?? Transform.Identity;
            }
            catch
            {
                toHost = Transform.Identity;
            }

            return new DimensionSource
            {
                Document = linkDocument,
                HostDocument = hostDocument,
                LinkInstance = linkInstance,
                ToHost = toHost,
                DisplayName = GetCleanLinkName(linkInstance, linkDocument)
            };
        }

        /// <summary>
        /// Every source the settings ask for: the open project when <paramref name="includeCurrent"/>,
        /// plus one per loaded link when <paramref name="includeLinked"/>.
        /// </summary>
        internal static IList<DimensionSource> Collect(Document hostDocument, bool includeCurrent, bool includeLinked)
        {
            List<DimensionSource> sources = new List<DimensionSource>();
            if (hostDocument == null)
                return sources;

            if (includeCurrent)
                sources.Add(ForHost(hostDocument));

            if (!includeLinked)
                return sources;

            IList<RevitLinkInstance> links;
            try
            {
                links = new FilteredElementCollector(hostDocument)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(link => link != null)
                    .ToList();
            }
            catch
            {
                return sources;
            }

            foreach (RevitLinkInstance link in links)
            {
                DimensionSource source = ForLink(hostDocument, link);
                if (source != null)
                    sources.Add(source);
            }

            return sources;
        }

        /// <summary>
        /// Converts a Reference obtained from this source's document into one the host document can
        /// DIMENSION to. Host references pass straight through. Returns null when the conversion is
        /// rejected (nested links and some element kinds cannot be referenced across the boundary).
        /// </summary>
        /// <remarks>
        /// CreateLinkReference on its own is NOT enough. Handing its result to NewDimension throws
        /// "the references are not geometric references" - the same wall the Dynamo Dimension.ByElements
        /// node hits on linked elements. The reference has to be rebuilt from its stable representation
        /// with the "RVTLINK/&lt;linkTypeUniqueId&gt;" segment reduced to a bare "RVTLINK", which is what
        /// NewDimension will accept. See PrepareForDimensioning.
        /// </remarks>
        internal Reference ToHostReference(Reference reference)
        {
            if (reference == null)
                return null;

            if (!IsLinked)
                return reference;

            Reference linkReference;
            try
            {
                linkReference = reference.CreateLinkReference(LinkInstance);
            }
            catch
            {
                return null;
            }

            if (linkReference == null)
                return null;

            // Preferred: the rewritten representation NewDimension accepts.
            Reference dimensionable = PrepareForDimensioning(HostDocument, linkReference);
            if (dimensionable != null)
                return dimensionable;

            // The rewrite is undocumented internal structure, so if Revit ever changes it we fall back
            // to the plain link reference rather than losing the target outright. It may still be
            // refused at creation time, which the factory reports as a skipped item.
            return linkReference;
        }

        /// <summary>
        /// Rewrites a link reference's stable representation into the form NewDimension accepts, by
        /// reducing the "RVTLINK/&lt;linkTypeUniqueId&gt;" segment to a bare "RVTLINK".
        /// Returns null if anything about the representation is not what we expect.
        /// </summary>
        /// <remarks>
        /// Revit produces  &lt;linkInstanceUniqueId&gt;:0:RVTLINK/&lt;linkTypeUniqueId&gt;:&lt;idInLink&gt;:&lt;index&gt;:SURFACE
        /// but dimensions need &lt;linkInstanceUniqueId&gt;:0:RVTLINK:&lt;idInLink&gt;:&lt;index&gt;:SURFACE.
        /// The stable-representation string is internal Revit structure with no compatibility promise,
        /// so every step here is guarded and failure is non-fatal.
        /// </remarks>
        internal static Reference PrepareForDimensioning(Document hostDocument, Reference linkReference)
        {
            if (hostDocument == null || linkReference == null)
                return null;

            try
            {
                ElementId linkedElementId = linkReference.LinkedElementId;
                if (linkedElementId == null || linkedElementId == ElementId.InvalidElementId)
                    return null;

                string representation = linkReference.ConvertToStableRepresentation(hostDocument);
                if (string.IsNullOrWhiteSpace(representation))
                    return null;

                string[] parts = representation.Split(':');
                StringBuilder rebuilt = new StringBuilder();
                bool first = true;

                foreach (string part in parts)
                {
                    string piece = part;

                    if (piece.IndexOf("RVTLINK", StringComparison.Ordinal) >= 0)
                    {
                        // Keep the ":0:" that must precede RVTLINK exactly once.
                        piece = rebuilt.ToString().EndsWith(":0", StringComparison.Ordinal)
                            ? "RVTLINK"
                            : "0:RVTLINK";
                    }

                    if (first)
                    {
                        rebuilt.Append(piece);
                        first = false;
                    }
                    else
                    {
                        rebuilt.Append(':').Append(piece);
                    }
                }

                string rewritten = rebuilt.ToString();
                if (string.Equals(rewritten, representation, StringComparison.Ordinal))
                    return null;

                return Reference.ParseFromStableRepresentation(hostDocument, rewritten);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A cross-document identity for an element in this source. Element ids repeat between
        /// documents, so the link instance id is always part of the key.
        /// </summary>
        internal string BuildKey(ElementId elementId)
        {
            long linkPart = LinkInstance?.Id == null ? 0L : LinkInstance.Id.LongValue();
            long elementPart = elementId == null ? -1L : elementId.LongValue();

            return string.Concat(
                linkPart.ToString(CultureInfo.InvariantCulture),
                ":",
                elementPart.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The stable representation of a HOST reference, used to recognise a dimension that already
        /// exists. Must be built against the host document or a linked reference never matches.
        /// </summary>
        internal static string GetStableKey(Document hostDocument, Reference hostReference)
        {
            if (hostDocument == null || hostReference == null)
                return string.Empty;

            try
            {
                return hostReference.ConvertToStableRepresentation(hostDocument);
            }
            catch
            {
                // Deliberately empty rather than falling back to the element id: an id-only key
                // collapses every face on one element to the same value, which silently drops all
                // but one face during de-duplication.
                return string.Empty;
            }
        }

        /// <summary>
        /// The (link instance id, element id) pair a dimension reference actually points at.
        /// For a link reference, Reference.ElementId is the RevitLinkInstance and the real element
        /// is Reference.LinkedElementId - comparing the wrong one makes every linked element look
        /// like the same element.
        /// </summary>
        internal static string GetReferenceTargetKey(Reference reference)
        {
            if (reference == null)
                return string.Empty;

            ElementId ownerId = null;
            ElementId targetId = null;

            try
            {
                ownerId = reference.ElementId;
                targetId = reference.LinkedElementId;
            }
            catch
            {
                return string.Empty;
            }

            bool isLinkReference = targetId != null && targetId != ElementId.InvalidElementId;

            long linkPart = isLinkReference && ownerId != null ? ownerId.LongValue() : 0L;
            long elementPart = isLinkReference
                ? targetId.LongValue()
                : (ownerId == null ? -1L : ownerId.LongValue());

            return string.Concat(
                linkPart.ToString(CultureInfo.InvariantCulture),
                ":",
                elementPart.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Resolves a reference the user picked. When the pick landed inside a Revit link this returns
        /// the link's source and the element inside it; otherwise the host source and the host element.
        /// Returns false when nothing usable could be resolved.
        /// </summary>
        internal static bool TryResolvePickedReference(
            Document hostDocument,
            Reference pickedReference,
            out DimensionSource source,
            out Element element)
        {
            source = null;
            element = null;

            if (hostDocument == null || pickedReference == null)
                return false;

            ElementId linkedElementId;
            try
            {
                linkedElementId = pickedReference.LinkedElementId;
            }
            catch
            {
                linkedElementId = ElementId.InvalidElementId;
            }

            bool isLinkPick = linkedElementId != null && linkedElementId != ElementId.InvalidElementId;

            if (!isLinkPick)
            {
                try
                {
                    element = hostDocument.GetElement(pickedReference);
                }
                catch
                {
                    element = null;
                }

                if (element == null)
                    return false;

                source = ForHost(hostDocument);
                return true;
            }

            RevitLinkInstance linkInstance;
            try
            {
                linkInstance = hostDocument.GetElement(pickedReference.ElementId) as RevitLinkInstance;
            }
            catch
            {
                linkInstance = null;
            }

            if (linkInstance == null)
                return false;

            source = ForLink(hostDocument, linkInstance);
            if (source == null)
                return false;

            try
            {
                element = source.Document.GetElement(linkedElementId);
            }
            catch
            {
                element = null;
            }

            return element != null;
        }

        /// <summary>
        /// The link's display name: its document title, falling back to the instance name, truncated at
        /// the first ':' which Revit uses to append the position label.
        /// </summary>
        private static string GetCleanLinkName(RevitLinkInstance linkInstance, Document linkDocument)
        {
            string name = null;

            try
            {
                name = linkDocument?.Title;
            }
            catch
            {
                name = null;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    name = linkInstance?.Name;
                }
                catch
                {
                    name = null;
                }
            }

            if (string.IsNullOrWhiteSpace(name))
                return "Linked Model";

            int separator = name.IndexOf(':');
            if (separator > 0)
                name = name.Substring(0, separator);

            return name.Trim();
        }
    }
}
