using Autodesk.Revit.DB;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctShapeService
    {
        /// <summary>
        /// Detects the duct shape. Returns "rectangular", "round", "oval", or null.
        /// </summary>
        public static string GetShape(Element duct, Document doc)
        {
            // 1. Try from duct type Shape property (Revit 2020 compatible)
            string fromType = GetShapeFromType(duct, doc);
            if (fromType != null)
                return fromType;

            // 2. Fallback: detect from parameter presence
            return GetShapeFromParameters(duct);
        }

        private static string GetShapeFromType(Element duct, Document doc)
        {
            var typeId = duct.GetTypeId();
            if (typeId == null || typeId == ElementId.InvalidElementId)
                return null;

            var ductType = doc.GetElement(typeId);
            if (ductType == null)
                return null;

            var shapeProp = ductType.GetType().GetProperty("Shape");
            if (shapeProp == null)
                return null;

            try
            {
                var shapeValue = shapeProp.GetValue(ductType);
                if (shapeValue == null)
                    return null;

                string shapeName = shapeValue.ToString().ToLowerInvariant();

                if (shapeName.Contains("rect"))
                    return "rectangular";
                if (shapeName.Contains("round") || shapeName.Contains("circ"))
                    return "round";
                if (shapeName.Contains("oval") || shapeName.Contains("oblong"))
                    return "oval";
            }
            catch
            {
                // Shape property access failed
            }

            return null;
        }

        private static string GetShapeFromParameters(Element duct)
        {
            bool hasWidth = GetParamValue(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM) > 0;
            bool hasHeight = GetParamValue(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM) > 0;
            bool hasDiameter = GetParamValue(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM) > 0;

            if (hasDiameter && !(hasWidth && hasHeight))
                return "round";
            if (hasWidth && hasHeight && hasDiameter)
                return "oval";
            if (hasWidth && hasHeight)
                return "rectangular";

            return null;
        }

        private static double GetParamValue(Element elem, BuiltInParameter bip)
        {
            try
            {
                var p = elem.get_Parameter(bip);
                if (p != null)
                    return p.AsDouble();
            }
            catch { }
            return 0.0;
        }
    }
}
