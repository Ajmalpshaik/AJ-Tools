using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.AiShell.Models
{
    public class RevitScriptGlobals
    {
        public Document Document { get; set; }
        public UIDocument UIDocument { get; set; }
        public Application Application { get; set; }
        public UIApplication UIApplication { get; set; }
        public System.Threading.CancellationToken CancellationToken { get; set; }
        public System.Action<int, string> ReportProgress { get; set; }
    }
}
