var sb = new System.Text.StringBuilder();

var terminals = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

bool foundExample = false;

foreach (var terminal in terminals) {
    if (terminal.MEPModel?.ConnectorManager == null) continue;
    
    foreach (Connector conn in terminal.MEPModel.ConnectorManager.Connectors) {
        if (conn.IsConnected && conn.AllRefs.Size > 1) {
            sb.AppendLine($"Found connected Terminal ID: {terminal.Id}");
            foundExample = true;
            
            // Traverse the connections
            foreach (Connector refConn in conn.AllRefs) {
                if (refConn.Owner.Id == terminal.Id) continue; // Skip self
                
                var owner = refConn.Owner;
                sb.AppendLine($"  -> Connected to {owner.Category.Name} (ID: {owner.Id}) [Type: {owner.GetType().Name}]");
                
                if (owner is MEPCurve mepCurve) {
                    var lc = owner.Location as LocationCurve;
                    var ep0 = lc.Curve.GetEndPoint(0);
                    var ep1 = lc.Curve.GetEndPoint(1);
                    sb.AppendLine($"     Curve Points: {ep0} to {ep1}");
                    
                    // See what this duct connects to
                    if (owner is MEPSystem || owner.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctSystem) continue;
                    
                    var ductConnMgr = (owner as MEPCurve).ConnectorManager;
                    foreach (Connector dc in ductConnMgr.Connectors) {
                        if (dc.IsConnected && !dc.Origin.IsAlmostEqualTo(conn.Origin)) {
                            foreach (Connector dRef in dc.AllRefs) {
                                if (dRef.Owner.Id == owner.Id || dRef.Owner is MEPSystem) continue;
                                sb.AppendLine($"       -> Then connected to {dRef.Owner.Category.Name} (ID: {dRef.Owner.Id}) [Type: {dRef.Owner.GetType().Name}]");
                                if (dRef.Owner is MEPCurve nextCurve) {
                                    var nlc = nextCurve.Location as LocationCurve;
                                    sb.AppendLine($"          Curve Points: {nlc.Curve.GetEndPoint(0)} to {nlc.Curve.GetEndPoint(1)}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

if (!foundExample) {
    sb.AppendLine("No connected terminals found.");
}

return sb.ToString();
