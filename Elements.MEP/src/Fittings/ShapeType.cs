using System;
using Elements.Geometry;

namespace Elements.Fittings
{
    /// <summary>Cross-sectional shape of a pipe or fitting port.</summary>
    public enum ShapeType
    {
        /// <summary>
        /// 圆形
        /// </summary>
        Circle = 0,
        /// <summary>
        /// 矩形
        /// </summary>
        Rectangle = 1,
        /// <summary>
        /// 椭圆形
        /// </summary>
        Oval = 2,
        /// <summary>
        /// 自定义
        /// </summary>
        Custom
    }

    internal static class PipeProfile
    {
        /// <summary>
        /// 创建端口形状Polygon，如果是shapeType=ShapeType.Custom(需要传入不为空且数量不为0的points，否则返回的是圆形)
        /// </summary>
        /// <param name="diameter"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="shapeType"></param>
        /// <param name="points">shapeType=ShapeType.Custom时需要</param>
        /// <returns></returns>
        public static Polygon Create(double diameter, double width, double height, ShapeType shapeType, System.Collections.Generic.List<Vector3> points = null)
        {
            var fallback = diameter > 0 ? diameter : 0.001;
            if (shapeType == ShapeType.Circle)
            {
                var circularDiameter = Math.Max(width, height);
                if (circularDiameter <= 0)
                {
                    circularDiameter = fallback;
                }
                return new Circle(circularDiameter / 2).ToPolygon(FlowSystemConstants.CIRCLE_SEGMENTS);
            }
            width = width > 0 ? width : fallback;
            height = height > 0 ? height : fallback;
            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    return Polygon.Rectangle(width, height);
                case ShapeType.Oval:
                    var vertices = new System.Collections.Generic.List<Vector3>(FlowSystemConstants.CIRCLE_SEGMENTS);
                    for (var i = 0; i < FlowSystemConstants.CIRCLE_SEGMENTS; i++)
                    {
                        var angle = 2 * Math.PI * i / FlowSystemConstants.CIRCLE_SEGMENTS;
                        vertices.Add(new Vector3(width * 0.5 * Math.Cos(angle), height * 0.5 * Math.Sin(angle)));
                    }
                    return new Polygon(vertices);
                case ShapeType.Custom:
                    if (points == null || points.Count == 0)
                    {
                        return new Circle(Math.Max(width, height) / 2).ToPolygon(FlowSystemConstants.CIRCLE_SEGMENTS); ;
                    }
                    else
                    {
                        return new Polygon(points);
                    }
                default:
                    return new Circle(Math.Max(width, height) / 2).ToPolygon(FlowSystemConstants.CIRCLE_SEGMENTS);
            }
        }

        public static Polygon Create(Port port)
        {
            return Create(port.Diameter, port.Width, port.Height, port.ShapeType);
        }
    }
}
