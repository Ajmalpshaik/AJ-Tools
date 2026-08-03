// ============================================================
// RECIPE — tag-elements-in-active-view.cs (v3 — scored candidate placement, registry-based)
// PURPOSE: Tag qualifying elements of one category visible in the active view, deciding each tag's
//          side (above/below/left/right) and leader by SCORING candidates against everything already
//          placed — not a fixed rule, not a place-everything-then-shove-apart pass. Ideas adapted from
//          AJ Tools' own compiled SmartTagPlacementEngine.cs (Ajmal, 2026-07-14: "you can refer our
//          smart tag program... take from there if you need"), reimplemented with plain Revit API
//          calls since that class is internal and unreachable from this bridge (see gotcha below).
// ------------------------------------------------------------
// WHY v3, NOT v2's place-all-then-resolve: v2 placed every tag on a FIXED side (below/right) then ran
// an iterative push-apart pass to fix overlaps afterward. At 1092 tags that pass had to move roughly
// half the tags to converge, which silently destroyed the flow-direction correctness Ajmal had already
// confirmed (dropped from 26/26 to ~49% — a coin flip) because the resolver only knows "clear this
// overlap", not "preserve why this tag was on this side". v3 fixes this by deciding each tag's final
// position ONCE, scored against a live registry of everything placed so far — so a later tag simply
// never chooses a position that clashes, instead of clashing then being dragged away from its correct
// side afterward. See ../knowledge/live-model/tagging.md § Registry-based scored placement for the full writeup.
// ------------------------------------------------------------
// GOTCHA — tag SIDE is scored, not fixed (Ajmal, 2026-07-14): earlier "always below / always right"
// rule was too rigid — Ajmal confirmed above OR below is fine, but must be CONSISTENT and must not force
// an ugly cramped L-shape when space is tight. Real engine's technique, adapted here: generate BOTH
// candidate sides per orientation (below/above for horizontal, left/right for vertical), at 3 sliding
// anchor points along the duct's length (50%/25%/75% — a short segment gets more chances to find a
// clean spot), first WITH a leader at full offset, then WITHOUT a leader at a smaller offset (tag sits
// close, no L-shape at all) if nothing with a leader is clash-free. Score = clash-free-space + distance
// from neighbors + a CONSISTENCY BONUS for matching the side the nearest already-placed same-orientation
// tag chose (keeps runs looking uniform without being rigid) + small preference for the canonical
// midpoint/full-leader options over the fallback ones. Only when literally nothing scores clash-free
// does it fall back to the least-overlapping candidate — "tag everywhere" (Ajmal's stated goal) means
// never skip a duct, even in a genuinely dense pocket.
// ------------------------------------------------------------
// WHY THIS ISN'T JUST A CALL TO AJTools.Services.SmartTag.SmartTagPlacementEngine: that class (and
// LeaderLogicService) are `internal`, and AJ AI Bridge's run_csharp scripts are NOT compiled with a
// reference to AJTools.dll — confirmed empirically (CS0246 "AJTools could not be found"). The algorithm
// below is a reimplementation of the same IDEAS with plain Revit API calls, not a call to the real
// class. If Ajmal ever wants the exact compiled engine run without clicking the button himself, that
// needs a new compiled AJ Tools command (ajtools-build territory).
// ------------------------------------------------------------
// GOTCHA — check view.Scale FIRST, always (Ajmal, 2026-07-13, standing rule): every mm-based clearance
// below is computed from one shared viewScaleRatio, computed as literally the first step. A clearance
// tuned at one view scale silently breaks the moment the view scale changes (already caused a real
// 546-tag regression once) — see ../knowledge/live-model/tagging.md § Check view.Scale FIRST.
// GOTCHA — tag type: use Document.GetDefaultFamilyTypeId(tagCategory.Id), not a hardcoded family name —
// matches whatever Revit's own "Tag by Category" would use. ALSO: IndependentTag.Create()'s symId
// argument is not reliable — always verify tag.GetTypeId() after Create() and force it with
// ChangeTypeId() if it doesn't match. Full detail in ../knowledge/live-model/tagging.md.
// GOTCHA — leader side must be REAL flow direction (Ajmal, 2026-07-13, confirmed): for horizontal
// ducts, read Connector.Direction (In/Out) off the duct's own ConnectorManager and extend the leader
// toward the downstream (Out) side — not a geometric guess. This project's model already has calculated
// flow on every connector (verified live).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory elementCategory = BuiltInCategory.OST_DuctCurves;   // OST_PipeCurves, OST_MechanicalEquipment, etc.
BuiltInCategory tagCategory = BuiltInCategory.OST_DuctTags;         // OST_PipeTags, OST_MechanicalEquipmentTags, etc.
string tagFamilyTypeNameOverride = null;   // leave null to use Document.GetDefaultFamilyTypeId (Ajmal's default)
double baseOffsetMm = 600.0;               // tag head offset at view scale 1:50, scales with viewScaleRatio
double noLeaderOffsetMm = 220.0;           // smaller offset used for the no-leader "sits close" fallback
bool onlyHorizontalRuns = true;            // true = skip vertical risers (Ajmal's standing rule, 2026-07-13)
double minLengthMm = 1000.0;               // skip runs shorter than this (Ajmal's standing rule, 2026-07-13)
double levelToleranceMm = 1.0;             // endpoint Z difference below this counts as "horizontal"
// baseline FULL tag box size measured at view scale 1:50 (this project's default duct tag family) —
// used as the starting size ESTIMATE for scoring before any real tag has been placed this run; refined
// by a running average of REAL measured boxes as soon as a few tags exist. Re-measure if using a
// different tag family — see the gotcha in ../knowledge/live-model/tagging.md § Registry-based scored placement.
double baselineTagWidthMm = 790.0;
double baselineTagHeightMm = 486.0;
// ---- END INPUTS ----

