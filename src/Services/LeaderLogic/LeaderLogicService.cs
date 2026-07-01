// Tool Name: Leader Logic Service
// Description: Reusable L-shaped leader calculation logic for tag placement tools.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using Autodesk.Revit.DB;

namespace AJTools.Services.LeaderLogic
{
    public enum LeaderToggleState
    {
        Side,
        TopBottom
    }

    /// <summary>
    /// Reusable service for computing L-shaped leader geometry (elbow points)
    /// in view space. Uses view direction vectors (RightDirection / UpDirection)
    /// for reliable coordinate projection across all view types.
    /// Handles three cases:
    /// <list type="bullet">
    ///   <item>Normal L-shape: elbow at (L1.X, T1.Y) in view space.</item>
    ///   <item>Guard 1 (same X): L1 and T1 vertically aligned — pushes elbow
    ///         horizontally to create an L instead of a straight vertical line.</item>
    ///   <item>Guard 2 (same Y): L1 and T1 horizontally aligned — no elbow needed,
    ///         Revit draws a straight horizontal line.</item>
    /// </list>
    /// </summary>
    internal class LeaderLogicService
    {
        private readonly XYZ _viewRight;
        private readonly XYZ _viewUp;
        private readonly double _minHorizontalStub;
        private readonly double _minVerticalStub;

        /// <summary>
        /// Creates a new LeaderLogicService for the given view.
        /// </summary>
        /// <param name="view">The active Revit view.</param>
        /// <param name="minHorizontalStub">Minimum horizontal offset (view feet) before Guard 1 kicks in.</param>
        /// <param name="minVerticalStub">Minimum vertical offset (view feet) before Guard 2 kicks in.</param>
        public LeaderLogicService(View view, double minHorizontalStub = 0.5, double minVerticalStub = 0.1)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            _viewRight         = (view.RightDirection != null && view.RightDirection.GetLength() > 1e-9)
                ? view.RightDirection.Normalize()
                : XYZ.BasisX;
            _viewUp            = (view.UpDirection != null && view.UpDirection.GetLength() > 1e-9)
                ? view.UpDirection.Normalize()
                : XYZ.BasisY;
            _minHorizontalStub = minHorizontalStub;
            _minVerticalStub   = minVerticalStub;
        }

        /// <summary>
        /// Projects a model-space vector onto the view's horizontal axis (RightDirection).
        /// </summary>
        private double ProjectX(XYZ modelVector)
        {
            return modelVector.DotProduct(_viewRight);
        }

        /// <summary>
        /// Projects a model-space vector onto the view's vertical axis (UpDirection).
        /// </summary>
        private double ProjectY(XYZ modelVector)
        {
            return modelVector.DotProduct(_viewUp);
        }

        // ─────────────────────────────────────────
        // L1 / P1 — Leader End on the element
        // ─────────────────────────────────────────

        /// <summary>
        /// Returns the leader end point (L1) of a tag, or null if unavailable.
        /// </summary>
        public XYZ GetL1(IndependentTag tag)
        {
            if (tag == null || !tag.HasLeader)
                return null;

            try { return tag.LeaderEnd; }
            catch { return null; }
        }

        // ─────────────────────────────────────────
        // T1 — Tag Head snapped to stack column X
        // ─────────────────────────────────────────

        /// <summary>
        /// Returns the tag head position (T1) with its view-X snapped to a stack
        /// column. Use this when aligning multiple tags to a common vertical axis.
        /// </summary>
        /// <param name="targetHeadPointModel">Desired tag head position in model coordinates.</param>
        /// <param name="stackXView">X coordinate in view space to snap to.</param>
        /// <returns>Snapped tag head position in model coordinates.</returns>
        public XYZ GetT1(XYZ targetHeadPointModel, double stackXView)
        {
            double currentX = ProjectX(targetHeadPointModel);
            double deltaX   = stackXView - currentX;
            return targetHeadPointModel.Add(_viewRight.Multiply(deltaX));
        }

