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

    }
}
