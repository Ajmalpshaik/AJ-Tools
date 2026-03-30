using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Generic ElementId option item used by graphics settings dropdowns.
    /// </summary>
    internal sealed class GraphicsIdOption
    {
        public GraphicsIdOption(ElementId id, string displayName)
        {
            Id = id ?? ElementId.InvalidElementId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        }

        public ElementId Id { get; }

        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
