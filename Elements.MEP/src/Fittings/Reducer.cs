using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;

namespace Elements.Fittings
{
    public partial class Reducer : IReducer
    {
        private bool applyBranchTransform = false;

        public Reducer(Vector3 position, Vector3 towardsStartDirection, double diameterEnd, double diameterStart, double length, Material material) :
            this(position, towardsStartDirection, diameterEnd, diameterEnd, diameterStart, diameterStart, ShapeType.Circle, length, material)
        {
        }

        public Reducer(Vector3 position, Vector3 towardsStartDirection, double widthEnd, double heightEnd, double widthStart, double heightStart, ShapeType shapeType, double length, Material material) :
            this(position, towardsStartDirection, widthEnd, heightEnd, shapeType, widthStart, heightStart, shapeType, length, material)
        {
        }

        /// <summary>
        /// Creates a reducer or transition whose two ports may have different dimensions and shapes.
        /// </summary>
        public Reducer(Vector3 position,
                       Vector3 towardsStartDirection,
                       double widthEnd,
                       double heightEnd,
                       ShapeType shapeTypeEnd,
                       double widthStart,
                       double heightStart,
                       ShapeType shapeTypeStart,
                       double length,
                       Material material) :
                                                                        base(false, FittingLocator.Empty(), new Transform(position),
                                                                            material == null ? FittingTreeRouting.DefaultFittingMaterial : material,
                                                                            new Representation(new List<SolidOperation>()),
                                                                            false,
                                                                            Guid.NewGuid(),
                                                                            "")
        {
            applyBranchTransform = true;
            this.Start = new Port(position + towardsStartDirection.Unitized() * length / 2, towardsStartDirection, widthStart, heightStart, shapeTypeStart);
            this.End = new Port(position - towardsStartDirection.Unitized() * length / 2, towardsStartDirection.Negate(), widthEnd, heightEnd, shapeTypeEnd);
        }

        public Transform BranchSideTransform { get; protected set; } = new Transform();

        /// <summary>
        /// Create a reducer for a pipe.  Default is on Branchside, invert to place relative to Trunkside.
        /// </summary>
        public static Reducer ReducerForPipe(StraightSegment pipe, double reducerLength, bool reducerAtEnd, double newDiameter, double additionalDistance)
        {
            if (pipe.ShapeType == ShapeType.Circle)
            {
                return ReducerForPipe(pipe, reducerLength, reducerAtEnd, newDiameter, newDiameter, ShapeType.Circle, additionalDistance);
            }

            var pipeWidth = pipe.Width > 0 ? pipe.Width : pipe.Diameter;
            var pipeHeight = pipe.Height > 0 ? pipe.Height : pipe.Diameter;
            var scale = Math.Sqrt(Math.Max(newDiameter, 0.000001) / Math.Max(pipe.Diameter, 0.000001));
            return ReducerForPipe(pipe,
                                  reducerLength,
                                  reducerAtEnd,
                                  pipeWidth * scale,
                                  pipeHeight * scale,
                                  pipe.ShapeType,
                                  additionalDistance);
        }

        /// <summary>
        /// Creates a reducer for a pipe using the complete target cross section.
        /// </summary>
        public static Reducer ReducerForPipe(StraightSegment pipe,
                                             double reducerLength,
                                             bool reducerAtEnd,
                                             double newWidth,
                                             double newHeight,
                                             ShapeType newShapeType,
                                             double additionalDistance)
        {
            var distanceFromEnd = (reducerLength / 2) + additionalDistance;
            pipe.SetPath();

            var path = reducerAtEnd ? pipe.Path.Segments()[0].Reversed() : pipe.Path.Segments()[0];
            var position = path.DivideByLength(distanceFromEnd)[0].End;
            var orientation = path.Direction();
            var fittingMaterial = FittingTreeRouting.DefaultFittingMaterial;
            var pipeWidth = pipe.Width > 0 ? pipe.Width : pipe.Diameter;
            var pipeHeight = pipe.Height > 0 ? pipe.Height : pipe.Diameter;

            return new Reducer(position,
                               reducerAtEnd ? orientation.Negate() : orientation,
                               reducerAtEnd ? pipeWidth : newWidth,
                               reducerAtEnd ? pipeHeight : newHeight,
                               reducerAtEnd ? pipe.ShapeType : newShapeType,
                               reducerAtEnd ? newWidth : pipeWidth,
                               reducerAtEnd ? newHeight : pipeHeight,
                               reducerAtEnd ? newShapeType : pipe.ShapeType,
                               reducerLength,
                               fittingMaterial);
        }