var doc = Document;
var uidoc = UIDocument;
var view = uidoc.ActiveView;

// MANDATORY FIRST STEP (Ajmal, 2026-07-13) — see gotcha above.
double viewScaleRatio = view.Scale / 50.0;

XYZ viewRight = (view.RightDirection != null && view.RightDirection.GetLength() > 1e-9) ? view.RightDirection.Normalize() : XYZ.BasisX;
XYZ viewUp    = (view.UpDirection != null && view.UpDirection.GetLength() > 1e-9) ? view.UpDirection.Normalize() : XYZ.BasisY;
Func<XYZ, double> projX = v => v.DotProduct(viewRight);
Func<XYZ, double> projY = v => v.DotProduct(viewUp);

double minHorizontalStub = UnitUtils.ConvertToInternalUnits(550.0 * viewScaleRatio, DisplayUnitType.DUT_MILLIMETERS);
double minVerticalStub = UnitUtils.ConvertToInternalUnits(30.0 * viewScaleRatio, DisplayUnitType.DUT_MILLIMETERS);

// Reproduced from AJTools.Services.LeaderLogic.LeaderLogicService.GetE1 — class unreachable from here.
Func<XYZ, XYZ, double?, XYZ> getE1 = (l1, t1, preferredSign) =>
{
    XYZ delta = t1 - l1;
    double dxView = projX(delta);
    double dyView = projY(delta);
    if (Math.Abs(dyView) < minVerticalStub) return null; // straight leader, no elbow
    if (Math.Abs(dxView) < minHorizontalStub)
    {
        double pushDirection = preferredSign ?? ((dxView < 0) ? 1.0 : -1.0);
        return t1.Add(viewRight.Multiply(pushDirection * minHorizontalStub));
    }
    return l1.Add(viewUp.Multiply(dyView));
};

double offsetFeet = UnitUtils.ConvertToInternalUnits(baseOffsetMm * viewScaleRatio, DisplayUnitType.DUT_MILLIMETERS);
double minOffsetFeet = UnitUtils.ConvertToInternalUnits(250, DisplayUnitType.DUT_MILLIMETERS);
if (offsetFeet < minOffsetFeet) offsetFeet = minOffsetFeet;
double noLeaderOffsetFeet = UnitUtils.ConvertToInternalUnits(noLeaderOffsetMm * viewScaleRatio, DisplayUnitType.DUT_MILLIMETERS);

double estWidthFeet = UnitUtils.ConvertToInternalUnits(baselineTagWidthMm * viewScaleRatio, DisplayUnitType.DUT_MILLIMETERS);
double estHeightFeet = UnitUtils.ConvertToInternalUnits(baselineTagHeightMm * viewScaleRatio, DisplayUnitType.DUT_MILLIMETERS);
int estSampleCount = 0;

double cellSizeFeet = estWidthFeet * 3.0; // spatial bucket size, generous relative to a tag box

Func<double, double, (int, int)> cellOf = (x, y) => ((int)Math.Floor(x / cellSizeFeet), (int)Math.Floor(y / cellSizeFeet));

Func<Element, (double minX, double maxX, double minY, double maxY)?> realViewBox = (el) =>
{
    var ebb = el.get_BoundingBox(view);
    if (ebb == null) return null;
    var corners = new[] {
        new XYZ(ebb.Min.X, ebb.Min.Y, ebb.Min.Z), new XYZ(ebb.Min.X, ebb.Max.Y, ebb.Min.Z),
        new XYZ(ebb.Max.X, ebb.Min.Y, ebb.Min.Z), new XYZ(ebb.Max.X, ebb.Max.Y, ebb.Min.Z)
    };
    return (corners.Min(c => projX(c)), corners.Max(c => projX(c)), corners.Min(c => projY(c)), corners.Max(c => projY(c)));
};

var elementsToTag = new FilteredElementCollector(doc, view.Id)
    .OfCategory(elementCategory)
    .WhereElementIsNotElementType()
    .Where(el =>
    {
        var lc = el.Location as LocationCurve;
        if (lc == null || lc.Curve == null) return true;
        var p0 = lc.Curve.GetEndPoint(0);
        var p1 = lc.Curve.GetEndPoint(1);
        double lengthMm = UnitUtils.ConvertFromInternalUnits(lc.Curve.Length, DisplayUnitType.DUT_MILLIMETERS);
        double dzMm = UnitUtils.ConvertFromInternalUnits(Math.Abs(p1.Z - p0.Z), DisplayUnitType.DUT_MILLIMETERS);
        if (onlyHorizontalRuns && dzMm >= levelToleranceMm) return false;
        if (lengthMm < minLengthMm) return false;
        return true;
    })
    .ToList();

