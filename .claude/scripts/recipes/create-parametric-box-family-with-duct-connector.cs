// ============================================================
// SCRIPT: create-parametric-box-family-with-duct-connector.cs
// PURPOSE: Build a fully parametric box-shaped family (e.g. an air terminal, a simple equipment box)
//          from an already-open, empty Generic Model family document: set its category, add a
//          parametric rectangular body extrusion (centered on origin, resizable via Length/Width/
//          Height), an optional smaller rectangular "neck" stub on top of it (Neck Width/Neck
//          Height/Neck Depth), and a rectangular duct Connector on the neck's outer face.
// REQUIRES: the family document must already be open and active in Revit (this runs against
//           `Document` as-is — it does not open/create a new family file). Must be a fresh Generic
//           Model template with its default reference planes intact: "Center (Left/Right)",
//           "Center (Front/Back)", "Reference Plane" (the horizontal one), and a plan view named
//           "Ref. Level".
// SOURCE:  ../knowledge/live-model/families.md § Building a parametric family from scratch (Family Editor, via the
//          bridge) — read that section for the gotchas (ReferencePlane's 3rd arg is a direction not
//          a point, EQ dimension chains for centered resize, ConnectorElement.CreateDuctConnector's
//          real signature, connector Width/Height not inheriting the face size, Regenerate() outside
//          a transaction rolling back the whole call).
// STATUS:  living document — refine in place, don't fork a v2 file. First built 2026-07-16 for a
//          square ceiling supply air terminal (600x600mm body, 200x200mm rectangular neck).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
Autodesk.Revit.DB.BuiltInCategory targetCategory = Autodesk.Revit.DB.BuiltInCategory.OST_DuctTerminal; // e.g. OST_DuctTerminal (Air Terminals), OST_MechanicalEquipment
double bodyLengthMm = 600;   // X-extent of the main box, centered on origin
double bodyWidthMm  = 600;   // Y-extent of the main box, centered on origin
double bodyHeightMm = 150;   // Z-extent (depth) of the main box, from Z=0 upward
bool addNeck = true;         // false = skip the neck + connector entirely (just the body box)
double neckWidthMm  = 200;   // X-extent of the neck stub, centered on origin
double neckHeightMm = 200;   // Y-extent of the neck stub, centered on origin
double neckDepthMm  = 100;   // how far the neck sticks out ABOVE the body's own top (Height)
Autodesk.Revit.DB.Mechanical.DuctSystemType connectorSystemType = Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir; // SupplyAir or ReturnAir
string defaultTypeName = "Default"; // rename after AJ_ discussion, e.g. "600x600 - Neck 200x200"
// ---- END INPUTS ----

if (!Document.IsFamilyDocument) return "Active document is not a family document — open the family and make it active first.";

using Autodesk.Revit.DB;
var fm = Document.FamilyManager;
double LFt(double mm) => UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS);
double MM(double ft) => UnitUtils.ConvertFromInternalUnits(ft, DisplayUnitType.DUT_MILLIMETERS);
var sb = new System.Text.StringBuilder();

var view = new FilteredElementCollector(Document).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
    .FirstOrDefault(v => v.Name == "Ref. Level" && v.ViewType == ViewType.FloorPlan);
if (view == null) return "No 'Ref. Level' floor plan view found — this recipe expects the default Generic Model template.";

ReferencePlane GetPlane(string name) => new FilteredElementCollector(Document).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().First(p => p.Name == name);

// ---- Category ----
using (var t = new Transaction(Document, "Set family category"))
{
    t.Start();
    Document.OwnerFamily.FamilyCategory = Document.Settings.Categories.get_Item(targetCategory);
    t.Commit();
}
sb.AppendLine("Category: " + Document.OwnerFamily.FamilyCategory.Name);

// ---- Body parameters + default type ----
FamilyParameter lengthParam, widthParam, heightParam;
using (var t = new Transaction(Document, "Add body parameters"))
{
    t.Start();
    lengthParam = fm.AddParameter("Length", BuiltInParameterGroup.PG_GEOMETRY, ParameterType.Length, false);
    widthParam  = fm.AddParameter("Width",  BuiltInParameterGroup.PG_GEOMETRY, ParameterType.Length, false);
    heightParam = fm.AddParameter("Height", BuiltInParameterGroup.PG_GEOMETRY, ParameterType.Length, false);
    var ft = fm.NewType(defaultTypeName);
    fm.CurrentType = ft;
    fm.Set(lengthParam, LFt(bodyLengthMm));
    fm.Set(widthParam,  LFt(bodyWidthMm));
    fm.Set(heightParam, LFt(bodyHeightMm));
    t.Commit();
}

