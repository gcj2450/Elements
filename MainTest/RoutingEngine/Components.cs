using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements.Fittings;
using Elements.Geometry;

namespace RoutingEngine.Core
{


    #region Components
    /// <summary>
    /// 管件类型
    /// </summary>

    public enum ComponentType
    {
        Straight,
        Elbow,
        Tee,
        Cross,
        Reducer
    }

    /// <summary>
    /// 管件基类，包含类型端口和端口形状信息
    /// </summary>
    public abstract class RoutingComponent
    {
        public string Id { get; set; }

        public ComponentType Type { get; protected set; }

        public List<Port> Ports { get; } =
            new List<Port>();

        public PipeProfile Profile { get; set; }
    }

    public sealed class Straight : RoutingComponent
    {
        public Vector3 Start { get; set; }

        public Vector3 End { get; set; }

        public double Length =>
            (Start - End).Length();

        public Straight()
        {
            Type = ComponentType.Straight;
        }
    }

    public sealed class Elbow : RoutingComponent
    {
        public Vector3 Position { get; set; }

        public double AngleDegrees { get; set; }

        public double BendRadius { get; set; }

        public Elbow()
        {
            Type = ComponentType.Elbow;
        }
    }

    public sealed class Tee : RoutingComponent
    {
        public Vector3 Position { get; set; }

        public Tee()
        {
            Type = ComponentType.Tee;
        }
    }

    public sealed class Cross : RoutingComponent
    {
        public Vector3 Position { get; set; }

        public Cross()
        {
            Type = ComponentType.Cross;
        }
    }

    public sealed class Reducer : RoutingComponent
    {
        public Vector3 Start { get; set; }

        public Vector3 End { get; set; }

        public double Length =>
            (Start - End).Length();

        public Reducer()
        {
            Type = ComponentType.Reducer;
        }
    }

    #endregion

    #region Diagnostics

    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class RoutingDiagnostic
    {
        public DiagnosticSeverity Severity { get; set; }

        public string Code { get; set; }

        public string Message { get; set; }

        public string NodeId { get; set; }

        public string EdgeId { get; set; }
    }

    #endregion

    #region Result

    public sealed class RoutingResult
    {
        public RouteGraph Graph { get; set; }

        public List<RoutingComponent> Components { get; } =
            new List<RoutingComponent>();

        public List<RoutingDiagnostic> Diagnostics { get; } =
            new List<RoutingDiagnostic>();

    //    public IEnumerable<Straight> Pipes =>
    //        Components.OfType<Straight>();

    //    public IEnumerable<Elbow> Elbows =>
    //        Components.OfType<Elbow>();

    //    public IEnumerable<Tee> Tees =>
    //        Components.OfType<Tee>();

    //    public IEnumerable<Cross> Crosses =>
    //        Components.OfType<Cross>();

    //    public IEnumerable<Reducer> Reducers =>
    //Components.OfType<Reducer>();

        public bool Success =>
            Diagnostics.All(
                x => x.Severity != DiagnosticSeverity.Error);
    }

    #endregion
}
