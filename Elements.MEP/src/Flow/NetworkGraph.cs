using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Fittings;

namespace Elements.Flow
{
    /// <summary>Role of a node in a general flow graph.</summary>
    public enum NetworkGraphNodeKind { Junction, Source, Outlet, Device }

    /// <summary>Allowed flow direction at a graph port.</summary>
    public enum NetworkGraphPortDirection { Bidirectional, Inlet, Outlet }

    /// <summary>A connection point on a graph node.</summary>
    public sealed class NetworkGraphPort
    {
        public Guid Id { get; }
        public NetworkGraphNode Node { get; internal set; }
        public string Name { get; set; }
        public NetworkGraphPortDirection Direction { get; set; }
        public double? FlowBoundary { get; set; }
        public double? FixedPressure { get; set; }
        public double Flow { get; internal set; }
        public ShapeType ShapeType { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }

        public NetworkGraphPort(string name = null, NetworkGraphPortDirection direction = NetworkGraphPortDirection.Bidirectional, Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
            Name = name ?? string.Empty;
            Direction = direction;
            ShapeType = ShapeType.Circle;
        }
    }

    /// <summary>A node in a general, possibly cyclic, flow network.</summary>
    public class NetworkGraphNode
    {
        private readonly List<NetworkGraphPort> ports = new List<NetworkGraphPort>();
        public Guid Id { get; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public NetworkGraphNodeKind Kind { get; protected set; }
        public double? FixedPressure { get; set; }
        public double Pressure { get; internal set; }
        /// <summary>Positive values inject flow into the network.</summary>
        public double ExternalFlow { get; set; }
        public IReadOnlyList<NetworkGraphPort> Ports { get { return ports; } }

        public NetworkGraphNode(Vector3 position = new Vector3(), NetworkGraphNodeKind kind = NetworkGraphNodeKind.Junction, string name = null, Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
            Position = position;
            Kind = kind;
            Name = name ?? string.Empty;
        }

        public NetworkGraphPort AddPort(string name = null, NetworkGraphPortDirection direction = NetworkGraphPortDirection.Bidirectional)
        {
            var port = new NetworkGraphPort(name, direction) { Node = this };
            ports.Add(port);
            return port;
        }

        internal void AddPort(NetworkGraphPort port)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (port.Node != null && port.Node != this) throw new ArgumentException("The port already belongs to another node.", nameof(port));
            port.Node = this;
            if (!ports.Contains(port)) ports.Add(port);
        }

        public bool RemovePort(NetworkGraphPort port)
        {
            if (port == null || port.Node != this) return false;
            if (ports.Remove(port)) { port.Node = null; return true; }
            return false;
        }
    }

    /// <summary>A device node with arbitrary, dynamically managed ports.</summary>
    public sealed class NetworkGraphDeviceNode : NetworkGraphNode
    {
        public IDictionary<string, object> Properties { get; }
        public NetworkGraphDeviceNode(Vector3 position = new Vector3(), string name = null, Guid? id = null)
            : base(position, NetworkGraphNodeKind.Device, name, id)
        {
            Properties = new Dictionary<string, object>();
        }
    }

    /// <summary>An edge between two graph ports. Flow is positive from Start to End.</summary>
    public sealed class NetworkGraphEdge
    {
        public Guid Id { get; }
        public string Name { get; set; }
        public NetworkGraphPort Start { get; }
        public NetworkGraphPort End { get; }
        public double Length { get; set; }
        public double Resistance { get; set; }
        public double HazenWilliamsCoefficient { get; set; }
        public double Flow { get; internal set; }
        public bool IsLoop { get; set; }
        public ShapeType ShapeType { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }

        public NetworkGraphEdge(NetworkGraphPort start, NetworkGraphPort end, double resistance = 1.0, double length = 0, Guid? id = null, string name = null)
        {
            if (start == null || end == null) throw new ArgumentNullException(start == null ? nameof(start) : nameof(end));
            if (start.Node == null || end.Node == null) throw new ArgumentException("Both ports must belong to graph nodes.");
            if (start == end || start.Node == end.Node) throw new ArgumentException("An edge must connect two different ports on different nodes.");
            if (resistance <= 0) throw new ArgumentOutOfRangeException(nameof(resistance));
            Id = id ?? Guid.NewGuid(); Name = name ?? string.Empty; Start = start; End = end; Resistance = resistance; Length = length;
            ShapeType = ShapeType.Circle;
        }
    }

