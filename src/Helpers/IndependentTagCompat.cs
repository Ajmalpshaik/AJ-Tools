using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    internal static class IndependentTagCompat
    {
        internal static IList<ElementId> GetTaggedLocalElementIds(IndependentTag tag)
        {
            var ids = new List<ElementId>();
            if (tag == null)
            {
                return ids;
            }

            try
            {
#if REVIT2022_OR_GREATER
                ICollection<ElementId> localIds = tag.GetTaggedLocalElementIds();
                if (localIds != null)
                {
                    ids.AddRange(localIds.Where(IsValidElementId));
                }
#else
                ElementId id = tag.TaggedLocalElementId;
                if (IsValidElementId(id))
                {
                    ids.Add(id);
                }
#endif
            }
            catch
            {
            }

            return ids;
        }

        internal static ElementId GetTaggedLocalElementId(IndependentTag tag)
        {
            IList<ElementId> ids = GetTaggedLocalElementIds(tag);
            return ids.Count > 0 ? ids[0] : ElementId.InvalidElementId;
        }

        internal static IList<Reference> GetTaggedReferences(IndependentTag tag)
        {
            var references = new List<Reference>();
            if (tag == null)
            {
                return references;
            }

            try
            {
#if REVIT2022_OR_GREATER
                ICollection<Reference> taggedReferences = tag.GetTaggedReferences();
                if (taggedReferences != null)
                {
                    references.AddRange(taggedReferences.Where(reference => reference != null));
                }
#else
                Reference reference = tag.GetTaggedReference();
                if (reference != null)
                {
                    references.Add(reference);
                }
#endif
            }
            catch
            {
            }

            return references;
        }

        internal static Reference GetTaggedReference(IndependentTag tag)
        {
            IList<Reference> references = GetTaggedReferences(tag);
            return references.Count > 0 ? references[0] : null;
        }

        internal static XYZ GetLeaderEnd(IndependentTag tag)
        {
            if (tag == null)
            {
                return null;
            }

            try
            {
                if (!tag.HasLeader)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

#if REVIT2022_OR_GREATER
            foreach (Reference reference in GetTaggedReferences(tag))
            {
                try
                {
                    XYZ end = tag.GetLeaderEnd(reference);
                    if (end != null)
                    {
                        return end;
                    }
                }
                catch
                {
                }
            }

            return null;
#else
            try
            {
                return tag.LeaderEnd;
            }
            catch
            {
                return null;
            }
#endif
        }

        internal static bool SetLeaderElbow(IndependentTag tag, XYZ elbow)
        {
            if (tag == null || elbow == null)
            {
                return false;
            }

#if REVIT2022_OR_GREATER
            foreach (Reference reference in GetTaggedReferences(tag))
            {
                try
                {
                    tag.SetLeaderElbow(reference, elbow);
                    return true;
                }
                catch
                {
                }
            }

            return false;
#else
            try
            {
                tag.LeaderElbow = elbow;
                return true;
            }
            catch
            {
                return false;
            }
#endif
        }

        private static bool IsValidElementId(ElementId id)
        {
            return id != null && id != ElementId.InvalidElementId;
        }
    }
}
