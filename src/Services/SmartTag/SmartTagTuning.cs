#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Tuning
 * File Name     : SmartTagTuning.cs
 * Purpose       : Every number that tunes where Smart MEP Tags puts a tag, in one place, each with a plain
 *                 note on what it does. To change how the placement behaves, change a number here - the
 *                 step files only read these.
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
 * Dependencies  : None.
 *
 * Input         : None - constants only.
 * Output        : The numbers the Smart MEP Tags placement reads.
 *
 * Notes         :
 * - Two kinds of millimetre, and the names say which:
 *     "...Mm"       real size in the model (a 150 mm gap is 150 mm whatever the view scale);
 *     "...PaperMm"  size on the PRINTED SHEET (1.5 mm on paper is 150 mm in the model at 1:100).
 * - These are the exact values the tool used before they were gathered here (2026-10-15), so moving
 *   them changed nothing. The user-facing settings (tag offset, size limits, leaders) are NOT here -
 *   they live in Smart MEP Tagging Settings.
 * - Change one number at a time and try it in Revit: several of these work together (see the notes
 *   on GoodEnoughScore and LeaderConsistencyPoints).
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Gathered from the placement and collection code into one file.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The numbers that tune Smart MEP Tags' placement algorithm. Change behaviour here.
    /// </summary>
    internal static class SmartTagTuning
    {
        // ─────────────────────────────────────────────
        // Where the tag is tried
        // ─────────────────────────────────────────────

        /// <summary>
        /// A tag WITH a leader is first tried this many times the set tag offset away from the element
        /// (300 mm offset gives 900 mm). Only if nothing fits does it try again at the plain offset with
        /// no leader.
        /// </summary>
        internal const double LeaderOffsetMultiplier = 3.0;

        /// <summary>
        /// Gap between a tag and its element when that category has "Leader" UNTICKED, real model mm.
        /// </summary>
        /// <remarks>
        /// 150mm, approved by Ajmal on 2026-08-18 after seeing both 300mm and 150mm on 32 live duct
        /// accessories. A no-leader tag reads as belonging to the thing it sits next to, so it wants to
        /// be closer than a tag on a leader - the general 300mm offset put it about 6mm off on a 1:50
        /// sheet before the tag's own width was even added, and he called it "very far".
        ///
        /// Kept separate from the general tag offset ON PURPOSE: that one is shared with every
        /// leader-carrying tag, and halving it would have moved every duct tag in the suite. He scoped
        /// this to accessories.
        /// </remarks>
        internal const double NoLeaderOffsetMm = 150.0;

        /// <summary>
        /// Points along a duct, pipe or tray run where the tag is tried, in this order: 0.5 is the
        /// middle, 0.25 a quarter along, 0.75 three quarters along. Elements with no run line (equipment,
        /// most accessories) are only tried at their middle.
        /// </summary>
        internal static readonly double[] SlidePointsAlongRun = { 0.5, 0.25, 0.75 };

        // ─────────────────────────────────────────────
        // Clash checks - mm on the printed sheet
        // ─────────────────────────────────────────────

        /// <summary>A tag closer than this to another tag, text or dimension counts as a clash.</summary>
        internal const double ClashTolerancePaperMm = 1.5;

        /// <summary>Closer than this (but not clashing) still scores lower - a "near miss".</summary>
        internal const double NearMissPaperMm = 2.0;

        /// <summary>A gap of more than this to the nearest annotation earns the full distance points.</summary>
        internal const double ComfortableGapPaperMm = 10.0;

        /// <summary>A gap smaller than this to the nearest annotation refuses the spot outright.</summary>
        internal const double MinimumGapPaperMm = 5.0;

        /// <summary>Clearance a no-leader tag keeps from other tags and from ducts and fittings.</summary>
        internal const double NoLeaderClearancePaperMm = 1.5;

        // ─────────────────────────────────────────────
        // The score - how one spot is rated
        // ─────────────────────────────────────────────

        /// <summary>Nothing within the near-miss distance.</summary>
        internal const int FreeSpacePoints = 40;

        /// <summary>No clash, but something within the near-miss distance.</summary>
        internal const int NearMissPoints = 10;

        /// <summary>Nearest annotation further than ComfortableGapPaperMm.</summary>
        internal const int ComfortableGapPoints = 20;

        /// <summary>Nearest annotation between MinimumGapPaperMm and ComfortableGapPaperMm.</summary>
        internal const int MinimumGapPoints = 10;

        /// <summary>Tag above or below the element - easiest to read.</summary>
        internal const int AboveOrBelowPoints = 20;

        /// <summary>Tag to the right or left of the element.</summary>
        internal const int BesidePoints = 10;

        /// <summary>
        /// A FLAT bonus every spot gets. It used to be a "leader length" test that was switched off, so
        /// the score is really out of 80 with these 20 free. GoodEnoughScore is set against that total -
        /// change the two together or every placement decision shifts.
        /// </summary>
        internal const int LeaderConsistencyPoints = 20;

        /// <summary>
        /// A spot scoring this or more is taken at once without trying the rest. Set against the
        /// 20-point flat bonus above.
        /// </summary>
        internal const int GoodEnoughScore = 60;

        // ─────────────────────────────────────────────
        // Crowded areas and parallel runs - real model mm
        // ─────────────────────────────────────────────

        /// <summary>Radius used to decide an element sits in a crowded area.</summary>
        internal const double DenseZoneRadiusMm = 500.0;

        /// <summary>
        /// More than this many other elements within DenseZoneRadiusMm makes it a crowded area. In a
        /// crowded area, if no normal spot is found, High priority elements are tagged at the least-bad
        /// spot and Medium/Low ones are skipped.
        /// </summary>
        internal const int DenseZoneNeighbourLimit = 5;

        /// <summary>Runs closer than this to each other count as one parallel group.</summary>
        internal const double ParallelGroupDistanceMm = 200.0;

        /// <summary>
        /// At least this many OTHER runs within ParallelGroupDistanceMm makes a parallel group. When no
        /// normal spot is found and another member of the group is already tagged, this one is skipped.
        /// </summary>
        internal const int ParallelGroupMinNeighbours = 2;

        // ─────────────────────────────────────────────
        // Expected tag size - mm on the printed sheet
        // ─────────────────────────────────────────────

        /// <summary>Tag width assumed when the view has no tag of that category to measure.</summary>
        internal const double FallbackTagWidthPaperMm = 15.0;

        /// <summary>Tag height assumed when the view has no tag of that category to measure.</summary>
        internal const double FallbackTagHeightPaperMm = 5.0;

        // ─────────────────────────────────────────────
        // Speed only - does not change where tags go
        // ─────────────────────────────────────────────

        /// <summary>Cell size of the quick-lookup grid used to find nearby annotations.</summary>
        internal const double SpatialIndexCellPaperMm = 20.0;
    }
}
