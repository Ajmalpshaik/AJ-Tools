// ============================================================
// SCRIPT: audit-family.cs
// PURPOSE: READ-ONLY full audit of an already-open family document. Reports identity
//          (category / OmniClass / types), every parameter with value + formula + scope per type,
//          every solid's subcategory / work plane / material, connectors, reference planes with
//          their Is Reference / Defines Origin / subcategory, model + reference-plane
//          subcategories, views (scale + levels visibility), counts and Revit's own warnings.
// USE IT:  before calling any family finished, after any geometry rebuild, and whenever Ajmal says
//          "check the family" / "full audit". Fastest way to catch leftover test values, blank
//          subcategories, unassociated work planes and empty material parameters.
// REQUIRES: the family document open in Revit. Read-only apart from switching the current type.
// SOURCE:  ../knowledge/live-model/families-conventions.md  (what the values SHOULD be)
//          ../knowledge/live-model/families-parameters.md   (labelled vs locked dimensions)
// STATUS:  living document - refine in place, do not fork a v2. Written 2026-08-27 from the
//          repeated diffuser / FCU audits.
// ============================================================

// ---- INPUT: substring of the open family's title. Edit every time. ----
string FAMILY_KEY = "Diffuser";

Autodesk.Revit.DB.Document doc = null;
foreach (Autodesk.Revit.DB.Document d in Application.Documents)
    if (d.IsFamilyDocument && d.Title.IndexOf(FAMILY_KEY, System.StringComparison.OrdinalIgnoreCase) >= 0) { doc = d; break; }
if (doc == null) return "no open family matching \"" + FAMILY_KEY + "\"";

var fm  = doc.FamilyManager;
var MMu = Autodesk.Revit.DB.DisplayUnitType.DUT_MILLIMETERS;
System.Func<double,double> TOMM = v => Autodesk.Revit.DB.UnitUtils.ConvertFromInternalUnits(v, MMu);
var sb = new System.Text.StringBuilder();

sb.AppendLine("############ " + doc.Title + " ############");
sb.AppendLine(doc.PathName);
var oc = doc.OwnerFamily.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.OMNICLASS_CODE);
var od = doc.OwnerFamily.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.OMNICLASS_DESCRIPTION);
sb.AppendLine("category=" + doc.OwnerFamily.FamilyCategory.Name
    + "  omniclass=" + (oc == null ? "?" : oc.AsString()) + " " + (od == null ? "" : od.AsString())
    + "  types=" + fm.Types.Size + "  warnings=" + doc.GetWarnings().Count);

// ---- parameters, per type. AsValueString() formats units correctly; fall back per storage type ----
foreach (Autodesk.Revit.DB.FamilyType ft in fm.Types)
{
    using (var tr = new Autodesk.Revit.DB.Transaction(doc, "switch type")) { tr.Start(); fm.CurrentType = ft; doc.Regenerate(); tr.Commit(); }
    sb.AppendLine("");
    sb.AppendLine("========== TYPE: " + ft.Name + " ==========");
    var rows = new System.Collections.Generic.List<string>();
    foreach (Autodesk.Revit.DB.FamilyParameter p in fm.Parameters)
    {
        bool builtin = false;
        try {
            var idf = p.Definition as Autodesk.Revit.DB.InternalDefinition;
            if (idf != null && idf.BuiltInParameter != Autodesk.Revit.DB.BuiltInParameter.INVALID) builtin = true;
        } catch {}
        if (builtin && !p.IsShared) continue;                     // skip Revit's own built-ins
        string v = "";
        try { v = ft.AsValueString(p); } catch {}
        if (string.IsNullOrEmpty(v))
        {
            if (p.StorageType == Autodesk.Revit.DB.StorageType.String) v = ft.AsString(p);
            else if (p.StorageType == Autodesk.Revit.DB.StorageType.Integer) { var i = ft.AsInteger(p); v = i.HasValue ? i.Value.ToString() : ""; }
            else if (p.StorageType == Autodesk.Revit.DB.StorageType.ElementId) { var e = ft.AsElementId(p); v = (e == null || e.IntegerValue == -1) ? "<none>" : doc.GetElement(e).Name; }
        }
        rows.Add(string.Format("  {0,-28} {1,-9} {2,-20} {3}", p.Definition.Name,
            p.IsInstance ? "Instance" : "Type",
            string.IsNullOrEmpty(v) ? "<empty>" : v,
            p.Formula == null ? "" : "= " + p.Formula));
    }
    rows.Sort();
    foreach (var r in rows) sb.AppendLine(r);
}

