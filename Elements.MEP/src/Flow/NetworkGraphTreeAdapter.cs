using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;

namespace Elements.Flow
{
    /// <summary>Explicit interoperability between the general graph and the existing tree APIs.</summary>
    public static class NetworkGraphTreeAdapter
    {
        /// <summary>Creates a graph that preserves the Tree's topology, profiles, loop markers, and inlet flow.</summary>
        public static NetworkGraph FromTree(Tree tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            var graph = new NetworkGraph { Name = tree.Name, Purpose = tree.Purpose };
            foreach (var reference in tree.RegionReferences ?? new List<string>()) graph.RegionReferences.Add(reference);
            var nodes = new Dictionary<Node, NetworkGraphNode>();
            foreach (var inlet in tree.Inlets)
            {
                var graphNode = graph.AddNode(new NetworkGraphNode(inlet.Position, NetworkGraphNodeKind.Source, inlet.Name, inlet.Id));
                graphNode.ExternalFlow = inlet.Flow;
                nodes[inlet] = graphNode;
            }
            var outlet = graph.AddNode(new NetworkGraphNode(tree.Outlet.Position, NetworkGraphNodeKind.Outlet, tree.Outlet.Name, tree.Outlet.Id));
            outlet.FixedPressure = 0;
            nodes[tree.Outlet] = outlet;
            foreach (var node in tree.InternalNodes)
            {
                nodes[node] = graph.AddNode(new NetworkGraphNode(node.Position, NetworkGraphNodeKind.Junction, node.Name, node.Id));
            }
            foreach (var connection in tree.Connections)
            {
                var start = graph.AddPort(nodes[connection.Start], connection.Name + "-start", NetworkGraphPortDirection.Outlet);
                var end = graph.AddPort(nodes[connection.End], connection.Name + "-end", NetworkGraphPortDirection.Inlet);
                var edge = graph.Connect(start, end, 1.0, connection.Length(), connection.IsLoop == true, connection.Id, connection.Name);
                edge.Flow = connection.Flow;
                edge.ShapeType = connection.ShapeType;
                edge.Width = connection.Width;
                edge.Height = connection.Height;
                edge.Diameter = connection.Diameter;
            }
            return graph;
        }

        /// <summary>
        /// Converts a graph to a Tree when its directed topology is compatible with Tree's many-to-one constraints.
        /// Device nodes are intentionally rejected because Tree has no device-port representation.
        /// </summary>
        public static Tree ToTree(NetworkGraph graph, NetworkGraphNode outletNode = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (graph.Nodes.Any(n => n is NetworkGraphDeviceNode || n.Kind == NetworkGraphNodeKind.Device))
            {
                throw new InvalidOperationException("A graph containing device nodes cannot be converted to Tree without losing its device-port semantics.");
            }
            outletNode = outletNode ?? graph.Nodes.SingleOrDefault(n => n.Kind == NetworkGraphNodeKind.Outlet);
            if (outletNode == null) throw new InvalidOperationException("Select exactly one Outlet node when converting a graph to Tree.");
            if (!graph.Nodes.Contains(outletNode)) throw new ArgumentException("The outlet node is not in the graph.", nameof(outletNode));
            if (graph.Outgoing(outletNode).Any()) throw new InvalidOperationException("A Tree outlet cannot have outgoing connections.");
            foreach (var node in graph.Nodes)
            {
                if (graph.Outgoing(node).Count(e => !e.IsLoop) > 1)
                {
                    throw new InvalidOperationException("A Tree node can have at most one non-loop outgoing connection.");
                }
                if (node.Kind == NetworkGraphNodeKind.Source && graph.Incoming(node).Any())
                {
                    throw new InvalidOperationException("A Tree inlet cannot have incoming connections.");
                }
            }

            var regions = graph.RegionReferences.ToList();
            var inletNodes = graph.Nodes.Where(n => n.Kind == NetworkGraphNodeKind.Source).ToList();
            var internalNodes = graph.Nodes.Where(n => n != outletNode && n.Kind != NetworkGraphNodeKind.Source)
                                          .Select(n => new Node(n.Position, n.Id, n.Name)).ToList();
            var mappedNodes = internalNodes.ToDictionary(n => n.Id, n => (Node)n);
            var leaves = new List<Leaf>();
            foreach (var node in inletNodes)
            {
                var leaf = new Leaf(ExternalFlow(node), null, node.Position, node.Id, node.Name);
                leaves.Add(leaf); mappedNodes[node.Id] = leaf;
            }
            var outletFlow = inletNodes.Sum(ExternalFlow);
            var trunk = new Trunk(outletFlow, Tree.NetworkRefFromRegionRefs(regions), outletNode.Position, outletNode.Id, outletNode.Name);
            mappedNodes[outletNode.Id] = trunk;
            var connections = new List<Connection>();
            foreach (var edge in graph.Edges)
            {
                var connection = new Connection(mappedNodes[edge.Start.Node.Id], mappedNodes[edge.End.Node.Id], edge.Id, edge.Name)
                {
                    Diameter = edge.Diameter,
                    Flow = edge.Flow,
                    Width = edge.Width,
                    Height = edge.Height,
                    ShapeType = edge.ShapeType,
                    IsLoop = edge.IsLoop
                };
                connections.Add(connection);
            }
            return new Tree(internalNodes, trunk, leaves, connections, outletFlow, regions, graph.Purpose ?? string.Empty, new Transform(), Tree.CollectionMaterial, null, false, Guid.NewGuid(), graph.Name);
        }

        private static double ExternalFlow(NetworkGraphNode node)
        {
            return node.ExternalFlow + node.Ports.Where(p => p.FlowBoundary.HasValue).Sum(p => p.FlowBoundary.Value);
        }
    }
}