    /// <summary>Mutable general graph for flow networks.</summary>
    public sealed partial class NetworkGraph : ICloneable
    {
        private readonly List<NetworkGraphNode> nodes = new List<NetworkGraphNode>();
        private readonly List<NetworkGraphEdge> edges = new List<NetworkGraphEdge>();
        public IList<NetworkGraphNode> Nodes { get { return nodes; } }
        public IList<NetworkGraphEdge> Edges { get { return edges; } }
        public string Name { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public IList<string> RegionReferences { get; } = new List<string>();

        public T AddNode<T>(T node) where T : NetworkGraphNode
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (nodes.Any(n => n.Id == node.Id)) throw new ArgumentException("A node with the same id already exists.");
            nodes.Add(node); return node;
        }

        public NetworkGraphNode AddNode(Vector3 position = new Vector3(), NetworkGraphNodeKind kind = NetworkGraphNodeKind.Junction, string name = null)
        {
            return AddNode(new NetworkGraphNode(position, kind, name));
        }

        public NetworkGraphDeviceNode AddDevice(Vector3 position = new Vector3(), string name = null)
        {
            return AddNode(new NetworkGraphDeviceNode(position, name));
        }

        public bool RemoveNode(NetworkGraphNode node)
        {
            if (node == null || !nodes.Contains(node)) return false;
            foreach (var edge in edges.Where(e => e.Start.Node == node || e.End.Node == node).ToList()) RemoveEdge(edge);
            foreach (var port in node.Ports.ToList()) node.RemovePort(port);
            return nodes.Remove(node);
        }

        public NetworkGraphPort AddPort(NetworkGraphNode node, string name = null, NetworkGraphPortDirection direction = NetworkGraphPortDirection.Bidirectional)
        {
            if (node == null || !nodes.Contains(node)) throw new ArgumentException("The node is not in this graph.", nameof(node));
            return node.AddPort(name, direction);
        }

        public bool RemovePort(NetworkGraphPort port)
        {
            if (port == null || port.Node == null) return false;
            if (edges.Any(e => e.Start == port || e.End == port)) throw new InvalidOperationException("Remove all edges attached to the port first.");
            return port.Node.RemovePort(port);
        }
        public NetworkGraphEdge Connect(NetworkGraphPort start, NetworkGraphPort end, double resistance = 1.0, double length = 0, bool isLoop = false, Guid? id = null, string name = null)
        {
            ValidatePort(start); ValidatePort(end);
            if (edges.Any(e => e.Start == start || e.End == start || e.Start == end || e.End == end)) throw new InvalidOperationException("Each port can be attached to at most one edge.");
            var edge = new NetworkGraphEdge(start, end, resistance, length, id, name) { IsLoop = isLoop };
            edges.Add(edge); return edge;
        }

        public void RemoveEdge(NetworkGraphEdge edge)
        {
            if (edge == null || !edges.Remove(edge)) return;
            edge.Start.Flow = 0; edge.End.Flow = 0;
        }

        public IEnumerable<NetworkGraphEdge> Incident(NetworkGraphNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            return edges.Where(e => e.Start.Node == node || e.End.Node == node);
        }

        public IEnumerable<NetworkGraphEdge> Incoming(NetworkGraphNode node) { return Incident(node).Where(e => e.End.Node == node); }
        public IEnumerable<NetworkGraphEdge> Outgoing(NetworkGraphNode node) { return Incident(node).Where(e => e.Start.Node == node); }

        public NetworkGraphNode SplitEdgeThroughPoint(NetworkGraphEdge edge, Vector3 point, out NetworkGraphEdge[] newEdges)
        {
            if (edge == null || !edges.Contains(edge)) throw new ArgumentException("The edge is not in this graph.", nameof(edge));
            if (edge.Start.Node.Position.IsAlmostEqualTo(point) || edge.End.Node.Position.IsAlmostEqualTo(point)) throw new InvalidOperationException("The split point coincides with an endpoint.");
            var node = AddNode(new NetworkGraphNode(point));
            RemoveEdge(edge);
            var portA = node.AddPort("split-in");
            var portB = node.AddPort("split-out");
            var first = Connect(edge.Start, portA, edge.Resistance / 2.0, edge.Length / 2.0, edge.IsLoop);
            var second = Connect(portB, edge.End, edge.Resistance / 2.0, edge.Length / 2.0, edge.IsLoop);
            first.ShapeType = second.ShapeType = edge.ShapeType;
            first.Width = second.Width = edge.Width;
            first.Height = second.Height = edge.Height;
            first.Diameter = second.Diameter = edge.Diameter;
            first.HazenWilliamsCoefficient = second.HazenWilliamsCoefficient = edge.HazenWilliamsCoefficient;
            newEdges = new[] { first, second };
            return node;
        }