// ---- geometry. Subcategory MUST be read with AsValueString(): a wrong id type still resolves via
//      GetElement() but shows BLANK in Revit's UI. See families-conventions.md.
sb.AppendLine("");
sb.AppendLine("=== GEOMETRY ===");
foreach (Autodesk.Revit.DB.GenericForm gf in new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.GenericForm)))
{
    var bb = gf.get_BoundingBox(null);
    var sp = gf.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.FAMILY_ELEM_SUBCATEGORY);
    var wp = gf.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.SKETCH_PLANE_PARAM);
    var mp = gf.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.MATERIAL_ID_PARAM);
    string mat = "<none>", assoc = "-";
    if (mp != null && mp.AsElementId().IntegerValue != -1)
    { var m = doc.GetElement(mp.AsElementId()) as Autodesk.Revit.DB.Material; if (m != null) mat = m.Name; }
    try { var fp = fm.GetAssociatedFamilyParameter(mp); if (fp != null) assoc = fp.Definition.Name; } catch {}
    string sub = sp == null ? "?" : (sp.AsValueString() ?? "");
    string wpv = wp == null ? "?" : (wp.AsString() ?? "");
    sb.AppendLine(string.Format("  {0,-6} {1,-10} Z {2,6}..{3,-6} sub={4}", gf.Id.IntegerValue, gf.GetType().Name,
        bb == null ? "?" : TOMM(bb.Min.Z).ToString("0.#"),
        bb == null ? "?" : TOMM(bb.Max.Z).ToString("0.#"),
        string.IsNullOrEmpty(sub) ? "*** BLANK - wrong id type, see conventions ***" : sub));
    sb.AppendLine(string.Format("         workplane={0}",
        (string.IsNullOrEmpty(wpv) || wpv.IndexOf("not associated", System.StringComparison.OrdinalIgnoreCase) >= 0)
            ? "*** NOT ASSOCIATED - geometry is not tied to the skeleton ***" : wpv));
    sb.AppendLine(string.Format("         material={0} (via {1})", mat, assoc));
}

// ---- connectors ----
sb.AppendLine("");
sb.AppendLine("=== CONNECTORS ===");
int nc = 0;
foreach (Autodesk.Revit.DB.ConnectorElement ce in new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.ConnectorElement)))
{
    nc++;
    var o = ce.Origin;
    var bits = new System.Collections.Generic.List<string>();
    foreach (Autodesk.Revit.DB.Parameter p in ce.Parameters)
    {
        string n = p.Definition.Name;
        if (n != "Connector Description" && n != "Flow" && n != "Width" && n != "Height"
            && n != "Flow Direction" && n != "Flow Configuration" && n != "Allow Slope Adjustments") continue;
        string v = p.StorageType == Autodesk.Revit.DB.StorageType.String ? p.AsString() : p.AsValueString();
        string a = "";
        try { var fp = fm.GetAssociatedFamilyParameter(p); if (fp != null) a = "<-" + fp.Definition.Name; } catch {}
        bits.Add(n + "=" + v + a);
    }
    sb.AppendLine(string.Format("  {0,-6} {1,-16} at ({2},{3},{4})  {5}", ce.Id.IntegerValue, ce.Domain,
        TOMM(o.X).ToString("0"), TOMM(o.Y).ToString("0"), TOMM(o.Z).ToString("0"),
        string.Join("  ", bits.ToArray())));
}
if (nc == 0) sb.AppendLine("  <none>");

