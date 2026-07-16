#region Metadata
/*
 * Tool Name     : AJ Tools Shared Helper
 * File Name     : TagCompat.cs
 * Purpose       : Version-safe access to IndependentTag leader/tagged-reference members across Revit 2020 -> 2027.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-07
 * Last Updated  : 2026-07-07
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / .NET Fx 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027 - verify SDK)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : IndependentTag.
 * Output        : Leader end point, tagged reference, tagged element id, and leader elbow set - version-safe.
 *
 * Notes         :
 * - Revit 2020-2022 exposed single-reference members: LeaderEnd, LeaderElbow, GetTaggedReference(),
 *   TaggedLocalElementId. Revit 2023 REMOVED them in favour of the multi-reference API introduced in 2022:
 *   GetLeaderEnd(ref), SetLeaderElbow(ref, xyz), GetTaggedReferences(), GetTaggedLocalElements().
 * - For a single-reference tag (the normal case) we operate on the first tagged reference, preserving
 *   the exact behaviour of the old single-reference members.
 * - LeaderEndCondition and HasLeader are unchanged across versions and are used directly at call sites.
 * - Every version branch lives ONLY in this file so tag tools never branch on Revit version.
 *
 * Changelog     :
 * v1.0.0 (2026-07-07) - Initial release: IndependentTag single/multi-reference bridge for Revit 2020 -> 2027.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Bridges the IndependentTag single-reference members (Revit 2020-2022) and the multi-reference
    /// API (Revit 2023+), so tag tools keep one code path across every supported Revit version.
    /// </summary>
    internal static class TagCompat
    {
#if REVIT2023_OR_GREATER
        /// <summary>First tagged reference of the tag, or null when the tag has none.</summary>
        private static Reference FirstReference(IndependentTag tag)
        {
            System.Collections.Generic.IList<Reference> references = tag.GetTaggedReferences();
            return (references != null && references.Count > 0) ? references[0] : null;
        }
#endif

        /// <summary>The tag's leader end point (Free-leader tags). Mirrors the old IndependentTag.LeaderEnd.</summary>
        internal static XYZ GetLeaderEnd(IndependentTag tag)
        {
#if REVIT2023_OR_GREATER
            Reference reference = FirstReference(tag);
            return reference == null ? null : tag.GetLeaderEnd(reference);
#else
            return tag.LeaderEnd;
#endif
        }

        /// <summary>Sets the tag's leader elbow point. Mirrors the old IndependentTag.LeaderElbow setter.</summary>
        internal static void SetLeaderElbow(IndependentTag tag, XYZ elbow)
        {
#if REVIT2023_OR_GREATER
            Reference reference = FirstReference(tag);
            if (reference != null)
                tag.SetLeaderElbow(reference, elbow);
#else
            tag.LeaderElbow = elbow;
#endif
        }

        /// <summary>The tag's first tagged reference. Mirrors the old IndependentTag.GetTaggedReference().</summary>
        internal static Reference GetTaggedReference(IndependentTag tag)
        {
#if REVIT2023_OR_GREATER
            return FirstReference(tag);
#else
            return tag.GetTaggedReference();
#endif
        }

        /// <summary>The element id of the first tagged local element. Mirrors the old IndependentTag.TaggedLocalElementId.</summary>
        internal static ElementId GetTaggedLocalElementId(IndependentTag tag)
        {
#if REVIT2023_OR_GREATER
            foreach (Element element in tag.GetTaggedLocalElements())
            {
                if (element != null)
                    return element.Id;
            }
            return ElementId.InvalidElementId;
#else
            return tag.TaggedLocalElementId;
#endif
        }
    }
}
