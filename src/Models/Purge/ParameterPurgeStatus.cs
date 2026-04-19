using System;

namespace AJTools.Models.Purge
{
    internal enum ParameterPurgeStatus
    {
        SafeToPurge = 0,
        PossiblyUnused = 1,
        InUse = 2,
        CannotDelete = 3
    }

    internal static class ParameterPurgeStatusExtensions
    {
        public static string ToDisplayText(this ParameterPurgeStatus status)
        {
            switch (status)
            {
                case ParameterPurgeStatus.SafeToPurge:
                    return "Safe to Purge";
                case ParameterPurgeStatus.PossiblyUnused:
                    return "Possibly Unused";
                case ParameterPurgeStatus.InUse:
                    return "In Use";
                case ParameterPurgeStatus.CannotDelete:
                    return "Cannot Delete";
                default:
                    return Enum.GetName(typeof(ParameterPurgeStatus), status) ?? "Unknown";
            }
        }
    }
}