// ---- reference planes ----
Autodesk.Revit.DB.Category rpCat = null;
foreach (Autodesk.Revit.DB.Category c in doc.Settings.Categories) if (c.Name == "Reference Planes") rpCat = c;
var subNames = new System.Collections.Generic.Dictionary<int,string>();
if (rpCat != null) foreach (Autodesk.Revit.DB.Category sc in rpCat.SubCategories) subNames[sc.Id.IntegerValue] = sc.Name;
sb.AppendLine("");
sb.AppendLine("=== REFERENCE PLANES ===");
foreach (Autodesk.Revit.DB.ReferencePlane rp in new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.ReferencePlane)))
{
    var pl = rp.GetPlane();
    var isRef = rp.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ELEM_REFERENCE_NAME);
    Autodesk.Revit.DB.Parameter subP = null, defO = null;
    foreach (Autodesk.Revit.DB.Parameter p in rp.Parameters)
    { if (p.Definition.Name == "Subcategory") subP = p; if (p.Definition.Name == "Defines Origin") defO = p; }
    int sid = subP == null ? -1 : subP.AsElementId().IntegerValue;
    string pos = System.Math.Abs(pl.Normal.Z) > 0.9 ? "Z=" + TOMM(pl.Origin.Z).ToString("0.#")
               : (System.Math.Abs(pl.Normal.X) > 0.9 ? "X=" + TOMM(pl.Origin.X).ToString("0.#")
               : "Y=" + TOMM(pl.Origin.Y).ToString("0.#"));
    sb.AppendLine(string.Format("  {0,-24} {1,-20} origin={2,-4} {3,-11} normalZ={4,-3} sub={5}",
        string.IsNullOrEmpty(rp.Name) ? "<unnamed>" : rp.Name,
        isRef == null ? "?" : isRef.AsValueString(),
        defO != null && defO.AsInteger() == 1 ? "Yes" : "No",
        pos, pl.Normal.Z.ToString("0"),
        subNames.ContainsKey(sid) ? subNames[sid] : "<default>"));
}

// ---- subcategories ----
sb.AppendLine("");
sb.AppendLine("=== SUBCATEGORIES ===");
foreach (Autodesk.Revit.DB.Category c in doc.OwnerFamily.FamilyCategory.SubCategories)
{
    var col = c.LineColor;
    var w = c.GetLineWeight(Autodesk.Revit.DB.GraphicsStyleType.Projection);
    sb.AppendLine(string.Format("  MODEL {0,-34} RGB({1},{2},{3}) w={4} material={5}", c.Name,
        col.Red, col.Green, col.Blue, w.HasValue ? w.Value.ToString() : "-",
        c.Material == null ? "(blank)" : c.Material.Name));
}
if (rpCat != null) foreach (Autodesk.Revit.DB.Category c in rpCat.SubCategories)
{
    var pid = c.GetLinePatternId(Autodesk.Revit.DB.GraphicsStyleType.Projection);
    string pat = "Solid";
    if (pid != null && pid.IntegerValue > 0)
    { var lp = doc.GetElement(pid) as Autodesk.Revit.DB.LinePatternElement; pat = lp == null ? "?" : lp.Name; }
    var col = c.LineColor;
    sb.AppendLine(string.Format("  REFPL {0,-34} RGB({1},{2},{3}) pattern={4}", c.Name, col.Red, col.Green, col.Blue, pat));
}

// ---- views ----
sb.AppendLine("");
sb.AppendLine("=== VIEWS ===");
var lv = new Autodesk.Revit.DB.ElementId(Autodesk.Revit.DB.BuiltInCategory.OST_Levels);
foreach (Autodesk.Revit.DB.View v in new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.View)))
{
    if (v.IsTemplate || v.ViewType == Autodesk.Revit.DB.ViewType.ProjectBrowser
        || v.ViewType == Autodesk.Revit.DB.ViewType.SystemBrowser) continue;
    string h;
    try { h = v.GetCategoryHidden(lv) ? "hidden" : "VISIBLE"; } catch { h = "n/a"; }
    sb.AppendLine(string.Format("  {0,-12} \"{1,-14}\" 1:{2,-5} levels {3}", v.ViewType, v.Name, v.Scale, h));
}

// ---- totals. SymbolicCurve cannot be passed to OfClass - collect CurveElement and filter. ----
int symb = 0, sh = 0, inst = 0;
foreach (Autodesk.Revit.DB.CurveElement ce in new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.CurveElement)))
    if (ce is Autodesk.Revit.DB.SymbolicCurve) symb++;
foreach (Autodesk.Revit.DB.FamilyParameter p in fm.Parameters) if (p.IsShared) { sh++; if (p.IsInstance) inst++; }
sb.AppendLine("");
sb.AppendLine("solids=" + new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.GenericForm)).GetElementCount()
    + "  connectors=" + nc + "  symbolic lines=" + symb
    + "  planes=" + new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.ReferencePlane)).GetElementCount()
    + "  dimensions=" + new Autodesk.Revit.DB.FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Dimension)).GetElementCount()
    + "  shared params=" + sh + " (" + inst + " instance)"
    + "  warnings=" + doc.GetWarnings().Count);
return sb.ToString();
