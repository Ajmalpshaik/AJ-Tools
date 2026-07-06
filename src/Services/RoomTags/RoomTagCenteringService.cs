using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.RoomTags
{
    /// <summary>
    /// Centers room tag heads on their tagged room in the active view.
    /// </summary>
    internal static class RoomTagCenteringService
    {
        private const string ToolTitle = "Center Room Tags";
        private const double MinAreaTolerance = 1e-9;
        private const double PointToleranceFeet = 1e-4;
        private const int InteriorGridDivisions = 12;

        private sealed class TaggedRoomContext
        {
            public TaggedRoomContext(Room room, Transform transformToHost)
            {
                Room = room;
                TransformToHost = transformToHost ?? Transform.Identity;
            }

            public Room Room { get; private set; }
            public Transform TransformToHost { get; private set; }
        }

        private sealed class CenteringSummary
        {
            public int Total { get; set; }
            public int Moved { get; set; }
            public int AlreadyCentered { get; set; }
            public int SkippedPinned { get; set; }
            public int SkippedOrphaned { get; set; }
            public int SkippedNoRoom { get; set; }
            public int SkippedNoCenter { get; set; }
            public int SkippedFailed { get; set; }

            public int SkippedTotal
            {
                get
                {
                    return SkippedPinned + SkippedOrphaned + SkippedNoRoom + SkippedNoCenter + SkippedFailed;
                }
            }
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

            IList<RoomTag> tags = CollectRoomTagsInView(doc, activeView);
            if (tags.Count == 0)
            {
                DialogHelper.ShowInfo(ToolTitle, "No room tags were found in the active view.");
                return Result.Cancelled;
            }

            CenteringSummary summary = new CenteringSummary { Total = tags.Count };

            using (Transaction transaction = new Transaction(doc, "Center Room Tags"))
            {
                transaction.Start();

                foreach (RoomTag tag in tags)
                {
                    ProcessTag(doc, activeView, tag, summary);
                }

                if (summary.Moved > 0)
                    transaction.Commit();
                else
                    transaction.RollBack();
            }

            ShowSummary(summary);

            if (summary.Moved > 0 || summary.AlreadyCentered == summary.Total)
                return Result.Succeeded;

            return Result.Cancelled;
        }

        private static IList<RoomTag> CollectRoomTagsInView(Document doc, View activeView)
        {
            return new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(SpatialElementTag))
                .WhereElementIsNotElementType()
                .Cast<SpatialElementTag>()
                .Select(tag => tag as RoomTag)
                .Where(tag => tag != null && tag.IsValidObject)
                .ToList();
        }

        private static void ProcessTag(Document hostDoc, View activeView, RoomTag tag, CenteringSummary summary)
        {
            if (tag == null || !tag.IsValidObject)
            {
                summary.SkippedFailed++;
                return;
            }

            if (tag.Pinned)
            {
                summary.SkippedPinned++;
                return;
            }

            if (IsOrphaned(tag))
            {
                summary.SkippedOrphaned++;
                return;
            }

            TaggedRoomContext roomContext = ResolveTaggedRoom(hostDoc, tag);
            if (roomContext == null || roomContext.Room == null || !roomContext.Room.IsValidObject)
            {
                summary.SkippedNoRoom++;
                return;
            }

            XYZ roomCenter;
            if (!TryGetRoomCenter(roomContext.Room, out roomCenter))
            {
                summary.SkippedNoCenter++;
                return;
            }

            XYZ currentHead = TryGetTagHeadPosition(tag);
            if (currentHead == null)
            {
                summary.SkippedFailed++;
                return;
            }

            XYZ centerInHost = roomContext.TransformToHost.OfPoint(roomCenter);
            XYZ target = PreserveViewDepth(activeView, currentHead, centerInHost);
            if (IsSamePoint(currentHead, target))
            {
                summary.AlreadyCentered++;
                return;
            }

            using (SubTransaction subTransaction = new SubTransaction(hostDoc))
            {
                subTransaction.Start();

                try
                {
                    tag.TagHeadPosition = target;
                    subTransaction.Commit();
                    summary.Moved++;
                }
                catch
                {
                    if (subTransaction.GetStatus() == TransactionStatus.Started)
                        subTransaction.RollBack();

                    summary.SkippedFailed++;
                }
            }
        }

        private static TaggedRoomContext ResolveTaggedRoom(Document hostDoc, RoomTag tag)
        {
            if (hostDoc == null || tag == null)
                return null;

            if (IsTaggingLink(tag))
                return ResolveLinkedRoom(hostDoc, tag);

            Room room = TryGetLocalRoom(hostDoc, tag);
            return room == null ? null : new TaggedRoomContext(room, Transform.Identity);
        }

        private static TaggedRoomContext ResolveLinkedRoom(Document hostDoc, RoomTag tag)
        {
            LinkElementId linkedRoomId;
            try
            {
                linkedRoomId = tag.TaggedRoomId;
            }
            catch
            {
                return null;
            }

            if (linkedRoomId == null || linkedRoomId.LinkInstanceId == ElementId.InvalidElementId)
                return null;

            RevitLinkInstance linkInstance = hostDoc.GetElement(linkedRoomId.LinkInstanceId) as RevitLinkInstance;
            if (linkInstance == null)
                return null;

            Document linkedDocument = linkInstance.GetLinkDocument();
            if (linkedDocument == null || linkedRoomId.LinkedElementId == ElementId.InvalidElementId)
                return null;

            Room linkedRoom = linkedDocument.GetElement(linkedRoomId.LinkedElementId) as Room;
            if (linkedRoom == null)
                return null;

            return new TaggedRoomContext(linkedRoom, linkInstance.GetTotalTransform());
        }

        private static Room TryGetLocalRoom(Document hostDoc, RoomTag tag)
        {
            try
            {
                if (tag.Room != null)
                    return tag.Room;
            }
            catch
            {
            }

            try
            {
                ElementId localRoomId = tag.TaggedLocalRoomId;
                if (localRoomId != ElementId.InvalidElementId)
                    return hostDoc.GetElement(localRoomId) as Room;
            }
            catch
            {
            }

            return null;
        }

        private static bool TryGetRoomCenter(Room room, out XYZ center)
        {
            center = null;

            if (room == null || !room.IsValidObject || !HasUsableArea(room))
                return false;

            if (TryGetBoundaryCentroid(room, out center))
                return true;

            if (TryGetBoundingBoxCenter(room, out center))
                return true;

            if (TryGetInteriorGridPoint(room, out center))
                return true;

            return TryGetLocationPoint(room, out center);
        }

        private static bool HasUsableArea(Room room)
        {
            try
            {
                return room.Area > MinAreaTolerance;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetBoundaryCentroid(Room room, out XYZ center)
        {
            center = null;

            IList<IList<BoundarySegment>> loops;
            try
            {
                loops = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
            }
            catch
            {
                return false;
            }

            if (loops == null || loops.Count == 0)
                return false;

            double crossTotal = 0.0;
            double xTotal = 0.0;
            double yTotal = 0.0;
            double zTotal = 0.0;
            double zWeightTotal = 0.0;

            foreach (IList<BoundarySegment> loop in loops)
            {
                List<XYZ> points = BuildLoopPoints(loop);
                if (points.Count < 3)
                    continue;

                double loopCross = 0.0;
                double loopX = 0.0;
                double loopY = 0.0;

                for (int i = 0; i < points.Count; i++)
                {
                    XYZ p1 = points[i];
                    XYZ p2 = points[(i + 1) % points.Count];
                    double cross = (p1.X * p2.Y) - (p2.X * p1.Y);

                    loopCross += cross;
                    loopX += (p1.X + p2.X) * cross;
                    loopY += (p1.Y + p2.Y) * cross;
                }

                if (Math.Abs(loopCross) < MinAreaTolerance)
                    continue;

                crossTotal += loopCross;
                xTotal += loopX;
                yTotal += loopY;

                double zWeight = Math.Abs(loopCross);
                zTotal += AverageZ(points) * zWeight;
                zWeightTotal += zWeight;
            }

            if (Math.Abs(crossTotal) < MinAreaTolerance)
                return false;

            double z = GetRoomInteriorZ(room, zWeightTotal > MinAreaTolerance ? zTotal / zWeightTotal : 0.0);
            center = new XYZ(xTotal / (3.0 * crossTotal), yTotal / (3.0 * crossTotal), z);
            return IsPointInRoom(room, center);
        }

        private static List<XYZ> BuildLoopPoints(IList<BoundarySegment> loop)
        {
            List<XYZ> points = new List<XYZ>();
            if (loop == null)
                return points;

            foreach (BoundarySegment segment in loop)
            {
                Curve curve = null;
                try
                {
                    curve = segment?.GetCurve();
                }
                catch
                {
                }

                if (curve == null)
                    continue;

                IList<XYZ> tessellated = null;
                try
                {
                    tessellated = curve.Tessellate();
                }
                catch
                {
                }

                if (tessellated == null || tessellated.Count == 0)
                {
                    AddPoint(points, curve.GetEndPoint(0));
                    AddPoint(points, curve.GetEndPoint(1));
                    continue;
                }

                foreach (XYZ point in tessellated)
                {
                    AddPoint(points, point);
                }
            }

            if (points.Count > 1 && IsSamePoint(points[0], points[points.Count - 1]))
                points.RemoveAt(points.Count - 1);

            return points;
        }

        private static void AddPoint(IList<XYZ> points, XYZ point)
        {
            if (points == null || point == null)
                return;

            if (points.Count > 0 && IsSamePoint(points[points.Count - 1], point))
                return;

            points.Add(point);
        }

        private static bool TryGetBoundingBoxCenter(Room room, out XYZ center)
        {
            center = null;
            BoundingBoxXYZ box = GetRoomBoundingBox(room);
            if (!IsUsableBox(box))
                return false;

            center = MidPoint(box.Min, box.Max);
            return IsPointInRoom(room, center);
        }

        private static bool TryGetInteriorGridPoint(Room room, out XYZ center)
        {
            center = null;
            BoundingBoxXYZ box = GetRoomBoundingBox(room);
            if (!IsUsableBox(box))
                return false;

            XYZ boxCenter = MidPoint(box.Min, box.Max);
            double bestDistance = double.MaxValue;
            XYZ bestPoint = null;

            for (int xIndex = 1; xIndex < InteriorGridDivisions; xIndex++)
            {
                double x = box.Min.X + ((box.Max.X - box.Min.X) * xIndex / InteriorGridDivisions);

                for (int yIndex = 1; yIndex < InteriorGridDivisions; yIndex++)
                {
                    double y = box.Min.Y + ((box.Max.Y - box.Min.Y) * yIndex / InteriorGridDivisions);
                    XYZ candidate = new XYZ(x, y, boxCenter.Z);
                    if (!IsPointInRoom(room, candidate))
                        continue;

                    double distance = DistanceSquared2D(candidate, boxCenter);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPoint = candidate;
                    }
                }
            }

            if (bestPoint == null)
                return false;

            center = bestPoint;
            return true;
        }

        private static bool TryGetLocationPoint(Room room, out XYZ center)
        {
            center = null;

            LocationPoint locationPoint = room.Location as LocationPoint;
            if (locationPoint == null || locationPoint.Point == null)
                return false;

            if (!IsPointInRoom(room, locationPoint.Point))
                return false;

            center = locationPoint.Point;
            return true;
        }

        private static XYZ TryGetTagHeadPosition(RoomTag tag)
        {
            try
            {
                return tag.TagHeadPosition;
            }
            catch
            {
                return null;
            }
        }

        private static XYZ PreserveViewDepth(View activeView, XYZ currentHead, XYZ target)
        {
            if (activeView == null || currentHead == null || target == null)
                return target;

            XYZ viewDirection = activeView.ViewDirection;
            if (viewDirection == null || viewDirection.GetLength() <= MinAreaTolerance)
                return target;

            viewDirection = viewDirection.Normalize();
            double depthOffset = (currentHead - target).DotProduct(viewDirection);
            return target.Add(viewDirection.Multiply(depthOffset));
        }

        private static BoundingBoxXYZ GetRoomBoundingBox(Room room)
        {
            try
            {
                return room.get_BoundingBox(null);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUsableBox(BoundingBoxXYZ box)
        {
            return box != null
                && box.Min != null
                && box.Max != null
                && box.Max.X > box.Min.X
                && box.Max.Y > box.Min.Y;
        }

        private static XYZ MidPoint(XYZ min, XYZ max)
        {
            return new XYZ(
                (min.X + max.X) * 0.5,
                (min.Y + max.Y) * 0.5,
                (min.Z + max.Z) * 0.5);
        }

        private static double AverageZ(IList<XYZ> points)
        {
            if (points == null || points.Count == 0)
                return 0.0;

            double total = 0.0;
            foreach (XYZ point in points)
            {
                if (point != null)
                    total += point.Z;
            }

            return total / points.Count;
        }

        private static double GetRoomInteriorZ(Room room, double fallback)
        {
            BoundingBoxXYZ box = GetRoomBoundingBox(room);
            if (box != null && box.Min != null && box.Max != null && box.Max.Z > box.Min.Z)
                return (box.Min.Z + box.Max.Z) * 0.5;

            LocationPoint locationPoint = room.Location as LocationPoint;
            if (locationPoint != null && locationPoint.Point != null)
                return locationPoint.Point.Z;

            return fallback;
        }

        private static bool IsPointInRoom(Room room, XYZ point)
        {
            try
            {
                return point != null && room.IsPointInRoom(point);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsOrphaned(RoomTag tag)
        {
            try
            {
                return tag.IsOrphaned;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsTaggingLink(RoomTag tag)
        {
            try
            {
                return tag.IsTaggingLink;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSamePoint(XYZ first, XYZ second)
        {
            if (first == null || second == null)
                return false;

            return first.DistanceTo(second) <= PointToleranceFeet;
        }

        private static double DistanceSquared2D(XYZ first, XYZ second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            return (dx * dx) + (dy * dy);
        }

        private static void ShowSummary(CenteringSummary summary)
        {
            if (summary.Moved == 0 && summary.AlreadyCentered == summary.Total)
            {
                DialogHelper.ShowInfo(ToolTitle, $"All {summary.Total} room tag(s) are already centered.");
                return;
            }

            string text = $"Centered {summary.Moved} of {summary.Total} room tag(s) in the active view.";

            if (summary.AlreadyCentered > 0)
                text += $"\nAlready centered: {summary.AlreadyCentered}.";

            if (summary.SkippedTotal > 0)
            {
                text += $"\nSkipped: {summary.SkippedTotal}.";

                if (summary.SkippedPinned > 0)
                    text += $"\n- Pinned: {summary.SkippedPinned}.";
                if (summary.SkippedOrphaned > 0)
                    text += $"\n- Orphaned: {summary.SkippedOrphaned}.";
                if (summary.SkippedNoRoom > 0)
                    text += $"\n- No readable room or unloaded link: {summary.SkippedNoRoom}.";
                if (summary.SkippedNoCenter > 0)
                    text += $"\n- No valid room center: {summary.SkippedNoCenter}.";
                if (summary.SkippedFailed > 0)
                    text += $"\n- Failed to move: {summary.SkippedFailed}.";
            }

            if (summary.Moved > 0)
                DialogHelper.ShowInfo(ToolTitle, text);
            else
                DialogHelper.ShowError(ToolTitle, text);
        }
    }
}