// ---- Body reference planes (Left/Right/Front/Back around the template's own Center planes) ----
double halfL = LFt(bodyLengthMm) / 2.0, halfW = LFt(bodyWidthMm) / 2.0;
ReferencePlane pLeft, pRight, pFront, pBack;
using (var t = new Transaction(Document, "Add body reference planes"))
{
    t.Start();
    pLeft  = Document.FamilyCreate.NewReferencePlane(new XYZ(-halfL, -3, 0), new XYZ(-halfL, 3, 0), XYZ.BasisZ, view); pLeft.Name = "Left";
    pRight = Document.FamilyCreate.NewReferencePlane(new XYZ( halfL, -3, 0), new XYZ( halfL, 3, 0), XYZ.BasisZ, view); pRight.Name = "Right";
    pFront = Document.FamilyCreate.NewReferencePlane(new XYZ(-3, -halfW, 0), new XYZ(3, -halfW, 0), XYZ.BasisZ, view); pFront.Name = "Front";
    pBack  = Document.FamilyCreate.NewReferencePlane(new XYZ(-3,  halfW, 0), new XYZ(3,  halfW, 0), XYZ.BasisZ, view); pBack.Name = "Back";
    t.Commit();
}

// ---- Body extrusion ----
var horizPlaneElem = GetPlane("Reference Plane");
Extrusion body;
using (var t = new Transaction(Document, "Create body extrusion"))
{
    t.Start();
    var sp = SketchPlane.Create(Document, horizPlaneElem.GetPlane());
    var arr = new CurveArrArray();
    var ca = new CurveArray();
    var p1 = new XYZ(-halfL, -halfW, 0); var p2 = new XYZ(halfL, -halfW, 0);
    var p3 = new XYZ(halfL, halfW, 0);   var p4 = new XYZ(-halfL, halfW, 0);
    ca.Append(Line.CreateBound(p1, p2)); ca.Append(Line.CreateBound(p2, p3));
    ca.Append(Line.CreateBound(p3, p4)); ca.Append(Line.CreateBound(p4, p1));
    arr.Append(ca);
    body = Document.FamilyCreate.NewExtrusion(true, arr, sp, LFt(bodyHeightMm));
    t.Commit();
}
sb.AppendLine("Body extrusion: " + bodyLengthMm + "x" + bodyWidthMm + "x" + bodyHeightMm + "mm");

// ---- Align body faces to planes, EQ-center, label overall dims, associate Height ----
var pCenterLR = GetPlane("Center (Left/Right)");
var pCenterFB = GetPlane("Center (Front/Back)");
var opt = new Options { ComputeReferences = true };
Reference bL=null, bR=null, bF=null, bB=null;
foreach (GeometryObject go in body.get_Geometry(opt))
    if (go is Solid solid)
        foreach (Face f in solid.Faces)
            if (f is PlanarFace pf)
            {
                var n = pf.FaceNormal;
                if (n.IsAlmostEqualTo(new XYZ(-1,0,0))) bL = pf.Reference;
                else if (n.IsAlmostEqualTo(new XYZ(1,0,0))) bR = pf.Reference;
                else if (n.IsAlmostEqualTo(new XYZ(0,-1,0))) bF = pf.Reference;
                else if (n.IsAlmostEqualTo(new XYZ(0,1,0))) bB = pf.Reference;
            }

