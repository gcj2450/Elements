using Elements.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoutingEngine.Core
{
    #region Junction

    public enum JunctionType
    {
        /// <summary>
        /// 管道末端
        /// </summary>
        Endpoint,
        /// <summary>
        /// 直管段
        /// </summary>
        Straight,
        /// <summary>
        /// 弯头
        /// </summary>
        Elbow,
        /// <summary>
        /// 三通
        /// </summary>
        Tee,
        /// <summary>
        /// 四通
        /// </summary>
        Cross,
        /// <summary>
        /// 变径
        /// </summary>
        Reducer,
        /// <summary>
        /// 天圆地方
        /// </summary>
        RoundRect,
        /// <summary>
        /// Represents a multi-way operation or configuration setting.
        /// </summary>
        /// <remarks>This class or member is used to handle scenarios where multiple pathways or options
        /// are available. It is designed to facilitate decision-making processes that involve more than two
        /// choices.</remarks>
        MultiWay,
        /// <summary>
        /// 不可用
        /// </summary>
        Invalid
    }

    public static class JunctionClassifier
    {
        public static JunctionType Classify(
            RouteNode node,
            RouteGraph graph,
            double angleTolerance)
        {
            int degree = node.Degree;

            if (degree == 0)
                return JunctionType.Invalid;

            if (degree == 1)
                return JunctionType.Endpoint;

            if (degree == 2)
            {
                RouteEdge a = graph.Edges[node.EdgeIds[0]];
                RouteEdge b = graph.Edges[node.EdgeIds[1]];

                Vector3 da = GetDirectionFromNode(node, a, graph);
                Vector3 db = GetDirectionFromNode(node, b, graph);

                if (Vector3.IsCollinear(
                    da,
                    db,
                    angleTolerance))
                {
                    return JunctionType.Straight;
                }

                return JunctionType.Elbow;
            }

            if (degree == 3)
                return JunctionType.Tee;

            if (degree == 4)
                return JunctionType.Cross;

            return JunctionType.MultiWay;
        }

        private static Vector3 GetDirectionFromNode(
            RouteNode node,
            RouteEdge edge,
            RouteGraph graph)
        {
            RouteNode other;

            if (edge.StartNodeId == node.Id)
                other = graph.Nodes[edge.EndNodeId];
            else
                other = graph.Nodes[edge.StartNodeId];

            return Elements.Geometry.Vector3.NormalizeSafe(
                other.Position -node.Position);
        }
    }

    #endregion

}
