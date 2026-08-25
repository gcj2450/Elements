using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Elements.Geometry;
using Elements.Geometry.Solids;

namespace Elements.Fittings
{
    public partial class Manifold
    {
        private double _size;

        public Manifold(Vector3 position, Vector3 trunkDirection, double trunkDiameter, List<(Vector3 direction, double diameter)> branches, Material material = null) :
            this(position,
                 trunkDirection,
                 trunkDiameter,
                 trunkDiameter,
                 ShapeType.Circle,
                 branches.Select(branch => (branch.direction, branch.diameter, branch.diameter, ShapeType.Circle)).ToList(),
                 material)
        {
        }

        public Manifold(Vector3 position,
                        Vector3 trunkDirection,
                        double trunkWidth,
                        double trunkHeight,
                        ShapeType trunkShapeType,
                        List<(Vector3 direction, double width, double height, ShapeType shapeType)> branches,
                        Material material = null) :
                                                                                         base(false,
                                                                                              FittingLocator.Empty(),
                                                                                              new Transform(),
                                                                                              material == null ? FittingTreeRouting.DefaultFittingMaterial : material,
                                                                                              new Representation(new List<SolidOperation>()),
                                                                                              false,
                                                                                              Guid.NewGuid(),
                                                                                              "")
        {
            this.Transform = new Transform(position);
            _size = Math.Max(Math.Max(trunkWidth, trunkHeight), branches.Max(branch => Math.Max(branch.width, branch.height))) * 1.5;
            var distance = _size / 2;
            this.Trunk = new Port(position + trunkDirection.Unitized() * distance,
                                  trunkDirection.Unitized(),
                                  trunkWidth,
                                  trunkHeight,
                                  trunkShapeType);
            this.Branches = new List<Port>();
            foreach (var (direction, width, height, shapeType) in branches)
            {
                Branches.Add(new Port(position + direction.Unitized() * distance,
                                      direction.Unitized(),
                                      width,
                                      height,
                                      shapeType));
            }
        }

        public override List<Port> BranchSidePorts()
        {
            return Branches.ToList();
        }

        public override Port[] GetPorts()
        {
            return new[] { Trunk }.Concat(Branches).ToArray();
        }

        public override Port TrunkSidePort()
        {
            return Trunk;
        }

        public override void UpdateRepresentations()
        {
            var extrude = new Extrude(Polygon.Rectangle(_size, _size).TransformedPolygon(new Transform(new Vector3(0, 0, -_size / 2))), _size, Vector3.ZAxis, false);
            var arrows = new List<Sweep>();
            arrows.AddRange(this.Trunk.GetArrow(this.Transform.Origin));
            foreach (var branch in Branches)
            {
                arrows.AddRange(branch.GetArrow(this.Transform.Origin));
            }
            var solidOps = new List<SolidOperation> { extrude }.Concat(arrows).Concat(GetExtensions()).ToList();
            this.Representation = new Geometry.Representation(solidOps);
        }

        public override Transform GetRotatedTransform()
        {
            throw new NotImplementedException();
        }
    }
}
