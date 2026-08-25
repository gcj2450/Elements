using System.Collections.Generic;
using Elements.Fittings;
using Elements.Geometry;
using Elements.Flow;
using Xunit;

namespace Elements.MEP.Tests
{
    public class ShapeTests
    {
        [Fact]
        public void StraightSegmentSupportsRectangleAndOvalProfiles()
        {
            var rectangleStart = new Port(Vector3.Origin, Vector3.XAxis, 0.4, 0.2, ShapeType.Rectangle);
            var rectangleEnd = new Port(new Vector3(2, 0, 0), Vector3.XAxis, 0.4, 0.2, ShapeType.Rectangle);
            var rectangle = new StraightSegment(0, rectangleEnd, rectangleStart);
            rectangle.UpdateRepresentations();
            Assert.Equal(ShapeType.Rectangle, rectangle.ShapeType);
            Assert.Equal(0.4, rectangle.Width);
            Assert.Equal(0.2, rectangle.Height);
            Assert.NotEmpty(rectangle.Representation.SolidOperations);

            var ovalStart = new Port(Vector3.Origin, Vector3.XAxis, 0.5, 0.25, ShapeType.Oval);
            var ovalEnd = new Port(new Vector3(2, 0, 0), Vector3.XAxis, 0.5, 0.25, ShapeType.Oval);
            var oval = new StraightSegment(0, ovalEnd, ovalStart);
            oval.UpdateRepresentations();
            Assert.Equal(ShapeType.Oval, oval.ShapeType);
            Assert.NotEmpty(oval.Representation.SolidOperations);
        }

        [Fact]
        public void FittingsPreserveNonCircularShape()
        {
            var elbow = new Elbow(Vector3.Origin, Vector3.XAxis, Vector3.YAxis, 0.3, 0.4, 0.2, ShapeType.Rectangle);
            elbow.UpdateRepresentations();
            Assert.Equal(ShapeType.Rectangle, elbow.ShapeType);
            Assert.Equal(0.4, elbow.Start.Width);
            Assert.Equal(0.2, elbow.End.Height);
            Assert.NotEmpty(elbow.Representation.SolidOperations);

            var reducer = new Reducer(Vector3.Origin, Vector3.XAxis, 0.2, 0.1, 0.4, 0.2, ShapeType.Oval, 0.3, null);
            reducer.UpdateRepresentations();
            Assert.Equal(ShapeType.Oval, reducer.Start.ShapeType);
            Assert.Equal(0.4, reducer.Start.Width);
        }

        [Fact]
        public void WyeAndCrossSupportRectangleAndOvalProfiles()
        {
            var wyeSettings = new WyeSettings
            {
                ShapeType = ShapeType.Rectangle,
                Width = 0.4,
                Height = 0.2,
                MainWidth = 0.3,
                MainHeight = 0.15,
                BranchWidth = 0.2,
                BranchHeight = 0.1
            };
            var wye = new Wye(Vector3.Origin,
                              Vector3.XAxis,
                              Vector3.XAxis.Negate(),
                              Vector3.YAxis,
                              wyeSettings,
                              null);
            wye.UpdateRepresentations();

            AssertPortShape(wye.Trunk, ShapeType.Rectangle, 0.4, 0.2);
            AssertPortShape(wye.MainBranch, ShapeType.Rectangle, 0.3, 0.15);
            AssertPortShape(wye.SideBranch, ShapeType.Rectangle, 0.2, 0.1);
            Assert.NotEmpty(wye.Representation.SolidOperations);

            var ovalWyeSettings = new WyeSettings
            {
                ShapeType = ShapeType.Oval,
                Width = 0.4,
                Height = 0.2,
                MainWidth = 0.3,
                MainHeight = 0.15,
                BranchWidth = 0.2,
                BranchHeight = 0.1
            };
            var ovalWye = new Wye(Vector3.Origin,
                                  Vector3.XAxis,
                                  Vector3.XAxis.Negate(),
                                  Vector3.YAxis,
                                  ovalWyeSettings,
                                  null);
            ovalWye.UpdateRepresentations();
            AssertPortShape(ovalWye.Trunk, ShapeType.Oval, 0.4, 0.2);
            AssertPortShape(ovalWye.MainBranch, ShapeType.Oval, ovalWyeSettings.MainWidth, ovalWyeSettings.MainHeight);
            AssertPortShape(ovalWye.SideBranch, ShapeType.Oval, ovalWyeSettings.BranchWidth, ovalWyeSettings.BranchHeight);
            Assert.NotEmpty(ovalWye.Representation.SolidOperations);
            Assert.NotEqual(wye.GetRepresentationHash(), ovalWye.GetRepresentationHash());

            var crossSettings = new CrossSettings
            {
                ShapeType = ShapeType.Oval,
                Width = 0.5,
                Height = 0.25,
                Width_A = 0.4,
                Height_A = 0.2,
                Width_B = 0.3,
                Height_B = 0.15,
                Width_C = 0.2,
                Height_C = 0.1
            };
            var cross = new Cross(Vector3.Origin,
                                  Vector3.XAxis,
                                  Vector3.XAxis.Negate(),
                                  Vector3.YAxis,
                                  Vector3.YAxis.Negate(),
                                  crossSettings);
            cross.UpdateRepresentations();

            AssertPortShape(cross.Trunk, ShapeType.Oval, 0.5, 0.25);
            AssertPortShape(cross.BranchA, ShapeType.Oval, 0.4, 0.2);
            AssertPortShape(cross.BranchB, ShapeType.Oval, 0.3, 0.15);
            AssertPortShape(cross.BranchC, ShapeType.Oval, 0.2, 0.1);
            Assert.NotEmpty(cross.Representation.SolidOperations);
        }