var sb = new System.Text.StringBuilder();
FamilySymbol tagType;
if (string.IsNullOrEmpty(tagFamilyTypeNameOverride))
{
    var tagCategoryObj = Category.GetCategory(doc, tagCategory);
    var defaultTypeId = doc.GetDefaultFamilyTypeId(tagCategoryObj.Id);
    tagType = doc.GetElement(defaultTypeId) as FamilySymbol;
    if (tagType == null) return "FAILED: could not resolve the project's default tag type for " + tagCategory + ".";
}
else
{
    tagType = new FilteredElementCollector(doc).OfCategory(tagCategory).WhereElementIsElementType()
        .FirstOrDefault(e => e.Name == tagFamilyTypeNameOverride) as FamilySymbol;
    if (tagType == null)
    {
        var available = new FilteredElementCollector(doc).OfCategory(tagCategory).WhereElementIsElementType().Select(e => e.Name);
        return "FAILED: tag type '" + tagFamilyTypeNameOverride + "' not found. Available: " + string.Join(", ", available);
    }
}

// ---- obstacle grid: every element of elementCategory in the view (not just the ones being tagged —
// a vertical riser we're not tagging is still something a tag must not land on top of) ----
var allElementsInView = new FilteredElementCollector(doc, view.Id).OfCategory(elementCategory).WhereElementIsNotElementType().ToElements();
var elementGrid = new Dictionary<(int, int), List<int>>();
var elementBoxes = new List<(double minX, double maxX, double minY, double maxY)>();
foreach (var el in allElementsInView)
{
    var b = realViewBox(el);
    if (!b.HasValue) continue;
    int idx = elementBoxes.Count;
    elementBoxes.Add(b.Value);
    var c0 = cellOf(b.Value.minX, b.Value.minY); var c1 = cellOf(b.Value.maxX, b.Value.maxY);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
    {
        var key = (gx, gy);
        if (!elementGrid.ContainsKey(key)) elementGrid[key] = new List<int>();
        elementGrid[key].Add(idx);
    }
}

// ---- tag registry grid — starts with any PRE-EXISTING tags in the view, grows as we place ----
var tagGrid = new Dictionary<(int, int), List<(double minX, double maxX, double minY, double maxY)>>();
var placedSideGrid = new Dictionary<(int, int), List<(double x, double y, bool isHorizontal, double sideSign)>>();

Action<double, double, double, double> registerTagBox = (minX, maxX, minY, maxY) =>
{
    var c0 = cellOf(minX, minY); var c1 = cellOf(maxX, maxY);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
    {
        var key = (gx, gy);
        if (!tagGrid.ContainsKey(key)) tagGrid[key] = new List<(double, double, double, double)>();
        tagGrid[key].Add((minX, maxX, minY, maxY));
    }
};
Action<double, double, bool, double> registerSide = (x, y, isHorizontal, sideSign) =>
{
    var key = cellOf(x, y);
    if (!placedSideGrid.ContainsKey(key)) placedSideGrid[key] = new List<(double, double, bool, double)>();
    placedSideGrid[key].Add((x, y, isHorizontal, sideSign));
};

foreach (var existingTag in new FilteredElementCollector(doc, view.Id).OfCategory(tagCategory).WhereElementIsNotElementType())
{
    var b = realViewBox(existingTag);
    if (b.HasValue) registerTagBox(b.Value.minX, b.Value.maxX, b.Value.minY, b.Value.maxY);
}

