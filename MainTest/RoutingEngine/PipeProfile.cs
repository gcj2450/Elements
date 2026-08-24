using Elements.Fittings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RoutingEngine.Core
{
    #region Profile

    public sealed class PipeProfile
    {
        public ShapeType Type { get; set; }

        public double Diameter { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double WallThickness { get; set; }

        public string CustomProfileId { get; set; }

        public static PipeProfile Round(
            double diameter,
            double wallThickness = 0)
        {
            return new PipeProfile
            {
                Type = ShapeType.Circle,
                Diameter = diameter,
                WallThickness = wallThickness
            };
        }

        public static PipeProfile Rectangular(
            double width,
            double height,
            double wallThickness = 0)
        {
            return new PipeProfile
            {
                Type = ShapeType.Rectangle,
                Width = width,
                Height = height,
                WallThickness = wallThickness
            };
        }

        public PipeProfile Clone()
        {
            return new PipeProfile
            {
                Type = Type,
                Diameter = Diameter,
                Width = Width,
                Height = Height,
                WallThickness = WallThickness,
                CustomProfileId = CustomProfileId
            };
        }
    }

    #endregion

}
