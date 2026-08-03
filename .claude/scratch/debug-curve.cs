var sb = new System.Text.StringBuilder();

var ducts = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType()
    .Cast<MEPCurve>()
    .ToList();

var mainDucts = ducts.Where(d => {
    if (d.Location is LocationCurve lc && lc.Curve is Line line) {
        return Math.Abs(line.GetEndPoint(0).Z - line.GetEndPoint(1).Z) < 0.01;
    }
    return false;
}).ToList();

int curveConnFound = 0;
int takeoffRefs = 0;

foreach (var duct in mainDucts) {
    foreach (Connector c in duct.ConnectorManager.Connectors) {
        if (c.ConnectorType == ConnectorType.Curve) {
            curveConnFound++;
            if (c.IsConnected) {
                foreach (Connector r in c.AllRefs) {
                    if (r.Owner.Id != duct.Id) {
                        takeoffRefs++;
                        sb.AppendLine($"Duct {duct.Id} has Curve ref: {r.Owner.Category.Name} (ID: {r.Owner.Id})");
                    }
                }
            }
        }
    }
}

sb.AppendLine($"Found {curveConnFound} curve connectors and {takeoffRefs} references.");

return sb.ToString();