        [Fact]
        public void RemainingFittingsSupportRectangleAndOvalProfiles()
        {
            var manifold = new Manifold(Vector3.Origin,
                                        Vector3.XAxis,
                                        0.4,
                                        0.2,
                                        ShapeType.Rectangle,
                                        new List<(Vector3, double, double, ShapeType)>
                                        {
                                            (Vector3.XAxis.Negate(), 0.3, 0.15, ShapeType.Oval),
                                            (Vector3.YAxis, 0.2, 0.1, ShapeType.Rectangle)
                                        });
            manifold.UpdateRepresentations();

            AssertPortShape(manifold.Trunk, ShapeType.Rectangle, 0.4, 0.2);
            AssertPortShape(manifold.Branches[0], ShapeType.Oval, 0.3, 0.15);
            Assert.NotEmpty(manifold.Representation.SolidOperations);

            var socket = new ExpansionSocket(Vector3.Origin,
                                             Vector3.XAxis,
                                             1.0,
                                             0.4,
                                             0.2,
                                             ShapeType.Oval,
                                             0.1);
            socket.UpdateRepresentations();

            AssertPortShape(socket.Start, ShapeType.Oval, 0.4, 0.2);
            AssertPortShape(socket.End, ShapeType.Oval, 0.4, 0.2);
            Assert.NotEmpty(socket.Representation.SolidOperations);
        }

        [Fact]
        public void RoutingPreservesJunctionProfiles()
        {
            var routing = new FittingTreeRouting(new Tree(new string[0]));
            var junction = new Node(Vector3.Origin);
            var outgoing = Connection(new Vector3(1, 0, 0), junction, true, 0.4, 0.2, ShapeType.Rectangle);
            var main = Connection(new Vector3(-1, 0, 0), junction, false, 0.3, 0.15, ShapeType.Oval);
            var branch = Connection(new Vector3(0, -1, 0), junction, false, 0.2, 0.1, ShapeType.Rectangle);

            var wye = Assert.IsType<Wye>(routing.BranchPipe(main, branch, outgoing));
            AssertPortShape(wye.Trunk, ShapeType.Rectangle, 0.4, 0.2);
            AssertPortShape(wye.MainBranch, ShapeType.Oval, 0.3, 0.15);
            AssertPortShape(wye.SideBranch, ShapeType.Rectangle, 0.2, 0.1);

            var oppositeBranch = Connection(new Vector3(0, 1, 0), junction, false, 0.25, 0.125, ShapeType.Oval);
            var cross = Assert.IsType<Cross>(routing.ManifoldPipe(new[] { main, branch, oppositeBranch }, outgoing));
            AssertPortShape(cross.Trunk, ShapeType.Rectangle, 0.4, 0.2);
            Assert.Contains(cross.BranchSidePorts(), port => port.ShapeType == ShapeType.Oval && port.Width == 0.25);

            var manifold = Assert.IsType<Manifold>(routing.ManifoldPipe(new[] { branch, oppositeBranch }, outgoing));
            AssertPortShape(manifold.Trunk, ShapeType.Rectangle, 0.4, 0.2);
            Assert.Contains(manifold.Branches, port => port.ShapeType == ShapeType.Oval && port.Width == 0.25);
        }

        private static Connection Connection(Vector3 otherPosition,
                                             Node junction,
                                             bool outgoing,
                                             double width,
                                             double height,
                                             ShapeType shapeType)
        {
            var other = new Node(otherPosition);
            return outgoing
                ? new Connection(junction, other, width, height, shapeType)
                : new Connection(other, junction, width, height, shapeType);
        }

        private static void AssertPortShape(Port port, ShapeType shapeType, double width, double height)
        {
            Assert.Equal(shapeType, port.ShapeType);
            Assert.Equal(width, port.Width);
            Assert.Equal(height, port.Height);
        }
    }
}
