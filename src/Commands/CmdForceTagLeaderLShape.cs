// Tool Name: Force Tag Leader L-Shape
// Description: Forces selected tags to use a right-angle (L-shaped) leader elbow, toggling sides on repeat runs.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Forces tags to use a right-angle leader by editing the leader elbow position.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdForceTagLeaderLShape : IExternalCommand
    {
        private const double ElbowNudgeFeet = 0.1;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            if (doc.IsReadOnly)
            {
                DialogHelper.ShowError("L-Shape Tag Leaders", "The document is read-only.");
                return Result.Cancelled;
            }

            IList<Element> preselected = CollectPreselectedTags(doc, uidoc.Selection.GetElementIds());
            if (preselected.Count > 0)
            {
                int updated;
                int skipped;
                ApplyToSelection(doc, preselected, out updated, out skipped);

                if (updated == 0)
                {
                    DialogHelper.ShowError("L-Shape Tag Leaders", "No editable tag leaders were found in the selection.");
                    return Result.Cancelled;
                }

                if (skipped > 0)
                {
                    DialogHelper.ShowInfo(
                        "L-Shape Tag Leaders",
                        $"Updated {updated} tag(s). Skipped {skipped}.");
                }

                return Result.Succeeded;
            }

            int changedCount = 0;
            int skippedCount = 0;
            bool hadPick = false;
            TagLeaderSelectionFilter filter = new TagLeaderSelectionFilter();
            const string prompt = "Pick tag to force L-shaped leader (Esc to finish)";

            while (true)
            {
                Reference picked;
                try
                {
                    picked = uidoc.Selection.PickObject(ObjectType.Element, filter, prompt);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                hadPick = true;
                Element tag = doc.GetElement(picked);
                if (tag == null)
                    continue;

                bool ok;
                using (Transaction t = new Transaction(doc, "Force L-Shaped Tag Leader"))
                {
                    t.Start();
                    ok = TryForceLShape(tag);
                    if (ok)
                    {
                        t.Commit();
                    }
                    else
                    {
                        t.RollBack();
                    }
                }

                if (ok)
                    changedCount++;
                else
                    skippedCount++;
            }

            if (changedCount == 0)
            {
                if (hadPick && skippedCount > 0)
                {
                    DialogHelper.ShowError(
                        "L-Shape Tag Leaders",
                        "The selected tag(s) did not allow a leader elbow edit.");
                }

                return Result.Cancelled;
            }

            if (skippedCount > 0)
            {
                DialogHelper.ShowInfo(
                    "L-Shape Tag Leaders",
                    $"Updated {changedCount} tag(s). Skipped {skippedCount}.");
            }

            return Result.Succeeded;
        }

        private static IList<Element> CollectPreselectedTags(Document doc, ICollection<ElementId> selectedIds)
        {
            List<Element> results = new List<Element>();
            if (selectedIds == null || selectedIds.Count == 0)
                return results;

            foreach (ElementId id in selectedIds)
            {
                Element element = doc.GetElement(id);
                if (element == null)
                    continue;

                if (HasProperty(element, "TagHeadPosition"))
                    results.Add(element);
            }

            return results;
        }

        private static void ApplyToSelection(Document doc, IList<Element> tags, out int updated, out int skipped)
        {
            updated = 0;
            skipped = 0;

            using (Transaction t = new Transaction(doc, "Force L-Shaped Tag Leaders"))
            {
                t.Start();

                foreach (Element tag in tags)
                {
                    if (TryForceLShape(tag))
                        updated++;
                    else
                        skipped++;
                }

                t.Commit();
            }
        }

        private static bool TryForceLShape(Element tag)
        {
            if (tag == null)
                return false;

            if (!EnsureLeaderEnabled(tag))
                return false;

            TryForceLeaderEndFree(tag);

            if (!TryGetXYZProperty(tag, "TagHeadPosition", out XYZ head))
                return false;

            if (!TryGetXYZProperty(tag, "LeaderEnd", out XYZ end))
                return false;

            if (!HasWritableProperty(tag, "LeaderElbow"))
                return false;

            XYZ currentElbow = null;
            TryGetXYZProperty(tag, "LeaderElbow", out currentElbow);
            XYZ elbow = ChooseElbow(head, end, currentElbow);
            return TrySetXYZProperty(tag, "LeaderElbow", elbow);
        }

        private static XYZ ChooseElbow(XYZ head, XYZ end, XYZ currentElbow)
        {
            XYZ elbow1 = new XYZ(head.X, end.Y, end.Z);
            XYZ elbow2 = new XYZ(end.X, head.Y, end.Z);

            XYZ elbow = null;
            if (currentElbow != null)
            {
                double d1 = currentElbow.DistanceTo(elbow1);
                double d2 = currentElbow.DistanceTo(elbow2);

                if (d1 <= Constants.MIN_DISTANCE_TOLERANCE && d2 > Constants.MIN_DISTANCE_TOLERANCE)
                {
                    elbow = elbow2;
                }
                else if (d2 <= Constants.MIN_DISTANCE_TOLERANCE && d1 > Constants.MIN_DISTANCE_TOLERANCE)
                {
                    elbow = elbow1;
                }
            }

            if (elbow == null)
            {
                elbow = elbow1;
                if (IsCollinear(head, elbow, end))
                    elbow = elbow2;
            }

            if (IsCollinear(head, elbow, end))
                elbow = new XYZ(elbow.X + ElbowNudgeFeet, elbow.Y, elbow.Z);

            return elbow;
        }

        private static bool IsCollinear(XYZ p1, XYZ p2, XYZ p3)
        {
            if (p1 == null || p2 == null || p3 == null)
                return false;

            XYZ v1 = p2 - p1;
            XYZ v2 = p3 - p1;
            XYZ cross = v1.CrossProduct(v2);
            return cross.GetLength() < Constants.ZERO_LENGTH_TOLERANCE;
        }

        private static bool EnsureLeaderEnabled(Element tag)
        {
            if (!TryGetBoolProperty(tag, "HasLeader", out bool hasLeader))
                return true;

            if (hasLeader)
                return true;

            return TrySetBoolProperty(tag, "HasLeader", true);
        }

        private static void TryForceLeaderEndFree(Element tag)
        {
            PropertyInfo prop = GetProperty(tag, "LeaderEndCondition");
            if (prop == null || !prop.CanWrite)
                return;

            try
            {
                object current = prop.GetValue(tag, null);
                if (current != null && current.ToString().IndexOf("Free", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                if (prop.PropertyType == typeof(LeaderEndCondition))
                {
                    prop.SetValue(tag, LeaderEndCondition.Free, null);
                    return;
                }

                if (prop.PropertyType.IsEnum)
                {
                    foreach (object value in Enum.GetValues(prop.PropertyType))
                    {
                        if (string.Equals(value.ToString(), "Free", StringComparison.OrdinalIgnoreCase))
                        {
                            prop.SetValue(tag, value, null);
                            return;
                        }
                    }
                }

                prop.SetValue(tag, 0, null);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static PropertyInfo GetProperty(Element element, string propertyName)
        {
            if (element == null || string.IsNullOrEmpty(propertyName))
                return null;

            return element.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        }

        private static bool HasProperty(Element element, string propertyName)
        {
            return GetProperty(element, propertyName) != null;
        }

        private static bool HasWritableProperty(Element element, string propertyName)
        {
            PropertyInfo prop = GetProperty(element, propertyName);
            return prop != null && prop.CanWrite;
        }

        private static bool TryGetXYZProperty(Element element, string propertyName, out XYZ value)
        {
            value = null;
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null)
                return false;

            try
            {
                object raw = prop.GetValue(element, null);
                if (raw is XYZ xyz)
                {
                    value = xyz;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TrySetXYZProperty(Element element, string propertyName, XYZ value)
        {
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null || !prop.CanWrite)
                return false;

            try
            {
                prop.SetValue(element, value, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetBoolProperty(Element element, string propertyName, out bool value)
        {
            value = false;
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null)
                return false;

            try
            {
                object raw = prop.GetValue(element, null);
                if (raw is bool flag)
                {
                    value = flag;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TrySetBoolProperty(Element element, string propertyName, bool value)
        {
            PropertyInfo prop = GetProperty(element, propertyName);
            if (prop == null || !prop.CanWrite)
                return false;

            try
            {
                prop.SetValue(element, value, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private class TagLeaderSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return HasProperty(elem, "TagHeadPosition");
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
