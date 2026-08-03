var sb = new System.Text.StringBuilder();
var collector = new FilteredElementCollector(Document)
    .OfClass(typeof(Wall))
    .WhereElementIsNotElementType()
    .ToElements();

int count = 0;
foreach (var e in collector)
{
    if (e.Location is LocationCurve lc)
    {
        var pt = lc.Curve.GetEndPoint(0);
        if (pt.X > 150)
        {
            count++;
        }
    }
}
sb.AppendLine($"Found {count} walls near X > 150.");
return sb.ToString();
