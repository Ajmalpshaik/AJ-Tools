// Tool Name: Smart Connect - Settings Model
// Description: Stores persisted Smart Connect routing and angle preferences.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: System.Collections.Generic

using System.Collections.Generic;

namespace AJTools.Models
{
    /// <summary>
    /// Routing modes supported by Smart Connect.
    /// </summary>
    public enum SmartConnectRoutingMode
    {
        SingleElbow = 0,
        OffsetWithTwoElbows = 1
    }

    /// <summary>
    /// Persisted Smart Connect settings.
    /// </summary>
    public class SmartConnectSettings
    {
        public SmartConnectRoutingMode RoutingMode { get; set; } = SmartConnectRoutingMode.SingleElbow;

        public double SelectedAngleDegrees { get; set; } = 90.0;

        public List<double> CustomAngles { get; set; } = new List<double>();
    }
}