double dimOffset1 = LFt(150), dimOffset2 = LFt(300);
using (var t = new Transaction(Document, "Align + dimension body"))
{
    t.Start();
    Document.FamilyCreate.NewAlignment(view, pLeft.GetReference(), bL);
    Document.FamilyCreate.NewAlignment(view, pRight.GetReference(), bR);
    Document.FamilyCreate.NewAlignment(view, pFront.GetReference(), bF);
    Document.FamilyCreate.NewAlignment(view, pBack.GetReference(), bB);

    var eqLine1 = Line.CreateBound(new XYZ(-halfL, -halfW-dimOffset1, 0), new XYZ(halfL, -halfW-dimOffset1, 0));
    var eqRefs1 = new ReferenceArray(); eqRefs1.Append(pLeft.GetReference()); eqRefs1.Append(pCenterLR.GetReference()); eqRefs1.Append(pRight.GetReference());
    Document.FamilyCreate.NewDimension(view, eqLine1, eqRefs1).AreSegmentsEqual = true;

    var lenLine = Line.CreateBound(new XYZ(-halfL, -halfW-dimOffset2, 0), new XYZ(halfL, -halfW-dimOffset2, 0));
    var lenRefs = new ReferenceArray(); lenRefs.Append(pLeft.GetReference()); lenRefs.Append(pRight.GetReference());
    Document.FamilyCreate.NewDimension(view, lenLine, lenRefs).FamilyLabel = lengthParam;

    var eqLine2 = Line.CreateBound(new XYZ(-halfL-dimOffset1, -halfW, 0), new XYZ(-halfL-dimOffset1, halfW, 0));
    var eqRefs2 = new ReferenceArray(); eqRefs2.Append(pFront.GetReference()); eqRefs2.Append(pCenterFB.GetReference()); eqRefs2.Append(pBack.GetReference());
    Document.FamilyCreate.NewDimension(view, eqLine2, eqRefs2).AreSegmentsEqual = true;

    var widLine = Line.CreateBound(new XYZ(-halfL-dimOffset2, -halfW, 0), new XYZ(-halfL-dimOffset2, halfW, 0));
    var widRefs = new ReferenceArray(); widRefs.Append(pFront.GetReference()); widRefs.Append(pBack.GetReference());
    Document.FamilyCreate.NewDimension(view, widLine, widRefs).FamilyLabel = widthParam;

    fm.AssociateElementParameterToFamilyParameter(body.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), heightParam);
    t.Commit();
}
sb.AppendLine("Body locked to Length/Width/Height parameters (EQ-centered on origin).");

