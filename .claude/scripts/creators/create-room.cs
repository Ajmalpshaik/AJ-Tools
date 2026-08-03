// ============================================================
// FRAGMENT (creator) — create-room.cs
// PURPOSE: Place a Room at one or more points on a level.
// PRODUCES: elements (List<Element>, the newly created Room(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see .claude/scripts/README.md for how to compose. A "creator" fills the same role
//          as a filter — it produces `elements` — so any action fragment can be appended after it.
// ============================================================
// IMPORTANT: Document.Create.NewRoom(level, UV) only produces a properly bounded room if the point
// sits inside an already-enclosed area (walls or room-separation lines forming a closed loop) on that
// level. If there's no enclosing boundary at the given point, Revit still creates the Room element but
// it comes out unbounded ("Not Enclosed" / zero area) — always check `room.Area > 0` after creating and
// report any that came out unbounded, don't assume success just because no exception was thrown.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId levelId = ElementId.InvalidElementId;
List<(double xMm, double yMm, string name, string number)> roomsToCreate = new List<(double, double, string, string)>
{
    (0, 0, "", "")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (levelId == ElementId.InvalidElementId)
{
    sb.AppendLine("No levelId set — nothing created.");
}
else
{
    var level = Document.GetElement(levelId) as Level;
    int unbounded = 0;

    using (var t = new Transaction(Document, "AJ Tools - Create Rooms"))
    {
        t.Start();
        try
        {
            foreach (var (xMm, yMm, name, number) in roomsToCreate)
            {
                var uv = new UV(
                    UnitUtils.ConvertToInternalUnits(xMm, DisplayUnitType.DUT_MILLIMETERS),
                    UnitUtils.ConvertToInternalUnits(yMm, DisplayUnitType.DUT_MILLIMETERS));
                var room = Document.Create.NewRoom(level, uv);
                if (!string.IsNullOrEmpty(name)) room.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set(name);
                if (!string.IsNullOrEmpty(number)) room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.Set(number);
                elements.Add(room);
                if (room.Area <= 0) unbounded++;
            }
            t.Commit();
            sb.AppendLine($"Created {elements.Count} room(s) on level '{level?.Name}'.");
            if (unbounded > 0)
            {
                sb.AppendLine($"WARNING: {unbounded} room(s) came out unbounded (zero area) — no enclosing walls/separation lines found at that point. Check before relying on their Area.");
            }
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create rooms — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