        public override void UpdateRepresentations()
        {
            if (Length().ApproximatelyEquals(0))
            {
                Representation = new Representation(new List<SolidOperation>());
                RepresentationInstances = new List<RepresentationInstance>();
                return;
            }

            var transition = new ConstructedSolid(CreateTransitionSolid());
            var branchSideTransformInverted = new Transform(BranchSideTransform);
            branchSideTransformInverted.Invert();
            var solidOperations = new List<SolidOperation> { transition }
                .Concat(Start.GetArrow(branchSideTransformInverted.OfPoint(Transform.Origin)))
                .Concat(End.GetArrow(Transform.Origin))
                .Concat(GetExtensions())
                .ToList();

            Representation = new Representation(new List<SolidOperation>());
            RepresentationInstances = solidOperations
                .Select(operation => new RepresentationInstance(new SolidRepresentation(operation), Material))
                .ToList();
        }

        private Solid CreateTransitionSolid()
        {
            var startProfile = PipeProfile.Create(Start);
            var endProfile = PipeProfile.Create(End);
            var isShapeTransition = Start.ShapeType != End.ShapeType;
            var vertexCount = isShapeTransition
                ? FlowSystemConstants.CIRCLE_SEGMENTS
                : Math.Max(startProfile.Vertices.Count, endProfile.Vertices.Count);
            var startVertices = CreateTransitionProfile(Start, startProfile, vertexCount, isShapeTransition);
            var endVertices = CreateTransitionProfile(End, endProfile, vertexCount, isShapeTransition);

            var axis = (Start.Position - End.Position).Unitized();
            var profileFrame = new Transform(Vector3.Origin, axis.Negate());
            var widthAxis = profileFrame.XAxis;
            var heightAxis = profileFrame.YAxis;
            var localStart = Start.Position - Transform.Origin;
            var localEnd = End.Position - Transform.Origin;

            var startRing = startVertices
                .Select(point => localStart + widthAxis * point.X + heightAxis * point.Y)
                .ToList();
            var endRing = endVertices
                .Select(point => localEnd + widthAxis * point.X + heightAxis * point.Y)
                .ToList();

            var solid = new Solid();
            solid.AddFace(new Polygon(startRing), mergeVerticesAndEdges: true);
            solid.AddFace(new Polygon(endRing), mergeVerticesAndEdges: true, reverse: true);
            for (var i = 0; i < vertexCount; i++)
            {
                var next = (i + 1) % vertexCount;
                solid.AddFace(new Polygon(new[]
                {
                    startRing[i],
                    endRing[i],
                    endRing[next]
                }), mergeVerticesAndEdges: true);
                solid.AddFace(new Polygon(new[]
                {
                    startRing[i],
                    endRing[next],
                    startRing[next]
                }), mergeVerticesAndEdges: true);
            }

            return solid;
        }

        private static List<Vector3> CreateTransitionProfile(Port port,
                                                              Polygon profile,
                                                              int count,
                                                              bool useAngularSampling)
        {
            if (!useAngularSampling || port.ShapeType == ShapeType.Custom)
            {
                return SampleProfile(profile, count);
            }

            var halfWidth = (port.Width > 0 ? port.Width : port.Diameter) / 2.0;
            var halfHeight = (port.Height > 0 ? port.Height : port.Diameter) / 2.0;
            var result = new List<Vector3>(count);
            for (var i = 0; i < count; i++)
            {
                var angle = 2.0 * Math.PI * i / count;
                var x = Math.Cos(angle);
                var y = Math.Sin(angle);
                if (port.ShapeType == ShapeType.Rectangle)
                {
                    var scale = 1.0 / Math.Max(Math.Abs(x) / halfWidth, Math.Abs(y) / halfHeight);
                    result.Add(new Vector3(x * scale, y * scale));
                }
                else
                {
                    result.Add(new Vector3(x * halfWidth, y * halfHeight));
                }
            }

            return result;
        }

