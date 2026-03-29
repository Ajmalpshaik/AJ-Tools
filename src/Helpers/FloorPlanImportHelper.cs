using Autodesk.Revit.DB;
using System;
using System.Windows.Forms;

namespace AJTools.Utils
{
    internal static class FloorPlanImportHelper
    {
        public static string SelectJsonFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Floor Plan JSON File";
                dialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dialog.Multiselect = false;
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.RestoreDirectory = true;

                DialogResult result = dialog.ShowDialog();
                return result == DialogResult.OK ? dialog.FileName : null;
            }
        }

        public static bool TryGetLengthUnit(string units, out DisplayUnitType displayUnitType)
        {
            displayUnitType = DisplayUnitType.DUT_MILLIMETERS;
            if (string.IsNullOrWhiteSpace(units))
            {
                return false;
            }

            switch (units.Trim().ToLowerInvariant())
            {
                case "mm":
                    displayUnitType = DisplayUnitType.DUT_MILLIMETERS;
                    return true;
                case "cm":
                    displayUnitType = DisplayUnitType.DUT_CENTIMETERS;
                    return true;
                case "m":
                    displayUnitType = DisplayUnitType.DUT_METERS;
                    return true;
                case "ft":
                    displayUnitType = DisplayUnitType.DUT_DECIMAL_FEET;
                    return true;
                case "in":
                    displayUnitType = DisplayUnitType.DUT_DECIMAL_INCHES;
                    return true;
                default:
                    return false;
            }
        }

        public static double ToInternalLength(double value, DisplayUnitType sourceDisplayUnitType)
        {
            return UnitUtils.ConvertToInternalUnits(value, sourceDisplayUnitType);
        }

        public static bool IsFiniteNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static bool TryParseWallLocationLine(string locationLine, out WallLocationLine wallLocationLine)
        {
            wallLocationLine = WallLocationLine.WallCenterline;

            if (string.IsNullOrWhiteSpace(locationLine))
            {
                return true;
            }

            return Enum.TryParse(locationLine.Trim(), true, out wallLocationLine);
        }
    }
}
