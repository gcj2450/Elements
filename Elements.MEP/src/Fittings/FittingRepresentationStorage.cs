
using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;

namespace Elements.Fittings
{
    static class FittingRepresentationStorage
    {
        private static readonly Dictionary<string, List<RepresentationInstance>> _fittings = new Dictionary<string, List<RepresentationInstance>>();
        public static Dictionary<string, List<RepresentationInstance>> Fittings => _fittings;

        public static void SetFittingRepresentation(Fitting fitting, Func<IList<SolidOperation>> makeSolids)
        {
            var portShapes = fitting.GetPorts().Select(port => $"{port.ShapeType}-{port.Width}-{port.Height}");
            var hash = $"{fitting.GetRepresentationHash()}-{String.Join("-", portShapes)}";
            if (!_fittings.ContainsKey(hash))
            {
                var solids = makeSolids();
                _fittings.Add(hash, new List<RepresentationInstance> { new RepresentationInstance(new SolidRepresentation(solids), fitting.Material) });
            }
            fitting.RepresentationInstances = _fittings[hash];

            fitting.Transform = fitting.GetRotatedTransform().Concatenated(new Transform(fitting.Transform.Origin));
        }
    }
}
