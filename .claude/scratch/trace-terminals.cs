var sb = new System.Text.StringBuilder();

var unions = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(f => f.Name.Contains("Union") || f.Symbol.Family.Name.Contains("Union"))
    .ToList();

var unionIds = unions.Select(u => u.Id).ToList();
if (unionIds.Count == 0) {
    return "No unions found.";
}

var mainDucts = new HashSet<ElementId>();
foreach (var u in unions) {
    var cm = u.MEPModel?.ConnectorManager;
    if (cm != null) {
        foreach (Connector c in cm.Connectors) {
            if (c.IsConnected) {
                foreach (Connector r in c.AllRefs) {
                    if (r.Owner is MEPCurve duct) {
                        mainDucts.Add(duct.Id);
                    }
                }
            }
        }
    }
}

foreach (var ductId in mainDucts) {
    var duct = Document.GetElement(ductId) as MEPCurve;
    sb.AppendLine($"--- Duct Segment {duct.Id} ---");
    
    int terminalCount = 0;
    foreach (Connector c in duct.ConnectorManager.Connectors) {
        if (c.ConnectorType == ConnectorType.Curve) {
            if (c.IsConnected) {
                foreach (Connector r in c.AllRefs) {
                    if (r.Owner.Id != duct.Id && r.Owner is FamilyInstance takeoff && takeoff.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting) {
                        // Trace from takeoff to terminal
                        int terminalsFed = TraceToTerminal(takeoff);
                        sb.AppendLine($"  Takeoff {takeoff.Id} feeds {terminalsFed} air terminals.");
                        terminalCount += terminalsFed;
                    }
                }
            }
        }
    }
    sb.AppendLine($"  Total Terminals Fed by this Segment: {terminalCount}");
}

return sb.ToString();

int TraceToTerminal(FamilyInstance takeoff) {
    int count = 0;
    var toVisit = new Stack<Element>();
    var visited = new HashSet<ElementId>();
    
    toVisit.Push(takeoff);
    
    while (toVisit.Count > 0) {
        var current = toVisit.Pop();
        if (visited.Contains(current.Id)) continue;
        visited.Add(current.Id);
        
        if (current.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctTerminal) {
            count++;
            continue;
        }
        
        ConnectorManager cm = null;
        if (current is MEPCurve curve) cm = curve.ConnectorManager;
        else if (current is FamilyInstance fi) cm = fi.MEPModel?.ConnectorManager;
        
        if (cm != null) {
            foreach (Connector c in cm.Connectors) {
                if (c.IsConnected) {
                    foreach (Connector r in c.AllRefs) {
                        if (r.Owner.Id != current.Id) {
                            toVisit.Push(r.Owner);
                        }
                    }
                }
            }
        }
    }
    
    return count;
}
