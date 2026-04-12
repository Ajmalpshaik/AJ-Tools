using System;
using Autodesk.Revit.DB;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctSizeService
    {
        private const double FT_TO_MM = 304.8;
        private const double FT_TO_M = 0.3048;

        public static double GetWidthMm(Element duct)
        {
            return GetParam(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM) * FT_TO_MM;
        }

        public static double GetHeightMm(Element duct)
        {
            return GetParam(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM) * FT_TO_MM;
        }

        public static double GetDiameterMm(Element duct)
        {
            return GetParam(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM) * FT_TO_MM;
        }

        public static double GetLengthM(Element duct)
        {
            return GetParam(duct, BuiltInParameter.CURVE_ELEM_LENGTH) * FT_TO_M;
        }

        /// <summary>
        /// Returns the governing size in mm used for rule matching.
        /// Rectangular/Oval: max(width, height). Round: diameter.
        /// </summary>
        public static double GetGoverningSize(Element duct, string shape)
        {
            switch (shape)
            {
                case "rectangular":
                case "oval":
                    double w = GetWidthMm(duct);
                    double h = GetHeightMm(duct);
                    return (w > 0 && h > 0) ? Math.Max(w, h) : 0.0;

                case "round":
                    return GetDiameterMm(duct);

                default:
                    return 0.0;
            }
        }

        private static double GetParam(Element elem, BuiltInParameter bip)
        {
            try
            {
                var p = elem.get_Parameter(bip);
                if (p != null)
                {
                    double val = p.AsDouble();
                    return val > 0 ? val : 0.0;
                }
            }
            catch { }
            return 0.0;
        }
    }
}
