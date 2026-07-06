// Tool Name: Duct Reference Dimension Report
// Description: Tracks command run totals and builds the final user report.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace AJTools.Services.DuctReferenceDimension
{
    internal sealed class DuctReferenceDimensionReport
    {
        private readonly Dictionary<string, int> _skipReasons = new Dictionary<string, int>();
        private readonly List<FailedItem> _failedItems = new List<FailedItem>();

        public int TotalDuctsPicked { get; private set; }
        public int TotalDimensionsCreated { get; private set; }
        public int TotalDuctsAutoIncluded { get; private set; }
        public int TotalSkipped { get; private set; }
        public int WrongSelectionCount { get; private set; }

        public bool HasActivity
        {
            get
            {
                return TotalDuctsPicked > 0 ||
                       TotalDimensionsCreated > 0 ||
                       TotalSkipped > 0 ||
                       _failedItems.Count > 0;
            }
        }

        public void RecordDuctPicked()
        {
            TotalDuctsPicked++;
        }

        public void RecordCreated(IEnumerable<ElementId> coveredDuctIds, ElementId selectedDuctId)
        {
            TotalDimensionsCreated++;

            if (coveredDuctIds == null)
                return;

            TotalDuctsAutoIncluded += coveredDuctIds
                .Where(id => id != null && selectedDuctId != null && AJTools.Utils.ElementIdHelper.GetIntegerValue(id) != AJTools.Utils.ElementIdHelper.GetIntegerValue(selectedDuctId))
                .Select(id => AJTools.Utils.ElementIdHelper.GetIntegerValue(id))
                .Distinct()
                .Count();
        }

        public void RecordWrongSelection()
        {
            WrongSelectionCount++;
            RecordSkipped("Wrong element selected");
        }

        public void RecordSkipped(string reason)
        {
            TotalSkipped++;
            AddSkipReason(reason);
        }

        public void RecordFailed(ElementId elementId, string reason)
        {
            TotalSkipped++;
            AddSkipReason(reason);
            _failedItems.Add(new FailedItem(elementId, reason));
        }

        public string BuildSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Duct Reference Dimension Report");
            sb.AppendLine();
            sb.AppendLine("Total ducts picked: " + TotalDuctsPicked);
            sb.AppendLine("Total dimensions created: " + TotalDimensionsCreated);
            sb.AppendLine("Total ducts auto-included: " + TotalDuctsAutoIncluded);
            sb.AppendLine("Total skipped: " + TotalSkipped);

            if (_skipReasons.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Skipped reasons:");
                foreach (KeyValuePair<string, int> item in _skipReasons.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key))
                {
                    sb.AppendLine("- " + item.Key + ": " + item.Value);
                }
            }

            if (_failedItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Failed items:");
                foreach (FailedItem item in _failedItems)
                {
                    string idText = item.ElementId == null ? "Unknown" : AJTools.Utils.ElementIdHelper.GetIntegerValue(item.ElementId).ToString();
                    sb.AppendLine("- ElementId " + idText + ": " + item.Reason);
                }
            }

            return sb.ToString().TrimEnd();
        }

        private void AddSkipReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Unknown reason";

            if (_skipReasons.ContainsKey(reason))
                _skipReasons[reason]++;
            else
                _skipReasons[reason] = 1;
        }

        private sealed class FailedItem
        {
            public FailedItem(ElementId elementId, string reason)
            {
                ElementId = elementId;
                Reason = string.IsNullOrWhiteSpace(reason) ? "Unknown reason" : reason;
            }

            public ElementId ElementId { get; }
            public string Reason { get; }
        }
    }
}
