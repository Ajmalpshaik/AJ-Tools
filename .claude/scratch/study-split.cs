var sb = new System.Text.StringBuilder();

var fittings = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

bool foundTransition = false;

foreach (var fitting in fittings) {
    var cm = fitting.MEPModel?.ConnectorManager;
    if (cm == null) continue;
    
    var connectedDucts = new List<MEPCurve>();
    foreach (Connector c in cm.Connectors) {
        if (c.IsConnected) {
            foreach (Connector r in c.AllRefs) {
                if (r.Owner.Id != fitting.Id && r.Owner is MEPCurve curve) {
                    connectedDucts.Add(curve);
                }
            }
        }
    }
    
    if (connectedDucts.Count == 2) {
        var d1 = connectedDucts[0];
        var d2 = connectedDucts[1];
        
        var w1 = d1.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.AsDouble() ?? 0;
        var w2 = d2.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.AsDouble() ?? 0;
        
        if (w1 > 0 && w2 > 0 && Math.Abs(w1 - w2) > 0.01) {
            sb.AppendLine($"Found Transition Fitting ID: {fitting.Id}");
            sb.AppendLine($"  Connects Duct {d1.Id} (Width: {w1 * 304.8:F1} mm) to Duct {d2.Id} (Width: {w2 * 304.8:F1} mm)");
            foundTransition = true;
            
            // Check takeoffs for D1
            int d1Takeoffs = 0;
            foreach (Connector dc in d1.ConnectorManager.Connectors) {
                if (dc.IsConnected) {
                    foreach (Connector dcr in dc.AllRefs) {
                        if (dcr.Owner.Id != d1.Id && dcr.Owner is FamilyInstance fi) {
                            var ptp = fi.Symbol.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
                            if (ptp != null) {
                                int pt = ptp.AsInteger();
                                if (pt == 23 || pt == 24 || pt == 25 || pt == 28) 
                                {
                                    d1Takeoffs++;
                                    string locStr = "";
                                    if (fi.Location is LocationPoint lp) locStr = lp.Point.ToString();
                                    sb.AppendLine($"    Duct {d1.Id} has Takeoff at {locStr}");
                                }
                            }
                        }
                    }
                }
            }
            
            // Check takeoffs for D2
            int d2Takeoffs = 0;
            foreach (Connector dc in d2.ConnectorManager.Connectors) {
                if (dc.IsConnected) {
                    foreach (Connector dcr in dc.AllRefs) {
                        if (dcr.Owner.Id != d2.Id && dcr.Owner is FamilyInstance fi) {
                            var ptp = fi.Symbol.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
                            if (ptp != null) {
                                int pt = ptp.AsInteger();
                                if (pt == 23 || pt == 24 || pt == 25 || pt == 28) 
                                {
                                    d2Takeoffs++;
                                    string locStr = "";
                                    if (fi.Location is LocationPoint lp) locStr = lp.Point.ToString();
                                    sb.AppendLine($"    Duct {d2.Id} has Takeoff at {locStr}");
                                }
                            }
                        }
                    }
                }
            }
            
            var lc1 = d1.Location as LocationCurve;
            var lc2 = d2.Location as LocationCurve;
            sb.AppendLine($"  Duct {d1.Id} P0: {lc1.Curve.GetEndPoint(0)}, P1: {lc1.Curve.GetEndPoint(1)}");
            sb.AppendLine($"  Duct {d2.Id} P0: {lc2.Curve.GetEndPoint(0)}, P1: {lc2.Curve.GetEndPoint(1)}");
        }
    }
}

if (!foundTransition) {
    sb.AppendLine("No size transition fittings found.");
}

return sb.ToString();