Func<double, double, double, double, bool> boxClashesElements = (minX, maxX, minY, maxY) =>
{
    var c0 = cellOf(minX, minY); var c1 = cellOf(maxX, maxY);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
        if (elementGrid.TryGetValue((gx, gy), out var lst))
            foreach (var idx in lst)
            {
                var d = elementBoxes[idx];
                if (minX < d.maxX && d.minX < maxX && minY < d.maxY && d.minY < maxY) return true;
            }
    return false;
};
Func<double, double, double, double, bool> boxClashesTags = (minX, maxX, minY, maxY) =>
{
    var c0 = cellOf(minX, minY); var c1 = cellOf(maxX, maxY);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
        if (tagGrid.TryGetValue((gx, gy), out var lst))
            foreach (var d in lst)
                if (minX < d.maxX && d.minX < maxX && minY < d.maxY && d.minY < maxY) return true;
    return false;
};
Func<double, double, double, double, double> overlapAreaVsAll = (minX, maxX, minY, maxY) =>
{
    double total = 0;
    var c0 = cellOf(minX, minY); var c1 = cellOf(maxX, maxY);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
    {
        if (elementGrid.TryGetValue((gx, gy), out var elst))
            foreach (var idx in elst)
            {
                var d = elementBoxes[idx];
                double ox = Math.Min(maxX, d.maxX) - Math.Max(minX, d.minX);
                double oy = Math.Min(maxY, d.maxY) - Math.Max(minY, d.minY);
                if (ox > 0 && oy > 0) total += ox * oy;
            }
        if (tagGrid.TryGetValue((gx, gy), out var tlst))
            foreach (var d in tlst)
            {
                double ox = Math.Min(maxX, d.maxX) - Math.Max(minX, d.minX);
                double oy = Math.Min(maxY, d.maxY) - Math.Max(minY, d.minY);
                if (ox > 0 && oy > 0) total += ox * oy;
            }
    }
    return total;
};
// nearest already-placed same-orientation tag's side, for the consistency bonus
Func<double, double, bool, double?> nearestSameOrientationSide = (x, y, isHorizontal) =>
{
    double best = double.MaxValue; double? bestSign = null;
    var c0 = cellOf(x - cellSizeFeet, y - cellSizeFeet); var c1 = cellOf(x + cellSizeFeet, y + cellSizeFeet);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
        if (placedSideGrid.TryGetValue((gx, gy), out var lst))
            foreach (var e in lst)
            {
                if (e.isHorizontal != isHorizontal) continue;
                double dist = (e.x - x) * (e.x - x) + (e.y - y) * (e.y - y);
                if (dist < best) { best = dist; bestSign = e.sideSign; }
            }
    return bestSign;
};
// nearest distance to any existing tag box, for the free-space distance bonus
Func<double, double, double, double, double> nearestTagDistance = (minX, maxX, minY, maxY) =>
{
    double best = double.MaxValue;
    var c0 = cellOf(minX - cellSizeFeet, minY - cellSizeFeet); var c1 = cellOf(maxX + cellSizeFeet, maxY + cellSizeFeet);
    for (int gx = c0.Item1; gx <= c1.Item1; gx++)
    for (int gy = c0.Item2; gy <= c1.Item2; gy++)
        if (tagGrid.TryGetValue((gx, gy), out var lst))
            foreach (var d in lst)
            {
                double dx = Math.Max(0, Math.Max(minX - d.maxX, d.minX - maxX));
                double dy = Math.Max(0, Math.Max(minY - d.maxY, d.minY - maxY));
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < best) best = dist;
            }
    return best == double.MaxValue ? cellSizeFeet * 5 : best;
};

// real flow direction (Ajmal, 2026-07-13, confirmed) — falls back to nearest-riser geometry only if a
// duct has no resolvable In/Out connector pair
var verticalDuctMidsX = new List<double>();
foreach (var el in allElementsInView)
{
    var lcV = el.Location as LocationCurve;
    if (lcV == null || lcV.Curve == null) continue;
    var vp0 = lcV.Curve.GetEndPoint(0); var vp1 = lcV.Curve.GetEndPoint(1);
    if ((vp1 - vp0).GetLength() < 1e-9) continue;
    XYZ vdir = (vp1 - vp0).Normalize();
    if (Math.Abs(projY(vdir)) > Math.Abs(projX(vdir))) verticalDuctMidsX.Add(projX(lcV.Curve.Evaluate(0.5, true)));
}
double fallbackCentroidX = elementsToTag.Count > 0
    ? elementsToTag.Select(el =>
      {
          var lcC = el.Location as LocationCurve;
          return lcC != null ? projX(lcC.Curve.Evaluate(0.5, true)) : projX((el.Location as LocationPoint).Point);
      }).Average()
    : 0.0;

Func<Element, double?> flowDirSignX = (el) =>
{
    var mepDuct = el as Autodesk.Revit.DB.Mechanical.Duct;
    if (mepDuct == null) return null;
    XYZ inOrigin = null, outOrigin = null;
    foreach (Connector c in mepDuct.ConnectorManager.Connectors)
    {
        if (c.Direction == FlowDirectionType.In) inOrigin = c.Origin;
        else if (c.Direction == FlowDirectionType.Out) outOrigin = c.Origin;
    }
    if (inOrigin == null || outOrigin == null) return null;
    double flowDx = projX(outOrigin) - projX(inOrigin);
    return Math.Abs(flowDx) > 1e-6 ? (double?)(flowDx > 0 ? 1.0 : -1.0) : null;
};
// GOTCHA (Ajmal, 2026-07-14): Supply tags must point OPPOSITE of real flow direction; Return and
// Exhaust keep the real flow direction as-is (already confirmed correct). Identify system type via
// `Duct.MEPSystem.SystemType` (verified live: reliably returns SupplyAir/ReturnAir/ExhaustAir in this
// project) — NOT a name/tag-text guess. This mirrors the standard drafting convention of drawing
// supply and return systems as opposites for quick visual distinction, even though both directions are
// independently "correct" per their own real flow data.
// GOTCHA: MechanicalSystem.SystemType is type Autodesk.Revit.DB.Mechanical.DuctSystemType, NOT
// MechanicalSystemType (verified live via reflection — ToString() prints "SupplyAir" either way, but
// the actual enum type differs and guessing the wrong one is a compile error, not a silent bug).
Func<Element, double?> preferredSignForSystem = (el) =>
{
    double? rawSign = flowDirSignX(el);
    if (rawSign == null) return null;
    var mepDuct = el as Autodesk.Revit.DB.Mechanical.Duct;
    var sys = mepDuct?.MEPSystem as Autodesk.Revit.DB.Mechanical.MechanicalSystem;
    bool isSupply = sys != null && sys.SystemType == Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir;
    return isSupply ? -rawSign.Value : rawSign.Value;
};
Func<Element, double> getFlowMagnitude = (el) =>
{
    var mepDuct = el as Autodesk.Revit.DB.Mechanical.Duct;
    if (mepDuct == null) return 0.0;
    double maxFlow = 0.0;
    foreach (Connector c in mepDuct.ConnectorManager.Connectors)
        if (c.Flow > maxFlow) maxFlow = c.Flow;
    return maxFlow;
};