        private static List<Vector3> SampleProfile(Polygon profile, int count)
        {
            if (profile.Vertices.Count == count)
            {
                return profile.Vertices.ToList();
            }

            var edges = profile.Segments();
            var lengths = edges.Select(edge => edge.Length()).ToArray();
            var perimeter = lengths.Sum();
            var result = new List<Vector3>(count);
            for (var i = 0; i < count; i++)
            {
                var distance = perimeter * i / count;
                var edgeIndex = 0;
                while (edgeIndex < lengths.Length - 1 && distance > lengths[edgeIndex])
                {
                    distance -= lengths[edgeIndex];
                    edgeIndex++;
                }

                var edge = edges[edgeIndex];
                var parameter = lengths[edgeIndex].ApproximatelyEquals(0) ? 0 : distance / lengths[edgeIndex];
                result.Add(edge.Start + (edge.End - edge.Start) * parameter);
            }

            return result;
        }

        public override void ApplyAdditionalTransform()
        {
            Transform.Concatenate(AdditionalTransform);
            if (applyBranchTransform)
            {
                Start.Position = this.BranchSideTransform.Concatenated(AdditionalTransform).OfPoint(Start.Position);
            }
            else
            {
                Start.Position = AdditionalTransform.OfPoint(Start.Position);
            }

            End.Position = AdditionalTransform.OfPoint(End.Position);

            applyBranchTransform = false;
            ClearAdditionalTransform();
        }

        public override Transform GetPropagatedTransform(TransformDirection transformDirection)
        {
            if (transformDirection == TransformDirection.TrunkToBranch && applyBranchTransform)
            {
                return BranchSideTransform.Concatenated(AdditionalTransform);
            }
            else
            {
                return AdditionalTransform;
            }
        }

        public override Port[] GetPorts()
        {
            return new[] { this.Start, this.End };
        }

        public override List<Port> BranchSidePorts()
        {
            return new List<Port> { Start };
        }

        public override Port TrunkSidePort()
        {
            return End;
        }

        public double Length()
        {
            return End.Position.DistanceTo(Transform.Origin) + Start.Position.DistanceTo(Transform.Origin);
        }

        public void Move(Vector3 translation)
        {
            Transform.Move(translation);
            Start.Position = Start.Position + translation;
            End.Position = End.Position + translation;
        }

        /// <summary>
        /// Port with smaller diameter points to the +X axis.
        /// If there is eccentric transform, the smaller part will be shifted to the -Z axis.
        /// We point smaller diameter in the +X direction so that there is one reducer defined in the standard orientation, to which this transformation is then applied.
        /// This let's us just have one size 110/90 that is rotated into a 90/110 orientation when needed.
        /// </summary>
        /// <returns>Rotated transform.</returns>
        public override Transform GetRotatedTransform()
        {
            var xAxis = Start.Diameter > End.Diameter ? End.Direction : Start.Direction;
            var largeConn = Start.Diameter > End.Diameter ? Start : End;
            var smallConn = Start.Diameter <= End.Diameter ? Start : End;
            var largeSideLine = new Line(largeConn.Position - largeConn.Direction * 100, largeConn.Position + largeConn.Direction * 100);
            smallConn.Position.DistanceTo(largeSideLine, out var smallConnectorProjectedToPosition);
            var smallSideDirection = smallConnectorProjectedToPosition - smallConn.Position;
            Vector3 zAxis;
            if (!smallSideDirection.IsZero())
            {
                zAxis = smallSideDirection;
            }
            else if (xAxis.IsParallelTo(Vector3.ZAxis))
            {
                zAxis = Vector3.XAxis;
            }
            else
            {
                zAxis = Vector3.ZAxis;
            }
            var t = new Transform(Vector3.Origin, xAxis, zAxis);
            return t;
        }
    }
}
