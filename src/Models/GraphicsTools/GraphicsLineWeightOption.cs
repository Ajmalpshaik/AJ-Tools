using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Represents line weight options for OverrideGraphicSettings.
    /// </summary>
    internal sealed class GraphicsLineWeightOption
    {
        public GraphicsLineWeightOption(int weight, string displayName)
        {
            Weight = weight;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        }

        public int Weight { get; }

        public string DisplayName { get; }

        public bool IsByView
        {
            get { return Weight == OverrideGraphicSettings.InvalidPenNumber; }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
