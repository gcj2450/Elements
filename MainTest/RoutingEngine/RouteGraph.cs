using Elements.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoutingEngine.Core
{
    #region Graph

    public sealed class RouteNode
    {
        public string Id { get; set; }

        public Vector3 Position { get; set; }

        public List<string> EdgeIds { get; } =
            new List<string>();

        public int Degree => EdgeIds.Count;

        public JunctionType JunctionType { get; set; }
    }

    public sealed class RouteEdge
    {
        public string Id { get; set; }

        public string StartNodeId { get; set; }

        public string EndNodeId { get; set; }

        public PipeProfile Profile { get; set; }

        public string SystemId { get; set; }

        public double Length { get; set; }

        public Vector3 Direction { get; set; }

        public bool IsGenerated { get; set; }
    }

    public sealed class RouteGraph
    {
        public Dictionary<string, RouteNode> Nodes { get; } =
            new Dictionary<string, RouteNode>();

        public Dictionary<string, RouteEdge> Edges { get; } =
            new Dictionary<string, RouteEdge>();

        public RouteNode GetNode(string id)
        {
            return Nodes[id];
        }

        public RouteEdge GetEdge(string id)
        {
            return Edges[id];
        }
    }

    #endregion


}