// ---- Neck + connector (optional) ----
if (addNeck)
{
    FamilyParameter neckWParam, neckHParam, neckDParam, neckTopParam;
    using (var t = new Transaction(Document, "Add neck parameters"))
    {
        t.Start();
        neckWParam = fm.AddParameter("Neck Width",  BuiltInParameterGroup.PG_MECHANICAL, ParameterType.Length, false);
        neckHParam = fm.AddParameter("Neck Height", BuiltInParameterGroup.PG_MECHANICAL, ParameterType.Length, false);
        neckDParam = fm.AddParameter("Neck Depth",  BuiltInParameterGroup.PG_MECHANICAL, ParameterType.Length, false);
        fm.Set(neckWParam, LFt(neckWidthMm));
        fm.Set(neckHParam, LFt(neckHeightMm));
        fm.Set(neckDParam, LFt(neckDepthMm));
        neckTopParam = fm.AddParameter("Neck Top", BuiltInParameterGroup.PG_MECHANICAL, ParameterType.Length, false);
        fm.SetFormula(neckTopParam, "Height + Neck Depth"); // NO quotes around spaced names — quoting breaks the formula
        t.Commit();
    }

    double halfNW = LFt(neckWidthMm)/2.0, halfNH = LFt(neckHeightMm)/2.0;
    ReferencePlane nLeft, nRight, nFront, nBack;
    Extrusion neck;
    using (var t = new Transaction(Document, "Build neck geometry"))
    {
        t.Start();
        nLeft  = Document.FamilyCreate.NewReferencePlane(new XYZ(-halfNW, -3, 0), new XYZ(-halfNW, 3, 0), XYZ.BasisZ, view); nLeft.Name = "Neck Left";
        nRight = Document.FamilyCreate.NewReferencePlane(new XYZ( halfNW, -3, 0), new XYZ( halfNW, 3, 0), XYZ.BasisZ, view); nRight.Name = "Neck Right";
        nFront = Document.FamilyCreate.NewReferencePlane(new XYZ(-3, -halfNH, 0), new XYZ(3, -halfNH, 0), XYZ.BasisZ, view); nFront.Name = "Neck Front";
        nBack  = Document.FamilyCreate.NewReferencePlane(new XYZ(-3,  halfNH, 0), new XYZ(3,  halfNH, 0), XYZ.BasisZ, view); nBack.Name = "Neck Back";

        var sp = SketchPlane.Create(Document, horizPlaneElem.GetPlane());
        var arr = new CurveArrArray();
        var ca = new CurveArray();
        var p1 = new XYZ(-halfNW, -halfNH, 0); var p2 = new XYZ(halfNW, -halfNH, 0);
        var p3 = new XYZ(halfNW, halfNH, 0);   var p4 = new XYZ(-halfNW, halfNH, 0);
        ca.Append(Line.CreateBound(p1, p2)); ca.Append(Line.CreateBound(p2, p3));
        ca.Append(Line.CreateBound(p3, p4)); ca.Append(Line.CreateBound(p4, p1));
        arr.Append(ca);
        double neckTopFt = fm.CurrentType.AsDouble(neckTopParam) ?? (LFt(bodyHeightMm) + LFt(neckDepthMm));
        neck = Document.FamilyCreate.NewExtrusion(true, arr, sp, neckTopFt);
        t.Commit();
    }

    Reference nL=null, nR=null, nF=null, nB=null, nTop=null;
    foreach (GeometryObject go in neck.get_Geometry(opt))
        if (go is Solid solid)
            foreach (Face f in solid.Faces)
                if (f is PlanarFace pf)
                {
                    var n = pf.FaceNormal;
                    if (n.IsAlmostEqualTo(new XYZ(-1,0,0))) nL = pf.Reference;
                    else if (n.IsAlmostEqualTo(new XYZ(1,0,0))) nR = pf.Reference;
                    else if (n.IsAlmostEqualTo(new XYZ(0,-1,0))) nF = pf.Reference;
                    else if (n.IsAlmostEqualTo(new XYZ(0,1,0))) nB = pf.Reference;
                    else if (n.IsAlmostEqualTo(new XYZ(0,0,1))) nTop = pf.Reference;
                }

    ConnectorElement conn;
    using (var t = new Transaction(Document, "Align + dimension neck, add connector"))
    {
        t.Start();
        Document.FamilyCreate.NewAlignment(view, nLeft.GetReference(), nL);
        Document.FamilyCreate.NewAlignment(view, nRight.GetReference(), nR);
        Document.FamilyCreate.NewAlignment(view, nFront.GetReference(), nF);
        Document.FamilyCreate.NewAlignment(view, nBack.GetReference(), nB);

        var eqLine1 = Line.CreateBound(new XYZ(-halfNW, halfW+dimOffset1, 0), new XYZ(halfNW, halfW+dimOffset1, 0));
        var eqRefs1 = new ReferenceArray(); eqRefs1.Append(nLeft.GetReference()); eqRefs1.Append(pCenterLR.GetReference()); eqRefs1.Append(nRight.GetReference());
        Document.FamilyCreate.NewDimension(view, eqLine1, eqRefs1).AreSegmentsEqual = true;

        var wLine = Line.CreateBound(new XYZ(-halfNW, halfW+dimOffset2, 0), new XYZ(halfNW, halfW+dimOffset2, 0));
        var wRefs = new ReferenceArray(); wRefs.Append(nLeft.GetReference()); wRefs.Append(nRight.GetReference());
        Document.FamilyCreate.NewDimension(view, wLine, wRefs).FamilyLabel = neckWParam;

        var eqLine2 = Line.CreateBound(new XYZ(halfL+dimOffset1, -halfNH, 0), new XYZ(halfL+dimOffset1, halfNH, 0));
        var eqRefs2 = new ReferenceArray(); eqRefs2.Append(nFront.GetReference()); eqRefs2.Append(pCenterFB.GetReference()); eqRefs2.Append(nBack.GetReference());
        Document.FamilyCreate.NewDimension(view, eqLine2, eqRefs2).AreSegmentsEqual = true;

        var hLine = Line.CreateBound(new XYZ(halfL+dimOffset2, -halfNH, 0), new XYZ(halfL+dimOffset2, halfNH, 0));
        var hRefs = new ReferenceArray(); hRefs.Append(nFront.GetReference()); hRefs.Append(nBack.GetReference());
        Document.FamilyCreate.NewDimension(view, hLine, hRefs).FamilyLabel = neckHParam;

        fm.AssociateElementParameterToFamilyParameter(neck.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), neckTopParam);

        conn = ConnectorElement.CreateDuctConnector(Document, connectorSystemType, ConnectorProfileType.Rectangular, nTop);
        fm.AssociateElementParameterToFamilyParameter(conn.get_Parameter(BuiltInParameter.CONNECTOR_WIDTH), neckWParam);
        fm.AssociateElementParameterToFamilyParameter(conn.get_Parameter(BuiltInParameter.CONNECTOR_HEIGHT), neckHParam);

        t.Commit();
    }
    sb.AppendLine("Neck: " + neckWidthMm + "x" + neckHeightMm + "mm, " + neckDepthMm + "mm above body top. Connector: " + connectorSystemType + " rectangular, size-linked to neck.");
}

return sb.ToString();
