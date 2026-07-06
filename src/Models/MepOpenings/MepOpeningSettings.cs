#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningSettings.cs
 * Purpose       : Stores all saved settings for the Opening tools.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : MepOpeningElementRule
 *
 * Input         : Saved or default user settings.
 * Output        : Normalized settings used by the settings UI and create command.
 *
 * Notes         :
 * - Opening source/host flags prepare the UI for future current-model and linked-model workflows.
 * - MergeDistanceMm combines nearby selected MEP crossings into one rectangular opening.
 * - IncludeInsulation adds detected pipe/duct insulation thickness before the buffer is applied.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;

namespace AJTools.Models.MepOpenings
{
    public sealed class MepOpeningSettings
    {
        public List<MepOpeningElementRule> Rules { get; set; }

        public double MergeDistanceMm { get; set; }

        public bool IncludeInsulation { get; set; }

        public MepOpeningCreationMode CreationMode { get; set; }

        public MepOpeningSelectionMethod SelectionMethod { get; set; }

        public bool UseCurrentModelSources { get; set; }

        public bool UseLinkedModelSources { get; set; }

        public string SourceLinkInstanceUniqueId { get; set; }

        public bool UseCurrentModelHosts { get; set; }

        public bool UseLinkedModelHosts { get; set; }

        public string HostLinkInstanceUniqueId { get; set; }

        public static MepOpeningSettings CreateDefault()
        {
            var settings = new MepOpeningSettings
            {
                MergeDistanceMm = 100,
                IncludeInsulation = true,
                CreationMode = MepOpeningCreationMode.DirectOpening,
                SelectionMethod = MepOpeningSelectionMethod.SourceElements,
                UseCurrentModelSources = true,
                UseLinkedModelSources = false,
                SourceLinkInstanceUniqueId = string.Empty,
                UseCurrentModelHosts = true,
                UseLinkedModelHosts = false,
                HostLinkInstanceUniqueId = string.Empty,
                Rules = new List<MepOpeningElementRule>
                {
                    new MepOpeningElementRule
                    {
                        ElementKind = MepOpeningElementKind.Pipe,
                        IsIncluded = true,
                        Shape = MepOpeningShape.Circle,
                        CutoutBufferMm = 20
                    },
                    new MepOpeningElementRule
                    {
                        ElementKind = MepOpeningElementKind.Duct,
                        IsIncluded = true,
                        Shape = MepOpeningShape.Rectangle,
                        CutoutBufferMm = 25
                    },
                    new MepOpeningElementRule
                    {
                        ElementKind = MepOpeningElementKind.CableTray,
                        IsIncluded = true,
                        Shape = MepOpeningShape.Rectangle,
                        CutoutBufferMm = 25
                    },
                    new MepOpeningElementRule
                    {
                        ElementKind = MepOpeningElementKind.Conduit,
                        IsIncluded = true,
                        Shape = MepOpeningShape.Circle,
                        CutoutBufferMm = 15
                    }
                }
            };

            settings.Normalize();
            return settings;
        }

        public MepOpeningElementRule GetRule(MepOpeningElementKind elementKind)
        {
            Normalize();
            return Rules.FirstOrDefault(rule => rule.ElementKind == elementKind);
        }

        public MepOpeningSettings Clone()
        {
            Normalize();
            return new MepOpeningSettings
            {
                MergeDistanceMm = MergeDistanceMm,
                IncludeInsulation = IncludeInsulation,
                CreationMode = CreationMode,
                SelectionMethod = SelectionMethod,
                UseCurrentModelSources = UseCurrentModelSources,
                UseLinkedModelSources = UseLinkedModelSources,
                SourceLinkInstanceUniqueId = SourceLinkInstanceUniqueId,
                UseCurrentModelHosts = UseCurrentModelHosts,
                UseLinkedModelHosts = UseLinkedModelHosts,
                HostLinkInstanceUniqueId = HostLinkInstanceUniqueId,
                Rules = Rules.Select(rule => rule.Clone()).ToList()
            };
        }

        public void Normalize()
        {
            if (Rules == null)
            {
                Rules = new List<MepOpeningElementRule>();
            }

            AddMissingRule(MepOpeningElementKind.Pipe, MepOpeningShape.Circle, 20);
            AddMissingRule(MepOpeningElementKind.Duct, MepOpeningShape.Rectangle, 25);
            AddMissingRule(MepOpeningElementKind.CableTray, MepOpeningShape.Rectangle, 25);
            AddMissingRule(MepOpeningElementKind.Conduit, MepOpeningShape.Circle, 15);

            Rules = Rules
                .GroupBy(rule => rule.ElementKind)
                .Select(group => group.First())
                .OrderBy(rule => (int)rule.ElementKind)
                .ToList();

            foreach (MepOpeningElementRule rule in Rules)
            {
                rule.Normalize();
            }

            if (MergeDistanceMm < 0)
            {
                MergeDistanceMm = 0;
            }

            if (!System.Enum.IsDefined(typeof(MepOpeningCreationMode), CreationMode))
            {
                CreationMode = MepOpeningCreationMode.DirectOpening;
            }

            if (!System.Enum.IsDefined(typeof(MepOpeningSelectionMethod), SelectionMethod))
            {
                SelectionMethod = MepOpeningSelectionMethod.SourceElements;
            }

            if (!UseCurrentModelSources && !UseLinkedModelSources)
            {
                UseCurrentModelSources = true;
            }

            if (SourceLinkInstanceUniqueId == null)
            {
                SourceLinkInstanceUniqueId = string.Empty;
            }

            if (UseCurrentModelSources && UseLinkedModelSources)
            {
                bool preferLinked = !string.IsNullOrWhiteSpace(SourceLinkInstanceUniqueId);
                UseCurrentModelSources = !preferLinked;
                UseLinkedModelSources = preferLinked;
            }

            if (!UseCurrentModelHosts && !UseLinkedModelHosts)
            {
                UseCurrentModelHosts = true;
            }

            if (HostLinkInstanceUniqueId == null)
            {
                HostLinkInstanceUniqueId = string.Empty;
            }

            if (UseCurrentModelHosts && UseLinkedModelHosts)
            {
                bool preferLinked = !string.IsNullOrWhiteSpace(HostLinkInstanceUniqueId);
                UseCurrentModelHosts = !preferLinked;
                UseLinkedModelHosts = preferLinked;
            }
        }

        private void AddMissingRule(MepOpeningElementKind kind, MepOpeningShape shape, double bufferMm)
        {
            if (Rules.Any(rule => rule.ElementKind == kind))
            {
                return;
            }

            Rules.Add(new MepOpeningElementRule
            {
                ElementKind = kind,
                IsIncluded = true,
                Shape = shape,
                CutoutBufferMm = bufferMm
            });
        }
    }
}