        // ─────────────────────────────────────────
        // E1 — Elbow Point
        //
        // NORMAL CASE  (L1 and T1 have different X):
        //   E1.X = L1.X   — directly above/below the element leader end
        //   E1.Y = T1.Y   — on the same row as the tag head
        //   Path: L1/P1 → up/down → E1 → horizontal → T1
        //
        // GUARD 1  (L1 and T1 same X — straight vertical risk):
        //   E1.X = T1.X + horizontal offset (pushed away from tag)
        //   E1.Y = T1.Y   — stays on same row as tag head
        //   Path: L1/P1 → up → E1 → horizontal back → T1
        //
        // GUARD 2  (L1 and T1 same Y — straight horizontal):
        //   return null → Revit draws a straight line (no elbow needed)
        // ─────────────────────────────────────────

        /// <summary>
        /// Calculates the elbow point (E1) for an L-shaped leader between the leader
        /// end (L1) and the tag head (T1). Returns null when no elbow is needed
        /// (Guard 2 — same Y, straight horizontal line).
        /// </summary>
        /// <param name="l1Model">Leader end point in model coordinates.</param>
        /// <param name="t1Model">Tag head point in model coordinates.</param>
        /// <returns>Elbow point in model coordinates, or null if no elbow is needed.</returns>
        public XYZ GetE1(XYZ l1Model, XYZ t1Model)
        {
            if (l1Model == null || t1Model == null)
                return null;

            XYZ delta = t1Model - l1Model;
            double dxView = ProjectX(delta);
            double dyView = ProjectY(delta);

            double horizontalDist = Math.Abs(dxView);
            double verticalDist   = Math.Abs(dyView);

            // ── Guard 2: Same Y — straight horizontal line ────────────────
            if (verticalDist < _minVerticalStub)
                return null;

            // ── Guard 1: Same X — push elbow horizontally to create the L ─
            if (horizontalDist < _minHorizontalStub)
            {
                // Push E1 away from the tag/stack side.
                // If tag is to the LEFT of L1 → push RIGHT, and vice versa.
                double pushDirection = (dxView < 0) ? 1.0 : -1.0;

                // E1 sits at T1's Y level, pushed horizontally from T1.
                return t1Model.Add(_viewRight.Multiply(pushDirection * _minHorizontalStub));
            }

            // ── Normal Case — standard L-shape ────────────────────────────
            // E1 = L1 shifted vertically to T1's Y level.
            // In view space: E1.X = L1.X, E1.Y = T1.Y.
            // In model space: start from L1, add the vertical component of (T1 - L1).
            return l1Model.Add(_viewUp.Multiply(dyView));
        }

        // ─────────────────────────────────────────
        // Apply — Full logic in one call
        // ─────────────────────────────────────────

        /// <summary>
        /// Applies the full L-shaped leader logic to a tag: snaps the head to
        /// a stack column and sets the elbow for an L-shaped leader path.
        /// </summary>
        /// <param name="tag">The tag to modify.</param>
        /// <param name="targetHeadPointModel">Desired tag head position in model coordinates.</param>
        /// <param name="stackXView">Stack column X in view coordinates.</param>
        public void ApplyLeaderLogic(IndependentTag tag, XYZ targetHeadPointModel, double stackXView)
        {
            if (tag == null || !tag.HasLeader)
                return;

            XYZ l1 = GetL1(tag);
            if (l1 == null)
                return;

            XYZ t1 = GetT1(targetHeadPointModel, stackXView);
            XYZ e1 = GetE1(l1, t1);

            tag.TagHeadPosition = t1;

            if (e1 != null)
                tag.LeaderElbow = e1;
        }