        public IList<IList<NetworkGraphNode>> FindCycles()
        {
            var result = new List<IList<NetworkGraphNode>>();
            var path = new List<NetworkGraphNode>();
            var visited = new HashSet<NetworkGraphNode>();
            foreach (var node in nodes) FindCycles(node, null, path, visited, result);
            return result;
        }

        private void FindCycles(NetworkGraphNode node, NetworkGraphNode parent, List<NetworkGraphNode> path, HashSet<NetworkGraphNode> visited, List<IList<NetworkGraphNode>> result)
        {
            if (path.Contains(node))
            {
                var cycle = path.Skip(path.IndexOf(node)).ToList();
                if (cycle.Count > 2) result.Add(cycle);
                return;
            }
            if (visited.Contains(node)) return;
            visited.Add(node);
            path.Add(node);
            foreach (var next in Incident(node).Select(e => e.Start.Node == node ? e.End.Node : e.Start.Node))
            {
                if (next != parent) FindCycles(next, node, path, visited, result);
            }
            path.RemoveAt(path.Count - 1);
        }

        private void ValidatePort(NetworkGraphPort port)
        {
            if (port == null || port.Node == null || !nodes.Contains(port.Node) || !port.Node.Ports.Contains(port)) throw new ArgumentException("The port is not in this graph.", nameof(port));
        }
        public NetworkGraph Clone()
        {
            var copy = new NetworkGraph { Name = Name, Purpose = Purpose };
            foreach (var reference in RegionReferences) copy.RegionReferences.Add(reference);
            var portMap = new Dictionary<NetworkGraphPort, NetworkGraphPort>();
            foreach (var node in nodes)
            {
                NetworkGraphNode clone;
                if (node is NetworkGraphDeviceNode)
                {
                    clone = new NetworkGraphDeviceNode(node.Position, node.Name, node.Id);
                }
                else
                {
                    clone = new NetworkGraphNode(node.Position, node.Kind, node.Name, node.Id);
                }
                clone.FixedPressure = node.FixedPressure;
                clone.ExternalFlow = node.ExternalFlow;
                clone.Pressure = node.Pressure;
                copy.AddNode(clone);
                foreach (var port in node.Ports)
                {
                    var clonedPort = clone.AddPort(port.Name, port.Direction);
                    clonedPort.FlowBoundary = port.FlowBoundary;
                    clonedPort.FixedPressure = port.FixedPressure;
                    clonedPort.Flow = port.Flow;
                    clonedPort.ShapeType = port.ShapeType;
                    clonedPort.Width = port.Width;
                    clonedPort.Height = port.Height;
                    clonedPort.Diameter = port.Diameter;
                    portMap[port] = clonedPort;
                }
                var device = node as NetworkGraphDeviceNode;
                var clonedDevice = clone as NetworkGraphDeviceNode;
                if (device != null && clonedDevice != null)
                {
                    foreach (var property in device.Properties) clonedDevice.Properties[property.Key] = property.Value;
                }
            }
            foreach (var edge in edges)
            {
                var clone = copy.Connect(portMap[edge.Start], portMap[edge.End], edge.Resistance, edge.Length, edge.IsLoop, edge.Id, edge.Name);
                clone.Flow = edge.Flow;
                clone.HazenWilliamsCoefficient = edge.HazenWilliamsCoefficient;
                clone.ShapeType = edge.ShapeType;
                clone.Width = edge.Width;
                clone.Height = edge.Height;
                clone.Diameter = edge.Diameter;
            }
            return copy;
        }

        object ICloneable.Clone() { return Clone(); }
        public static NetworkGraph FromTree(Tree tree) { return NetworkGraphTreeAdapter.FromTree(tree); }
        public Tree ToTree(NetworkGraphNode outletNode = null) { return NetworkGraphTreeAdapter.ToTree(this, outletNode); }
    }
}