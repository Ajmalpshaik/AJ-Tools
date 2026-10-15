#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, The Picture Of The View
 * File Name     : SmartTagPlacementContext.cs
 * Purpose       : Everything the placement needs to know about the view, worked out ONCE per run before
 *                 the first tag is placed: view directions and scale, what is already on the view, the
 *                 boundary tags must stay inside, the expected tag sizes, where every other element is,
 *                 and which elements have been tagged so far.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Services.LeaderLogic,
 *                 SmartTagViewObstacles, SmartTagSizeHints, SmartTagViewPlane
 *
 * Input         : The view check result, the settings and the candidates.
 * Output        : One object handed to every placement step, instead of 17 separate values.
 *
 * Notes         :
 * - Built exactly as the placement loop used to build these values itself, in the same order - the
 *   restructure moved them here so SmartTagPositionFinder can take one argument instead of seventeen.
 * - Annotations and AnnotationIndex GROW during the run: each placed tag is added so later tags avoid it.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Initial release (Smart MEP Tags restructure).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
using static AJTools.Services.SmartTag.SmartTagSizeHints;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The picture of the view the placement works against, built once per run.
    /// </summary>
    internal sealed class SmartTagPlacementContext
    {
        private SmartTagPlacementContext()
        {
        }

        public Document Doc { get; private set; }

        public View View { get; private set; }

        public PreFlightResult Preflight { get; private set; }

        public SmartTagSettingsState Settings { get; private set; }

        /// <summary>View scale, e.g. 100 for 1:100. Turns paper mm into model distances.</summary>
        public int ViewScale { get; private set; }

        /// <summary>The view's right direction, as a model direction.</summary>
        public XYZ ViewRight { get; private set; }

        /// <summary>The view's up direction, as a model direction.</summary>
        public XYZ ViewUp { get; private set; }

        public LeaderLogicService LeaderLogic { get; private set; }

        /// <summary>Tags, text notes and dimensions already on the view - grows as tags are placed.</summary>
        public List<AnnotationBox> Annotations { get; private set; }

        /// <summary>Quick-lookup grid over <see cref="Annotations"/> - grows with it.</summary>
        public AnnotationSpatialIndex AnnotationIndex { get; private set; }

        /// <summary>Expected tag size per category.</summary>
        public Dictionary<BuiltInCategory, TagSizeHint> SizeHints { get; private set; }

        /// <summary>The (annotation) crop a tag must stay inside; null when the view is not cropped.</summary>
        public AnnotationBox PlacementBoundary { get; private set; }

        /// <summary>Every element being tagged this run, in priority order.</summary>
        public List<TagCandidate> Candidates { get; private set; }

        /// <summary>Quick-lookup grid over the candidates' midpoints, for the parallel-group rule.</summary>
        public AnnotationSpatialIndex CandidateIndex { get; private set; }

        public Dictionary<AnnotationBox, TagCandidate> CandidateByBox { get; private set; }

        /// <summary>
        /// Ducts, fittings, accessories and equipment a no-leader tag must not sit on. Null unless some
        /// category has its Leader tick off - a normal run does not pay for it.
        /// </summary>
        public AnnotationSpatialIndex ModelObstacleIndex { get; private set; }

        public Dictionary<ElementId, AnnotationBox> ModelObstacleBoxes { get; private set; }

        /// <summary>
        /// Elements tagged so far this run, held as the candidates themselves - an ElementId alone cannot
        /// tell a linked element from one of this model's own.
        /// </summary>
        public HashSet<TagCandidate> TaggedCandidates { get; private set; }

        /// <summary>
        /// Works out the picture of the view once, before the first tag is placed.
        /// </summary>
        public static SmartTagPlacementContext Build(
            Document doc,
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            List<TagCandidate> candidates)
        {
            View activeView = preflight.ActiveView;
            int viewScale = preflight.ViewScale;
            XYZ viewRight = SmartTagViewPlane.GetViewRightDirection(activeView);
            XYZ viewUp = SmartTagViewPlane.GetViewUpDirection(activeView);

            var context = new SmartTagPlacementContext
            {
                Doc = doc,
                View = activeView,
                Preflight = preflight,
                Settings = settingsState,
                ViewScale = viewScale,
                ViewRight = viewRight,
                ViewUp = viewUp,
                LeaderLogic = new LeaderLogicService(activeView),
                Candidates = candidates,
                TaggedCandidates = new HashSet<TagCandidate>()
            };

            // Collect all existing annotations ONCE before the placement loop.
            context.Annotations = SmartTagViewObstacles.CollectExistingAnnotations(doc, activeView);
            context.AnnotationIndex = SmartTagViewObstacles.BuildAnnotationSpatialIndex(context.Annotations, viewScale);
            context.SizeHints = BuildTagSizeHints(doc, activeView, candidates, viewRight, viewUp, viewScale);
            context.PlacementBoundary = SmartTagViewObstacles.BuildPlacementBoundaryInViewPlane(preflight, viewRight, viewUp);

            // Index every candidate's own position ONCE too, for the parallel-group check - same
            // reasoning as the annotation index above.
            Dictionary<AnnotationBox, TagCandidate> candidateByBox;
            context.CandidateIndex = SmartTagViewObstacles.BuildCandidateSpatialIndex(candidates, out candidateByBox);
            context.CandidateByBox = candidateByBox;

            // Only built when a category actually has its leader switched off - it is the no-leader
            // placement that needs to know where the ducts are, and a normal run should not pay for it.
            foreach (TagCandidate probe in candidates)
            {
                if (probe != null && !SmartTagSettingsTracker.ResolveUseLeader(settingsState, probe.Category))
                {
                    Dictionary<ElementId, AnnotationBox> modelObstacleBoxes;
                    context.ModelObstacleIndex = SmartTagViewObstacles.BuildModelObstacleIndex(
                        doc, activeView, viewRight, viewUp, viewScale, out modelObstacleBoxes);
                    context.ModelObstacleBoxes = modelObstacleBoxes;
                    break;
                }
            }

            return context;
        }
    }
}
