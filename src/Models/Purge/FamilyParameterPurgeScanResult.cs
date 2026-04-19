using System.Collections.Generic;

namespace AJTools.Models.Purge
{
    internal sealed class FamilyParameterPurgeScanResult
    {
        public FamilyParameterPurgeScanResult(
            IList<FamilyParameterPurgeItem> items,
            IList<string> limitations)
        {
            Items = items ?? new List<FamilyParameterPurgeItem>();
            Limitations = limitations ?? new List<string>();
        }

        public IList<FamilyParameterPurgeItem> Items { get; }

        public IList<string> Limitations { get; }
    }
}
