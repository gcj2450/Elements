using System;
using System.Collections.Generic;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;

namespace Elements.Flow
{
    public partial class Connection
    {
        public const double DIAMETER_INSET = 0.001;
        public const double DEFAULT_CONNECTION_DIAMETER = 0.01;

        public ConnectionLocator ComponentLocator { get; set; }

        public bool? IsLoop { get; set; }

        /// <summary>
        /// 设置形状参数，如果是圆形：取width和height的最大值作为直径
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="shapeType"></param>
        public void SetShape(double width, double height, Elements.Fittings.ShapeType shapeType)
        {
            Width = width;
            Height = height;
            ShapeType = shapeType;
            Diameter = shapeType == Elements.Fittings.ShapeType.Circle ? Math.Max(width, height) : Math.Sqrt(width * height);
        }

        /// <summary>
        /// Tests whether two flow connections have the same complete cross section.
        /// </summary>
        public bool HasSameProfile(Connection other, double tolerance = Vector3.EPSILON)
        {
            if (other == null || ShapeType != other.ShapeType)
            {
                return false;
            }

            if (ShapeType == Elements.Fittings.ShapeType.Circle)
            {
                return Diameter.ApproximatelyEquals(other.Diameter, tolerance);
            }

            return EffectiveWidth().ApproximatelyEquals(other.EffectiveWidth(), tolerance) &&
                   EffectiveHeight().ApproximatelyEquals(other.EffectiveHeight(), tolerance);
        }

        /// <summary>
        /// Tests whether this flow connection and a fitting port have the same complete cross section.
        /// </summary>
        public bool HasSameProfile(Elements.Fittings.Port port, double tolerance = Vector3.EPSILON)
        {
            if (port == null || ShapeType != port.ShapeType)
            {
                return false;
            }

            if (ShapeType == Elements.Fittings.ShapeType.Circle)
            {
                return Diameter.ApproximatelyEquals(port.Diameter, tolerance);
            }

            var portWidth = port.Width > 0 ? port.Width : port.Diameter;
            var portHeight = port.Height > 0 ? port.Height : port.Diameter;
            return EffectiveWidth().ApproximatelyEquals(portWidth, tolerance) &&
                   EffectiveHeight().ApproximatelyEquals(portHeight, tolerance);
        }

        private double EffectiveWidth()
        {
            return Width > 0 ? Width : Diameter;
        }

        private double EffectiveHeight()
        {
            return Height > 0 ? Height : Diameter;
        }

        public override void UpdateRepresentations()
        {
            if (this.Representation == null)
            {
                this.Representation = new Representation(new List<SolidOperation>());
            }
            this.Representation.SolidOperations = new List<SolidOperation>();
            var width = ShapeType == Elements.Fittings.ShapeType.Circle ? (Diameter > 0 ? Diameter - DIAMETER_INSET : DEFAULT_CONNECTION_DIAMETER) : (Width > 0 ? Math.Max(Width - DIAMETER_INSET, 0.001) : DEFAULT_CONNECTION_DIAMETER);
            var height = ShapeType == Elements.Fittings.ShapeType.Circle ? width : (Height > 0 ? Math.Max(Height - DIAMETER_INSET, 0.001) : width);
            var circle = Elements.Fittings.PipeProfile.Create(Diameter, width, height, ShapeType);

            var s = new Sweep(circle, Path(), 0, 0, 0, false);
            this.Representation.SolidOperations.Add(s);
        }

        public Connection(Node start, Node end, Guid id, string name)
            : this(start, end, 0, 0)
        {
            Id = id;
            Name = name;
        }

        public Connection(Node start, Node end, double width, double height, Elements.Fittings.ShapeType shapeType, double flow = 0)
            : this(start, end, shapeType == Elements.Fittings.ShapeType.Circle ? Math.Max(width, height) : Math.Sqrt(width * height), flow)
        {
            SetShape(width, height, shapeType);
        }

        public Vector3 Direction()
        {
            return (this.End.Position - this.Start.Position).Unitized();
        }

        public double Length()
        {
            return (this.End.Position - this.Start.Position).Length();
        }

        public Line Path()
        {
            return new Line(this.Start.Position, this.End.Position);
        }

        public override string ToString()
        {
            return $"Connection-Size: {this.Width}x{this.Height}m ({this.ShapeType}) Start: {this.Start.Position} Direction: {this.Direction()} End: {this.End.Position}";
        }
    }
}
