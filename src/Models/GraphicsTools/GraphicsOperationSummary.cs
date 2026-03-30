namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Tracks attempted/applied/skipped counts for graphics operations.
    /// </summary>
    internal sealed class GraphicsOperationSummary
    {
        public int Attempted { get; set; }

        public int Applied { get; set; }

        public int Skipped { get; set; }

        public bool HasChanges
        {
            get { return Applied > 0; }
        }
    }
}
