// Tool Name: Intelligent Tag Arranger Service
// Description: Converts the pyRevit intelligent tag arranger workflow into native C#.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.LeaderLogic, AJTools.Utils

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Services.LeaderLogic;
using AJTools.Utils;

namespace AJTools.Services.TagArrange
{
    /// <summary>
    /// Rearranges selected tags using nearest-first assignment based on T1/L1 distance.
    /// </summary>
    internal static class IntelligentTagArrangerService
    {
        private const string ToolTitle = "Intelligent Tag Arranger";

        private sealed class TagData
        {
            public TagData(IndependentTag tag, XYZ originalLeaderStart)
            {
                Tag = tag;
                OriginalLeaderStart = originalLeaderStart;
            }

            public IndependentTag Tag { get; private set; }
            public XYZ OriginalLeaderStart { get; private set; }
        }

        internal static Result Execute(ExternalCommandData commandData, ref string message)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            if (doc.IsReadOnly)
            {
                DialogHelper.ShowError(ToolTitle, "The document is read-only.");
                return Result.Cancelled;
            }

            View activeView = doc.ActiveView;
            if (activeView == null || activeView.IsTemplate)
            {
                DialogHelper.ShowError(ToolTitle, "Please run this tool in a normal project view.");
                return Result.Cancelled;
            }

            List<IndependentTag> selectedTags = CollectSelectedTags(doc, uidoc.Selection.GetElementIds());
            if (selectedTags.Count < 2)
            {
                DialogHelper.ShowError(ToolTitle, "Please select at least two tags to arrange.");
                return Result.Cancelled;
            }

            int selectedCount = selectedTags.Count;
            List<TagData> allTags = BuildTagData(doc, selectedTags);
            if (allTags.Count < 2)
            {
                DialogHelper.ShowError(
                    ToolTitle,
                    $"You selected {selectedCount} tag(s), but only {allTags.Count} tag(s) had a readable existing leader start (L1).\n" +
                    "This tool only uses each tag's current L1 and will not replace it.");
                return Result.Cancelled;
            }

            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);
            int viewScale = Math.Max(activeView.Scale, 1);
            double spacingMm = TagArrangeSettings.GetTagSpacingMm();
            double verticalOffset = spacingMm * Constants.MM_TO_FEET * viewScale;

