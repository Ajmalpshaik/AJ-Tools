var sb = new System.Text.StringBuilder();

var terminal = Document.GetElement(new ElementId(918704)) as FamilyInstance;
if (terminal != null) {
    sb.AppendLine($"Terminal Z: {(terminal.Location as LocationPoint).Point.Z}");
    
    var conn = terminal.MEPModel.ConnectorManager.Connectors.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
    foreach (Connector refConn in conn.AllRefs) {
        if (refConn.Owner.Id == terminal.Id || refConn.Owner is MEPSystem) continue;
        
        var owner1 = refConn.Owner;
        sb.AppendLine($"1. {owner1.Category.Name} (ID: {owner1.Id})");
        
        if (owner1 is MEPCurve mep1) {
            foreach (Connector c1 in mep1.ConnectorManager.Connectors) {
                if (c1.IsConnected && !c1.Origin.IsAlmostEqualTo(conn.Origin)) {
                    foreach (Connector r1 in c1.AllRefs) {
                        if (r1.Owner.Id == owner1.Id || r1.Owner is MEPSystem) continue;
                        
                        var owner2 = r1.Owner;
                        sb.AppendLine($"  2. {owner2.Category.Name} (ID: {owner2.Id})");
                        
                        if (owner2 is FamilyInstance fitting) {
                            foreach (Connector c2 in fitting.MEPModel.ConnectorManager.Connectors) {
                                if (c2.IsConnected && !c2.Origin.IsAlmostEqualTo(c1.Origin)) {
                                    foreach (Connector r2 in c2.AllRefs) {
                                        if (r2.Owner.Id == owner2.Id || r2.Owner is MEPSystem) continue;
                                        
                                        var owner3 = r2.Owner;
                                        sb.AppendLine($"    3. {owner3.Category.Name} (ID: {owner3.Id})");
                                        
                                        if (owner3 is MEPCurve mep3) {
                                            var lc = mep3.Location as LocationCurve;
                                            sb.AppendLine($"       Points: {lc.Curve.GetEndPoint(0)} to {lc.Curve.GetEndPoint(1)}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

return sb.ToString();