// priority order: bigger-flow ducts (more "important" to read clearly) get first pick of clean spots
var orderedDucts = elementsToTag.OrderByDescending(el => getFlowMagnitude(el)).ToList();

int created = 0, placedWithLeader = 0, placedNoLeader = 0, forcedBestEffort = 0;

using (var tx = new Transaction(doc, "Tag elements — scored candidate placement"))
{
    tx.Start();
    try
    {
        if (!tagType.IsActive) { tagType.Activate(); doc.Regenerate(); }

        double[] tParams = { 0.5, 0.25, 0.75 };

        foreach (var duct in orderedDucts)
        {
            var lc = duct.Location as LocationCurve;
            XYZ p0 = lc.Curve.GetEndPoint(0);
            XYZ p1 = lc.Curve.GetEndPoint(1);
            XYZ dir = (p1 - p0).GetLength() > 1e-9 ? (p1 - p0).Normalize() : XYZ.BasisX;
            bool isHorizontal = Math.Abs(projX(dir)) >= Math.Abs(projY(dir));

            // candidate sides: try both, order = tie-break preference when scores are equal
            double[] sideSigns = isHorizontal ? new[] { -1.0, 1.0 } : new[] { 1.0, -1.0 }; // horizontal: below first; vertical: right first

            double bestScore = -1e9;
            XYZ bestT1 = null, bestE1 = null, bestAnchor = null;
            bool bestNeedsLeader = true;
            double bestSideSign = sideSigns[0];
            bool foundCleanA = false;

            double? flowSign = isHorizontal ? preferredSignForSystem(duct) : null;
            if (isHorizontal && flowSign == null)
            {
                double myX = projX(lc.Curve.Evaluate(0.5, true));
                double referenceX = verticalDuctMidsX.Count > 0
                    ? verticalDuctMidsX.OrderBy(vx => Math.Abs(vx - myX)).First()
                    : fallbackCentroidX;
                flowSign = (myX >= referenceX) ? 1.0 : -1.0;
            }

            // PASS A: with leader, full offset
            foreach (var tParam in tParams)
            {
                XYZ anchor;
                try { anchor = lc.Curve.Evaluate(tParam, true); } catch { anchor = lc.Curve.Evaluate(0.5, true); }

                foreach (var sideSign in sideSigns)
                {
                    XYZ offsetDir = isHorizontal ? viewUp.Multiply(sideSign) : viewRight.Multiply(sideSign);
                    XYZ t1 = anchor.Add(offsetDir.Multiply(offsetFeet));
                    double? preferredSign = isHorizontal ? flowSign : (double?)null;
                    XYZ e1 = getE1(anchor, t1, preferredSign);

                    double t1x = projX(t1), t1y = projY(t1);
                    double minX = t1x - estWidthFeet / 2, maxX = t1x + estWidthFeet / 2;
                    double minY = t1y - estHeightFeet / 2, maxY = t1y + estHeightFeet / 2;

                    if (boxClashesElements(minX, maxX, minY, maxY) || boxClashesTags(minX, maxX, minY, maxY))
                        continue; // disqualified for PASS A, try next candidate

                    double dist = nearestTagDistance(minX, maxX, minY, maxY);
                    double distScore = Math.Min(30.0, dist / cellSizeFeet * 30.0);
                    double consistencyScore = 0;
                    var nearSide = nearestSameOrientationSide(t1x, t1y, isHorizontal);
                    if (nearSide.HasValue && nearSide.Value == sideSign) consistencyScore = 25;
                    double midpointScore = tParam == 0.5 ? 15 : 5;
                    double sideOrderScore = sideSign == sideSigns[0] ? 5 : 0; // slight preference for the default side on ties

                    double score = 40 + distScore + consistencyScore + midpointScore + sideOrderScore;

                    if (score > bestScore)
                    {
                        bestScore = score; bestT1 = t1; bestE1 = e1; bestAnchor = anchor;
                        bestNeedsLeader = true; bestSideSign = sideSign; foundCleanA = true;
                    }
                }
            }

            // PASS B: no leader, smaller offset — only if PASS A found nothing clash-free
            if (!foundCleanA)
            {
                foreach (var tParam in tParams)
                {
                    XYZ anchor;
                    try { anchor = lc.Curve.Evaluate(tParam, true); } catch { anchor = lc.Curve.Evaluate(0.5, true); }

                    foreach (var sideSign in sideSigns)
                    {
                        XYZ offsetDir = isHorizontal ? viewUp.Multiply(sideSign) : viewRight.Multiply(sideSign);
                        XYZ t1 = anchor.Add(offsetDir.Multiply(noLeaderOffsetFeet));

                        double t1x = projX(t1), t1y = projY(t1);
                        double minX = t1x - estWidthFeet / 2, maxX = t1x + estWidthFeet / 2;
                        double minY = t1y - estHeightFeet / 2, maxY = t1y + estHeightFeet / 2;

                        if (boxClashesElements(minX, maxX, minY, maxY) || boxClashesTags(minX, maxX, minY, maxY))
                            continue;

                        double dist = nearestTagDistance(minX, maxX, minY, maxY);
                        double distScore = Math.Min(30.0, dist / cellSizeFeet * 30.0);
                        double consistencyScore = 0;
                        var nearSide = nearestSameOrientationSide(t1x, t1y, isHorizontal);
                        if (nearSide.HasValue && nearSide.Value == sideSign) consistencyScore = 25;
                        double midpointScore = tParam == 0.5 ? 15 : 5;

                        double score = 20 + distScore + consistencyScore + midpointScore; // base lower than PASS A — leader is preferred when available

                        if (score > bestScore)
                        {
                            bestScore = score; bestT1 = t1; bestE1 = null; bestAnchor = anchor;
                            bestNeedsLeader = false; bestSideSign = sideSign;
                        }
                    }
                }
            }

            // PASS C: forced — genuinely dense pocket, nothing scored clash-free anywhere. Never skip
            // a duct (Ajmal: "I need to tag everywhere") — place the least-overlapping option.
            if (bestT1 == null)
            {
                double leastOverlap = double.MaxValue;
                foreach (var tParam in tParams)
                {
                    XYZ anchor;
                    try { anchor = lc.Curve.Evaluate(tParam, true); } catch { anchor = lc.Curve.Evaluate(0.5, true); }
                    foreach (var sideSign in sideSigns)
                    {
                        XYZ offsetDir = isHorizontal ? viewUp.Multiply(sideSign) : viewRight.Multiply(sideSign);
                        XYZ t1 = anchor.Add(offsetDir.Multiply(offsetFeet));
                        double t1x = projX(t1), t1y = projY(t1);
                        double minX = t1x - estWidthFeet / 2, maxX = t1x + estWidthFeet / 2;
                        double minY = t1y - estHeightFeet / 2, maxY = t1y + estHeightFeet / 2;
                        double overlap = overlapAreaVsAll(minX, maxX, minY, maxY);
                        if (overlap < leastOverlap)
                        {
                            leastOverlap = overlap; bestT1 = t1; bestAnchor = anchor;
                            bestSideSign = sideSign;
                            bestE1 = getE1(anchor, t1, isHorizontal ? flowSign : (double?)null);
                            bestNeedsLeader = true;
                        }
                    }
                }
                forcedBestEffort++;
            }

            var reference = new Reference(duct);
            var tag = IndependentTag.Create(doc, tagType.Id, view.Id, reference, bestNeedsLeader, TagOrientation.Horizontal, bestT1);
            if (tag.GetTypeId() != tagType.Id) tag.ChangeTypeId(tagType.Id);
            tag.TagHeadPosition = bestT1;
            if (bestNeedsLeader && bestE1 != null) tag.LeaderElbow = bestE1;

            if (bestNeedsLeader) placedWithLeader++; else placedNoLeader++;

            // register the ESTIMATED box now (fast — no per-tag Regenerate); real measurement +
            // correction happens once, after the whole loop, in the final pass below
            double bt1x = projX(bestT1), bt1y = projY(bestT1);
            registerTagBox(bt1x - estWidthFeet / 2, bt1x + estWidthFeet / 2, bt1y - estHeightFeet / 2, bt1y + estHeightFeet / 2);
            registerSide(bt1x, bt1y, isHorizontal, bestSideSign);

            created++;
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine("FAILED, rolled back: " + ex.Message);
        return sb.ToString();
    }
}

sb.AppendLine($"Ducts processed: {orderedDucts.Count} | tags created: {created} | with leader: {placedWithLeader} | no-leader fallback: {placedNoLeader} | forced (dense pocket): {forcedBestEffort}");

// ============================================================
// FINAL PASS — measure REAL placed boxes (one Regenerate, not one per tag) and correct any residual
// own-leader clash using the ACTUAL measured half-width/half-height, not the running estimate. Registry
// placement should make this rare; this is the safety net, not the primary mechanism.
// ============================================================
using (var txFix = new Transaction(doc, "Correct elbow clearance using real measured tag boxes"))
{
    txFix.Start();
    try
    {
        doc.Regenerate();
        var justPlacedTags = new FilteredElementCollector(doc, view.Id).OfCategory(tagCategory).WhereElementIsNotElementType()
            .Cast<IndependentTag>().ToList();
        int corrected = 0;
        foreach (var tag in justPlacedTags)
        {
            XYZ e1Current = null;
            try { if (tag.HasLeader) e1Current = tag.LeaderElbow; } catch { }
            if (e1Current == null) continue;

            var tagged = doc.GetElement(tag.TaggedLocalElementId);
            var tlc = tagged?.Location as LocationCurve;
            XYZ l1 = tlc != null ? tlc.Curve.Evaluate(0.5, true) : tag.TagHeadPosition;

            var bb = tag.get_BoundingBox(view);
            if (bb == null) continue;
            XYZ t1 = tag.TagHeadPosition;
            double halfWmm = UnitUtils.ConvertFromInternalUnits(Math.Abs(Math.Min(t1.X - bb.Min.X, bb.Max.X - t1.X)), DisplayUnitType.DUT_MILLIMETERS);
            double halfHmm = UnitUtils.ConvertFromInternalUnits(Math.Abs(Math.Min(t1.Y - bb.Min.Y, bb.Max.Y - t1.Y)), DisplayUnitType.DUT_MILLIMETERS);
            double edx = UnitUtils.ConvertFromInternalUnits(Math.Abs(e1Current.X - t1.X), DisplayUnitType.DUT_MILLIMETERS);
            double edy = UnitUtils.ConvertFromInternalUnits(Math.Abs(e1Current.Y - t1.Y), DisplayUnitType.DUT_MILLIMETERS);
            bool clashing = (edy < 5 && edx < halfWmm) || (edx < 5 && edy < halfHmm);
            if (!clashing) continue;

            double neededHalfWFeet = UnitUtils.ConvertToInternalUnits(halfWmm + 60.0, DisplayUnitType.DUT_MILLIMETERS); // measured half-width + margin
            XYZ delta = t1 - l1;
            double dxView = projX(delta), dyView = projY(delta);
            XYZ newE1;
            if (Math.Abs(dyView) < minVerticalStub) newE1 = null;
            else if (Math.Abs(dxView) < neededHalfWFeet)
            {
                double pushDirection = (dxView < 0) ? 1.0 : -1.0;
                newE1 = t1.Add(viewRight.Multiply(pushDirection * neededHalfWFeet));
            }
            else newE1 = l1.Add(viewUp.Multiply(dyView));

            if (newE1 != null) { tag.LeaderElbow = newE1; corrected++; }
        }
        sb.AppendLine($"Final elbow-clearance correction pass: {corrected} tag(s) adjusted using real measured boxes.");
        txFix.Commit();
    }
    catch (Exception ex)
    {
        txFix.RollBack();
        sb.AppendLine("Final correction pass FAILED, rolled back: " + ex.Message);
    }
}

// ============================================================
// PASS D — residual tag-vs-tag / tag-vs-duct cleanup (ad hoc, only fires if registry placement left a
// residual — confirmed 2026-07-14 this is NOT automatic from PASS A/B/C alone: a dense pocket's "forced"
// tags (bestT1 == null path) can still land overlapping each other or nearby duct geometry). Combined
// loop per ../knowledge/live-model/tagging.md "resolve together, not sequentially" — moves ONLY the straight-leader tag
// of a clashing pair (L-shaped stays put, per Ajmal's "no need to move" feedback); tag-vs-duct always
// moves the tag (the duct never moves). Bounded iteration cap — a genuine geometric deadlock in one dense
// pocket does NOT always fully converge; report the residual honestly, don't claim clash-free if the cap
// is hit.
// ============================================================
using (var txCleanup = new Transaction(doc, "Resolve residual tag clashes (dense-pocket cleanup)"))
{
    txCleanup.Start();
    try
    {
        double marginFeet = UnitUtils.ConvertToInternalUnits(50.0, DisplayUnitType.DUT_MILLIMETERS);
        var ductsForCleanup = new FilteredElementCollector(doc, view.Id).OfCategory(elementCategory).WhereElementIsNotElementType().ToList();
        var ductBoxesForCleanup = new List<(double minX, double maxX, double minY, double maxY)>();
        foreach (var d in ductsForCleanup) { var b = realViewBox(d); if (b.HasValue) ductBoxesForCleanup.Add(b.Value); }

        Func<IndependentTag, (double minX, double maxX, double minY, double maxY)?> textBoxOf = (tagEl) =>
        {
            var bbT = tagEl.get_BoundingBox(view);
            if (bbT == null) return null;
            XYZ t1T = tagEl.TagHeadPosition;
            double halfW = Math.Abs(Math.Min(t1T.X - bbT.Min.X, bbT.Max.X - t1T.X));
            double halfH = Math.Abs(Math.Min(t1T.Y - bbT.Min.Y, bbT.Max.Y - t1T.Y));
            double cx = projX(t1T), cy = projY(t1T);
            return (cx - halfW, cx + halfW, cy - halfH, cy + halfH);
        };
        Func<IndependentTag, XYZ> safeElbow = (tagEl) => { try { return tagEl.HasLeader ? tagEl.LeaderElbow : null; } catch { return null; } };
        Action<Dictionary<int, XYZ>, int, XYZ> addMove = (dict, id, v) => { if (dict.TryGetValue(id, out var ex2)) dict[id] = ex2.Add(v); else dict[id] = v; };

        int cleanupMoves = 0, cleanupIter;
        const int CLEANUP_CAP = 20;
        for (cleanupIter = 1; cleanupIter <= CLEANUP_CAP; cleanupIter++)
        {
            var liveTags = new FilteredElementCollector(doc, view.Id).OfCategory(tagCategory).WhereElementIsNotElementType().Cast<IndependentTag>().ToList();
            var fullBoxes = new List<(IndependentTag tag, double minX, double maxX, double minY, double maxY)>();
            foreach (var t in liveTags) { var b = realViewBox(t); if (b.HasValue) fullBoxes.Add((t, b.Value.minX, b.Value.maxX, b.Value.minY, b.Value.maxY)); }

            var moves = new Dictionary<int, XYZ>();

            for (int i = 0; i < fullBoxes.Count; i++)
            for (int j = i + 1; j < fullBoxes.Count; j++)
            {
                var a = fullBoxes[i]; var b = fullBoxes[j];
                double ox = Math.Min(a.maxX, b.maxX) - Math.Max(a.minX, b.minX);
                double oy = Math.Min(a.maxY, b.maxY) - Math.Max(a.minY, b.minY);
                if (ox <= 0 || oy <= 0) continue;

                bool aIsL = a.tag.HasLeader && safeElbow(a.tag) != null;
                bool bIsL = b.tag.HasLeader && safeElbow(b.tag) != null;
                double pushDist = Math.Min(ox, oy) + marginFeet;
                bool pushXAxis = ox < oy;
                double sign = pushXAxis ? Math.Sign((a.minX + a.maxX) / 2 - (b.minX + b.maxX) / 2) : Math.Sign((a.minY + a.maxY) / 2 - (b.minY + b.maxY) / 2);
                if (sign == 0) sign = 1;
                XYZ axisDir = pushXAxis ? viewRight : viewUp;

                double aShare, bShare;
                if (aIsL && !bIsL) { aShare = 0; bShare = 1; }
                else if (!aIsL && bIsL) { aShare = 1; bShare = 0; }
                else { aShare = 0.5; bShare = 0.5; }

                addMove(moves, a.tag.Id.IntegerValue, axisDir.Multiply(sign * pushDist * aShare));
                addMove(moves, b.tag.Id.IntegerValue, axisDir.Multiply(-sign * pushDist * bShare));
            }

            foreach (var t in liveTags)
            {
                var tb = textBoxOf(t);
                if (!tb.HasValue) continue;
                foreach (var d in ductBoxesForCleanup)
                {
                    double ox = Math.Min(tb.Value.maxX, d.maxX) - Math.Max(tb.Value.minX, d.minX);
                    double oy = Math.Min(tb.Value.maxY, d.maxY) - Math.Max(tb.Value.minY, d.minY);
                    if (ox <= 0 || oy <= 0) continue;
                    double pushDist = Math.Min(ox, oy) + marginFeet;
                    bool pushXAxis = ox < oy;
                    double tCenter = pushXAxis ? (tb.Value.minX + tb.Value.maxX) / 2 : (tb.Value.minY + tb.Value.maxY) / 2;
                    double dCenter = pushXAxis ? (d.minX + d.maxX) / 2 : (d.minY + d.maxY) / 2;
                    double sign = Math.Sign(tCenter - dCenter);
                    if (sign == 0) sign = 1;
                    XYZ axisDir = pushXAxis ? viewRight : viewUp;
                    addMove(moves, t.Id.IntegerValue, axisDir.Multiply(sign * pushDist));
                }
            }

            if (moves.Count == 0) break;

            foreach (var kv in moves)
            {
                var tagM = doc.GetElement(new ElementId(kv.Key)) as IndependentTag;
                if (tagM == null) continue;
                XYZ newHead = tagM.TagHeadPosition.Add(kv.Value);
                var taggedEl = doc.GetElement(tagM.TaggedLocalElementId);
                var tlcM = taggedEl?.Location as LocationCurve;
                XYZ l1M = tlcM != null ? tlcM.Curve.Evaluate(0.5, true) : tagM.TagHeadPosition;
                tagM.TagHeadPosition = newHead;
                if (tagM.HasLeader)
                {
                    var newE1 = getE1(l1M, newHead, null);
                    if (newE1 != null) tagM.LeaderElbow = newE1;
                }
                cleanupMoves++;
            }
            doc.Regenerate();
        }
        sb.AppendLine($"Residual-clash cleanup: {cleanupIter - 1} iteration(s), {cleanupMoves} tag move(s).");
        if (cleanupIter > CLEANUP_CAP) sb.AppendLine("WARNING: cleanup hit its iteration cap — some clash may remain in a genuinely dense pocket. Re-check, don't assume clean.");
        txCleanup.Commit();
    }
    catch (Exception ex)
    {
        txCleanup.RollBack();
        sb.AppendLine("Residual-clash cleanup FAILED, rolled back: " + ex.Message);
    }
}

// ---- after running: verify with a fresh, independent read-back (separate call) — re-check all THREE
// clash types (own-leader, tag-vs-tag, tag-vs-element) AND flow-direction match. Don't assume any of
// them held just because placement scored clean — always re-measure from the live model. Cleanup here
// prioritizes clash-free over preferred leader side, so a handful of flow-direction mismatches among the
// forced/cleaned-up tags is an accepted trade-off, not a bug — report it plainly if it shows up. ----
return sb.ToString();
