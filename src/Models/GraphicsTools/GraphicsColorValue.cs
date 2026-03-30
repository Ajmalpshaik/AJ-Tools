using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// UI color value wrapper that supports explicit "By View" state.
    /// </summary>
    internal sealed class GraphicsColorValue
    {
        private GraphicsColorValue(bool isByView, byte red, byte green, byte blue)
        {
            IsByView = isByView;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public bool IsByView { get; }

        public byte Red { get; }

        public byte Green { get; }

        public byte Blue { get; }

        public static GraphicsColorValue ByView()
        {
            return new GraphicsColorValue(true, 0, 0, 0);
        }

        public static GraphicsColorValue FromRgb(byte red, byte green, byte blue)
        {
            return new GraphicsColorValue(false, red, green, blue);
        }

        public static GraphicsColorValue FromRevitColor(Color color)
        {
            if (color == null || !color.IsValid)
            {
                return ByView();
            }

            return FromRgb(color.Red, color.Green, color.Blue);
        }

        public Color ToRevitColorOrInvalid()
        {
            return IsByView
                ? Color.InvalidColorValue
                : new Color(Red, Green, Blue);
        }
    }
}