            bool hadCommit = false;
            using (TransactionGroup tg = new TransactionGroup(doc, "AJ Tools - Arrange Tags"))
            {
                tg.Start();

                while (true)
                {
                    XYZ basePointModel;
                    try
                    {
                        basePointModel = uidoc.Selection.PickPoint("Click a base location (Press ESC when satisfied)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    using (Transaction t = new Transaction(doc, "Try Arrangement"))
                    {
                        t.Start();
                        try
                        {
                            bool ok = TryArrangeAtPoint(doc, activeView, leaderLogic, allTags, basePointModel, verticalOffset);
                            if (ok)
                            {
                                t.Commit();
                                hadCommit = true;
                            }
                            else
                            {
                                t.RollBack();
                            }
                        }
                        catch (Exception)
                        {
                            if (t.GetStatus() != TransactionStatus.RolledBack && t.GetStatus() != TransactionStatus.Committed)
                            {
                                t.RollBack();
                            }
                            break;
                        }
                    }
                }

                if (hadCommit)
                    tg.Assimilate();
                else
                    tg.RollBack();
            }

            return hadCommit ? Result.Succeeded : Result.Cancelled;
        }

        private static List<IndependentTag> CollectSelectedTags(Document doc, ICollection<ElementId> selectedIds)
        {
            List<IndependentTag> tags = new List<IndependentTag>();
            if (selectedIds == null || selectedIds.Count == 0)
                return tags;

            foreach (ElementId id in selectedIds)
            {
                IndependentTag tag = doc.GetElement(id) as IndependentTag;
                if (tag != null)
                    tags.Add(tag);
            }

            return tags;
        }

        private static List<TagData> BuildTagData(Document doc, IList<IndependentTag> selectedTags)
        {
            List<TagData> data = new List<TagData>();
            if (doc == null || selectedTags == null)
                return data;

            List<IndependentTag> unresolved = new List<IndependentTag>();

            foreach (IndependentTag tag in selectedTags)
            {
                if (tag == null)
                    continue;

                XYZ existingL1 = TryGetLeaderEnd(tag);
                if (existingL1 == null)
                {
                    unresolved.Add(tag);
                    continue;
                }

                data.Add(new TagData(tag, existingL1));
            }

            if (unresolved.Count > 0)
                TryResolveLeaderStartsByRollbackProbe(doc, unresolved, data);

            return data;
        }

        private static void TryResolveLeaderStartsByRollbackProbe(
            Document doc,
            IList<IndependentTag> unresolved,
            IList<TagData> output)
        {
            if (doc == null || unresolved == null || unresolved.Count == 0 || output == null)
                return;

            using (Transaction t = new Transaction(doc, "Probe Tag Leader Starts"))
            {
                t.Start();

                foreach (IndependentTag tag in unresolved)
                {
                    if (tag == null || !tag.IsValidObject)
                        continue;

                    using (SubTransaction st = new SubTransaction(doc))
                    {
                        st.Start();

                        XYZ probed = null;
                        try
                        {
                            if (TrySetLeaderEndCondition(tag, LeaderEndCondition.Free))
                                probed = TryGetLeaderEnd(tag);
                        }
                        catch
                        {
                        }

                        st.RollBack();

                        if (probed != null)
                            output.Add(new TagData(tag, probed));
                    }
                }

                t.RollBack();
            }
        }

        private static bool TryArrangeAtPoint(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            IList<TagData> allTags,
            XYZ basePointModel,
            double verticalOffset)
        {
            if (doc == null || activeView == null || leaderLogic == null || basePointModel == null || allTags == null)
                return false;

            List<TagData> remaining = new List<TagData>();
            foreach (TagData data in allTags)
            {
                if (data != null && data.Tag != null && data.Tag.IsValidObject && data.OriginalLeaderStart != null)
                    remaining.Add(data);
            }

            if (remaining.Count < 2)
                return false;

            UV basePointView = leaderLogic.ProjectToView(basePointModel);
            double baseXView = basePointView.U;

            TagData first = FindNearestTagForTarget(remaining, leaderLogic, basePointModel);
            if (first == null)
                return false;

            XYZ firstAnchor = first.OriginalLeaderStart;
            if (firstAnchor == null)
                return false;

            bool stackUp = IsT1AboveL1(basePointModel, firstAnchor, leaderLogic);
            if (!TryMoveTag(first, doc, activeView, leaderLogic, basePointModel, baseXView))
                return false;

            remaining.Remove(first);

            XYZ viewUp = activeView.UpDirection;
            if (viewUp == null || viewUp.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                viewUp = XYZ.BasisY;
            else
                viewUp = viewUp.Normalize();

            XYZ stepDirection = stackUp ? viewUp : -viewUp;
            XYZ lastPosition = basePointModel;

            while (remaining.Count > 0)
            {
                XYZ nextSlot = lastPosition.Add(stepDirection.Multiply(verticalOffset));
                TagData next = FindNearestTagForTarget(remaining, leaderLogic, nextSlot);
                if (next == null)
                    return false;

                if (!TryMoveTag(next, doc, activeView, leaderLogic, nextSlot, baseXView))
                    return false;

                remaining.Remove(next);
                lastPosition = nextSlot;
            }

            return true;
        }

        private static TagData FindNearestTagForTarget(
            IList<TagData> candidates,
            LeaderLogicService leaderLogic,
            XYZ targetT1)
        {
            if (candidates == null || candidates.Count == 0 || leaderLogic == null || targetT1 == null)
                return null;

            TagData nearest = null;
            double minDistance = double.MaxValue;

            foreach (TagData data in candidates)
            {
                XYZ l1 = data != null ? data.OriginalLeaderStart : null;
                if (l1 == null)
                    continue;

                double distance = DistanceInView(targetT1, l1, leaderLogic);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = data;
                }
            }

            return nearest ?? candidates[0];
        }

        private static double DistanceInView(XYZ p1, XYZ p2, LeaderLogicService leaderLogic)
        {
            UV uv1 = leaderLogic.ProjectToView(p1);
            UV uv2 = leaderLogic.ProjectToView(p2);
            double dx = uv1.U - uv2.U;
            double dy = uv1.V - uv2.V;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static bool IsT1AboveL1(XYZ t1Model, XYZ l1Model, LeaderLogicService leaderLogic)
        {
            UV t1 = leaderLogic.ProjectToView(t1Model);
            UV l1 = leaderLogic.ProjectToView(l1Model);
            return t1.V > l1.V;
        }

        private static bool TryMoveTag(
            TagData data,
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            XYZ targetModel,
            double baseXView)
        {
            if (data == null || data.Tag == null || doc == null || leaderLogic == null || targetModel == null)
                return false;

            IndependentTag tag = data.Tag;
            if (tag.Pinned)
                return false;

            XYZ finalTarget = AlignToBaseX(targetModel, baseXView, leaderLogic);
            if (finalTarget == null)
                return false;

            XYZ currentHead;
            try
            {
                currentHead = tag.TagHeadPosition;
            }
            catch
            {
                return false;
            }

            if (currentHead == null)
                return false;

            XYZ move = finalTarget - currentHead;
            if (move.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
            {
                try
                {
                    ElementTransformUtils.MoveElement(doc, tag.Id, move);
                }
                catch
                {
                    return false;
                }
            }

            XYZ finalHead = finalTarget;
            try
            {
                XYZ refreshed = tag.TagHeadPosition;
                if (refreshed != null)
                    finalHead = refreshed;
            }
            catch
            {
            }

            TryEnsureLeaderEnabled(tag);
            if (!TryHasLeader(tag))
                return true;

            XYZ leaderEnd = data.OriginalLeaderStart;
            if (leaderEnd == null)
                return false;

            return TryApplyLShapeLeader(tag, finalHead, leaderEnd, leaderLogic);
        }

        private static XYZ AlignToBaseX(XYZ pointModel, double baseXView, LeaderLogicService leaderLogic)
        {
            if (pointModel == null || leaderLogic == null)
                return null;

            UV uv = leaderLogic.ProjectToView(pointModel);
            double deltaX = baseXView - uv.U;
            return leaderLogic.OffsetInView(pointModel, deltaX, 0);
        }

        private static XYZ TryGetLeaderEnd(IndependentTag tag)
        {
            if (tag == null)
                return null;

            try
            {
                if (!tag.HasLeader)
                    return null;
            }
            catch
            {
                return null;
            }

            try
            {
                XYZ direct = tag.LeaderEnd;
                if (direct != null)
                    return direct;
            }
            catch
            {
            }

            XYZ byTaggedReference = TryGetLeaderEndFromTaggedReference(tag);
            if (byTaggedReference != null)
                return byTaggedReference;

            XYZ byTaggedReferences = TryGetLeaderEndFromTaggedReferences(tag);
            if (byTaggedReferences != null)
                return byTaggedReferences;

            return TryGetXYZProperty(tag, "LeaderEnd");
        }

        private static XYZ TryGetLeaderEndFromTaggedReference(IndependentTag tag)
        {
            if (tag == null)
                return null;

            try
            {
                Reference taggedReference = tag.GetTaggedReference();
                if (taggedReference == null)
                    return null;

                return InvokeGetLeaderEnd(tag, taggedReference);
            }
            catch
            {
                return null;
            }
        }

        private static XYZ TryGetLeaderEndFromTaggedReferences(IndependentTag tag)
        {
            if (tag == null)
                return null;

            try
            {
                MethodInfo method = tag.GetType().GetMethod("GetTaggedReferences", BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                    return null;

                object refsRaw = method.Invoke(tag, null);
                IEnumerable refs = refsRaw as IEnumerable;
                if (refs == null)
                    return null;

                foreach (object item in refs)
                {
                    Reference reference = item as Reference;
                    if (reference == null)
                        continue;

                    XYZ end = InvokeGetLeaderEnd(tag, reference);
                    if (end != null)
                        return end;
                }
            }
            catch
            {
            }

            return null;
        }

        private static XYZ InvokeGetLeaderEnd(IndependentTag tag, Reference reference)
        {
            if (tag == null || reference == null)
                return null;

            try
            {
                MethodInfo[] methods = tag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (MethodInfo method in methods)
                {
                    if (!string.Equals(method.Name, "GetLeaderEnd", StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Reference))
                        continue;

                    object result = method.Invoke(tag, new object[] { reference });
                    XYZ xyz = result as XYZ;
                    if (xyz != null)
                        return xyz;
                }
            }
            catch
            {
            }

            return null;
        }

        private static XYZ TryGetXYZProperty(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            try
            {
                PropertyInfo prop = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null)
                    return null;

                object raw = prop.GetValue(instance, null);
                return raw as XYZ;
            }
            catch
            {
                return null;
            }
        }

        private static void TryEnsureLeaderEnabled(IndependentTag tag)
        {
            if (tag == null)
                return;

            try
            {
                if (!tag.HasLeader)
                    tag.HasLeader = true;
            }
            catch
            {
            }
        }

        private static bool TryHasLeader(IndependentTag tag)
        {
            if (tag == null)
                return false;

            try
            {
                return tag.HasLeader;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryApplyLShapeLeader(
            IndependentTag tag,
            XYZ headModel,
            XYZ leaderEndModel,
            LeaderLogicService leaderLogic)
        {
            if (tag == null || headModel == null || leaderEndModel == null || leaderLogic == null)
                return false;

            XYZ elbow = leaderLogic.ComputeElbow(headModel, leaderEndModel);
            if (elbow == null)
                return true;

            // Keep L1 exactly as-is: do not toggle leader end condition as fallback.
            return TrySetLeaderElbow(tag, elbow);
        }

        private static bool TrySetLeaderElbow(IndependentTag tag, XYZ elbow)
        {
            if (tag == null || elbow == null)
                return false;

            try
            {
                tag.LeaderElbow = elbow;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetLeaderEndCondition(IndependentTag tag, LeaderEndCondition condition)
        {
            if (tag == null)
                return false;

            try
            {
                if (tag.LeaderEndCondition == condition)
                    return true;
            }
            catch
            {
            }

            try
            {
                if (tag.CanLeaderEndConditionBeAssigned(condition))
                {
                    tag.LeaderEndCondition = condition;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

    }
}