        /// <summary>
        /// Computes the elbow point for a tag given its current head and leader end
        /// positions, without modifying the tag. Useful for tools that manage tag
        /// properties via reflection.
        /// </summary>
        /// <param name="headModel">Current tag head position in model coordinates.</param>
        /// <param name="leaderEndModel">Current leader end position in model coordinates.</param>
        /// <returns>Elbow point in model coordinates, or null if no elbow is needed.</returns>
        public XYZ ComputeElbow(XYZ headModel, XYZ leaderEndModel)
        {
            return GetE1(leaderEndModel, headModel);
        }

        /// <summary>
        /// Projects a model point to 2D view-space coordinates.
        /// </summary>
        public UV ProjectToView(XYZ modelPoint)
        {
            if (modelPoint == null)
                return new UV(0, 0);

            return new UV(ProjectX(modelPoint), ProjectY(modelPoint));
        }

        /// <summary>
        /// Offsets a model point by view-space horizontal/vertical deltas.
        /// </summary>
        public XYZ OffsetInView(XYZ modelPoint, double deltaXView, double deltaYView)
        {
            if (modelPoint == null)
                return null;

            return modelPoint
                .Add(_viewRight.Multiply(deltaXView))
                .Add(_viewUp.Multiply(deltaYView));
        }

        /// <summary>
        /// Determines if the current elbow is placed in a Side attachment or Top/Bottom attachment configuration.
        /// Defaults to Side if no elbow exists or if indeterminate.
        /// </summary>
        public LeaderToggleState DetermineToggleState(XYZ headModel, XYZ elbowModel)
        {
            if (headModel == null || elbowModel == null)
                return LeaderToggleState.Side;

            XYZ delta = elbowModel - headModel;
            double dxView = Math.Abs(ProjectX(delta));
            double dyView = Math.Abs(ProjectY(delta));

            // If the vertical distance to the elbow is greater than the horizontal, it's a Top/Bottom attachment
            if (dyView > dxView)
                return LeaderToggleState.TopBottom;

            return LeaderToggleState.Side;
        }

        /// <summary>
        /// Calculates the elbow point for a Side attachment.
        /// The leader approaches the tag text horizontally.
        /// </summary>
        public XYZ ComputeSideElbow(XYZ headModel, XYZ leaderEndModel)
        {
            if (headModel == null || leaderEndModel == null)
                return null;

            XYZ delta = leaderEndModel - headModel;
            double dxView = ProjectX(delta);
            double dyView = ProjectY(delta);
            
            double horizontalDist = Math.Abs(dxView);
            double verticalDist   = Math.Abs(dyView);

            if (verticalDist < _minVerticalStub)
                return null;

            if (horizontalDist < _minHorizontalStub)
            {
                double pushDirection = (dxView < 0) ? 1.0 : -1.0;
                return headModel.Add(_viewRight.Multiply(pushDirection * _minHorizontalStub));
            }

            return leaderEndModel.Add(_viewUp.Multiply(dyView));
        }

        /// <summary>
        /// Calculates the elbow point for a Top/Bottom attachment.
        /// The leader approaches the tag text vertically.
        /// </summary>
        public XYZ ComputeTopBottomElbow(XYZ headModel, XYZ leaderEndModel)
        {
            if (headModel == null || leaderEndModel == null)
                return null;

            XYZ delta = leaderEndModel - headModel;
            double dxView = ProjectX(delta);
            double dyView = ProjectY(delta);

            double horizontalDist = Math.Abs(dxView);
            double verticalDist   = Math.Abs(dyView);

            // If horizontally aligned, no elbow needed
            if (horizontalDist < _minHorizontalStub)
                return null;

            // Guard: If vertically aligned, create a vertical stub
            if (verticalDist < _minVerticalStub)
            {
                double pushDirection = (dyView < 0) ? 1.0 : -1.0;
                return headModel.Add(_viewUp.Multiply(pushDirection * _minVerticalStub));
            }

            // Top/Bottom elbow: aligned with TagHead in X, LeaderEnd in Y.
            return leaderEndModel.Add(_viewRight.Multiply(-dxView));
        }
    }
}
