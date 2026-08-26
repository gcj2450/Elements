using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;

namespace Elements.Flow
{
    public partial class Section
    {
        public static Material SectionMaterial = new Material("Section", new Color(0.2, 0.666, 1.0, 1.0));
        public static double DefaultRepresentationDiameter = 0.05;

        public Section(Tree tree, string initialDescriptorValue) : base(new Transform(), SectionMaterial, null, false, Guid.NewGuid(), "")
        {
            Tree = tree;
            SectionKey = initialDescriptorValue;
        }

        public override string ToString()
        {
            return $"Section: {SectionKey}, Flow: {Flow}, Network: {Tree.Purpose} {Tree.GetNetworkReference()}";
        }

        internal bool IsDirectlyUpstream(Section s)
        {
            return s.End == this.Start;
        }

        public override void UpdateRepresentations()
        {
            if (this.Representation == null)
            {
                this.Representation = new Representation(new List<SolidOperation>());
            }
            this.Representation.SolidOperations = new List<SolidOperation>();

            var connections = Tree?.GetConnectionsForSection(this) ?? Array.Empty<Connection>();

            if (connections.Length == 0)
            {
                var fallbackProfile = new Circle(new Vector3(), DefaultRepresentationDiameter / 2).ToPolygon(FlowSystemConstants.CIRCLE_SEGMENTS);
                foreach (var segment in this.Path.Segments())
                {
                    AddSweep(segment.Start, segment.End, fallbackProfile);
                }

                return;
            }

            foreach (var connection in connections)
            {
                var fallbackDiameter = connection.Diameter > 0 ? connection.Diameter : DefaultRepresentationDiameter;
                var width = connection.ShapeType == Fittings.ShapeType.Circle
                    ? fallbackDiameter
                    : (connection.Width > 0 ? connection.Width : fallbackDiameter);
                var height = connection.ShapeType == Fittings.ShapeType.Circle
                    ? fallbackDiameter
                    : (connection.Height > 0 ? connection.Height : fallbackDiameter);
                var profile = Fittings.PipeProfile.Create(fallbackDiameter, width, height, connection.ShapeType);
                AddSweep(connection.Start.Position, connection.End.Position, profile);
            }

            void AddSweep(Vector3 start, Vector3 end, Polygon profile)
            {
                if (end != start)
                {
                    var centerLine = new Line(end, start);
                    var pipe = new Sweep(profile, centerLine, 0, 0, 0, false);
                    Representation.SolidOperations.Add(pipe);
                    return;
                }

                Console.WriteLine("Start and end were the same");
            }
        }

    }
}