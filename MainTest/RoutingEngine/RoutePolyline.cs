using Elements.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoutingEngine.Core
{

    #region Input Route

    public sealed class RoutePolyline
    {
        public string Id { get; set; }

        public List<Vector3> Points { get; set; } =
            new List<Vector3>();

        public PipeProfile Profile { get; set; }

        public string SystemId { get; set; }
    }

    public sealed class RoutingOptions
    {
        public double SnapTolerance { get; set; } = 1.0;

        public double IntersectionTolerance { get; set; } = 1.0;

        public double AngleToleranceDegrees { get; set; } = 1.0;

        public double MinimumPipeLength { get; set; } = 0.01;

        public double DefaultBendRadius { get; set; } = 150.0;

        public bool DetectIntersections { get; set; } = true;

        public bool MergeCollinearSegments { get; set; } = true;
    }

    #endregion
}
