using System.Collections.Generic;

namespace AJTools.Models.Purge
{
    internal sealed class FamilyParameterPurgeDeleteFailure
    {
        public FamilyParameterPurgeDeleteFailure(string parameterName, string reason)
        {
            ParameterName = parameterName ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string ParameterName { get; }

        public string Reason { get; }
    }

    internal sealed class FamilyParameterPurgeDeleteResult
    {
        public FamilyParameterPurgeDeleteResult()
        {
            Failures = new List<FamilyParameterPurgeDeleteFailure>();
        }

        public int AttemptedCount { get; set; }

        public int DeletedCount { get; set; }

        public IList<FamilyParameterPurgeDeleteFailure> Failures { get; }

        public int FailedCount
        {
            get { return Failures.Count; }
        }

        public void AddFailure(string parameterName, string reason)
        {
            Failures.Add(new FamilyParameterPurgeDeleteFailure(parameterName, reason));
        }
    }
}
