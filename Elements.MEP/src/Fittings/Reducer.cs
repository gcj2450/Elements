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
                                                                        base(false, FittingLocator.Empty(), new Transform(position),
                                                                            material == null ? FittingTreeRouting.DefaultFittingMaterial : material,
                                                                            new Representation(new List<SolidOperation>()),
                                                                            false,
                                                                            Guid.NewGuid(),
                                                                            "")
        {
            applyBranchTransform = true;
            this.Start = new Port(position + towardsStartDirection.Unitized() * length / 2, towardsStartDirection, widthStart, heightStart, shapeType);
            this.End = new Port(position - towardsStartDirection.Unitized() * length / 2, towardsStartDirection.Negate(), widthEnd, heightEnd, shapeType);
        }

        public Transform BranchSideTransform { get; protected set; } = new Transform();

        /// <summary>
        /// Create a reducer for a pipe.  Default is on Branchside, invert to place relative to Trunkside.
        /// </summary>
        public static Reducer ReducerForPipe(StraightSegment pipe, double reducerLength, bool reducerAtEnd, double newDiameter, double additionalDistance)
        {
            var distanceFromEnd = (reducerLength / 2) + additionalDistance;
            pipe.SetPath();

            var path = reducerAtEnd ? pipe.Path.Segments()[0].Reversed() : pipe.Path.Segments()[0];

            var position = path.DivideByLength(distanceFromEnd)[0].End;

            var orientation = path.Direction();
            // var fittingMaterial = new Material("green", new Color(0, 1, 0, 0.5);
            var fittingMaterial = FittingTreeRouting.DefaultFittingMaterial;
            if (pipe.ShapeType == ShapeType.Circle)
            {
                return new Reducer(position, reducerAtEnd ? orientation.Negate() : orientation, reducerAtEnd ? pipe.Diameter : newDiameter, reducerAtEnd ? newDiameter : pipe.Diameter, reducerLength, fittingMaterial);
            }

            var pipeWidth = pipe.Width > 0 ? pipe.Width : pipe.Diameter;
            var pipeHeight = pipe.Height > 0 ? pipe.Height : pipe.Diameter;
            var scale = Math.Sqrt(Math.Max(newDiameter, 0.000001) / Math.Max(pipe.Diameter, 0.000001));
            var newWidth = pipeWidth * scale;
            var newHeight = pipeHeight * scale;
            return new Reducer(position,
                               reducerAtEnd ? orientation.Negate() : orientation,
                               reducerAtEnd ? pipeWidth : newWidth,
                               reducerAtEnd ? pipeHeight : newHeight,
                               reducerAtEnd ? newWidth : pipeWidth,
                               reducerAtEnd ? newHeight : pipeHeight,
                               pipe.ShapeType,
                               reducerLength,
                               fittingMaterial);
        }

        public override void UpdateRepresentations()
        {
            if (Length().ApproximatelyEquals(0))
            {
                Representation = new Representation(new List<SolidOperation>());
                return;
            }
            var startNodeTransform = Transform.Concatenated(this.BranchSideTransform);
            var endNodeTransform = Transform;

            var startProfile = PipeProfile.Create(this.Start);
            var startLinePoint1 = Start.Position - Transform.Origin;
            var startLinePoint2 = startNodeTransform.Origin - Transform.Origin;
            var line = new Line(startLinePoint1, startLinePoint2);
            var sweep1 = new Sweep(startProfile, line, 0, 0, 0, false);

            var endProfile = PipeProfile.Create(this.End);
            var endLinePoint1 = End.Position - Transform.Origin;
            var endLinePoint2 = endNodeTransform.Origin - Transform.Origin;
            var otherLine = new Line(endLinePoint2, endLinePoint1);
            var sweep2 = new Sweep(endProfile, otherLine, 0, 0, 0, false);

            var branchSideTransformInverted = new Transform(this.BranchSideTransform);
            branchSideTransformInverted.Invert();

            var arrows = this.Start.GetArrow(branchSideTransformInverted.OfPoint(startNodeTransform.Origin))
                 .Concat(this.End.GetArrow(endNodeTransform.Origin)).Concat(GetExtensions());

            // TODO: Update the Factory pattern for setting representationInstances to work with Reducer transforms.
            // It would also be ideal to fully understand the geometry artifacts seen with certain Reducers that result in
            // bad boolean graphics which result in invisible or fractured geometry.
            var solidOperations = new List<SolidOperation> { sweep1, sweep2 }.Concat(arrows).ToList();
            foreach (var solidOperation in solidOperations)
            {
                this.RepresentationInstances.Add(new RepresentationInstance(new SolidRepresentation(solidOperation), this.Material));
            }
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
