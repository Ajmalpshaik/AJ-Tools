var sb = new System.Text.StringBuilder();

var terminals = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

if (terminals.Any()) {
    var t1 = terminals.First();
    var fs = t1.Symbol;
    var fam = fs.Family;
    sb.AppendLine($"Terminal: {fam.Name} - {fs.Name}");
    sb.AppendLine($"PartType: {fam.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE)?.AsInteger()}");
    
    var mepModel = t1.MEPModel;
    if (mepModel != null && mepModel.ConnectorManager != null) {
        sb.AppendLine($"Connectors: {mepModel.ConnectorManager.Connectors.Size}");
        foreach (Connector c in mepModel.ConnectorManager.Connectors) {
            sb.AppendLine($"Connector Domain: {c.Domain}, IsConnected: {c.IsConnected}");
        }
    } else {
        sb.AppendLine("No MEPModel or ConnectorManager.");
    }
}

return sb.ToString();
