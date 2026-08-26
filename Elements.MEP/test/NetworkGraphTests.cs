using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Fittings;
using Elements.Flow;
using Elements.Geometry;
using Xunit;

namespace Elements.MEP.Tests
{
    public class NetworkGraphTests
    {
        [Fact]
        public void DevicePortsAndNodeRemovalAreManagedByGraph()
        {
            var graph = new NetworkGraph();
            var device = graph.AddDevice(new Vector3(1, 0, 0), "Air handling unit");
            var ports = Enumerable.Range(0, 4).Select(i => graph.AddPort(device, "P" + i)).ToList();
            var source = graph.AddNode(new Vector3(0, 0, 0), NetworkGraphNodeKind.Source);
            var sourcePort = graph.AddPort(source, "s", NetworkGraphPortDirection.Outlet);
            var edge = graph.Connect(sourcePort, ports[0]);

            Assert.Equal(4, device.Ports.Count);
            Assert.Throws<InvalidOperationException>(() => graph.RemovePort(ports[0]));
            graph.RemoveEdge(edge);
            Assert.True(graph.RemovePort(ports[0]));
            Assert.Equal(3, device.Ports.Count);

            var anotherPort = graph.AddPort(source, "s2", NetworkGraphPortDirection.Outlet);
            graph.Connect(anotherPort, ports[1]);
            Assert.True(graph.RemoveNode(device));
            Assert.Empty(graph.Edges);
            Assert.DoesNotContain(device, graph.Nodes);
        }

        [Fact]
        public void SplittingAnEdgePreservesProfileAndHydraulicPath()
        {
            var graph = new NetworkGraph();
            var source = graph.AddNode(new Vector3(0, 0, 0), NetworkGraphNodeKind.Source);
            source.FixedPressure = 10;
            var outlet = graph.AddNode(new Vector3(2, 0, 0), NetworkGraphNodeKind.Outlet);
            outlet.FixedPressure = 0;
            var edge = Connect(graph, source, outlet);
            edge.ShapeType = ShapeType.Rectangle;
            edge.Width = 0.4;
            edge.Height = 0.2;
            var split = graph.SplitEdgeThroughPoint(edge, new Vector3(1, 0, 0), out var parts);

            Assert.Equal(3, graph.Nodes.Count);
            Assert.Equal(2, graph.Edges.Count);
            Assert.All(parts, part =>
            {
                Assert.Equal(0.5, part.Resistance, 8);
                Assert.Equal(ShapeType.Rectangle, part.ShapeType);
                Assert.Equal(0.4, part.Width, 8);
                Assert.Equal(0.2, part.Height, 8);
            });
            var result = new NetworkGraphFlowSolver().Solve(graph);
            Assert.True(result.Converged, result.Error);
            Assert.Equal(5, split.Pressure, 8);
            Assert.All(parts, part => Assert.Equal(10, part.Flow, 8));
        }
        [Fact]
        public void LinearSolverBalancesParallelPathsInALoop()
        {
            var graph = new NetworkGraph();
            var source = graph.AddNode(new Vector3(0, 0, 0), NetworkGraphNodeKind.Source);
            source.FixedPressure = 10;
            var top = graph.AddNode(new Vector3(1, 1, 0));
            var bottom = graph.AddNode(new Vector3(1, -1, 0));
            var outlet = graph.AddNode(new Vector3(2, 0, 0), NetworkGraphNodeKind.Outlet);
            outlet.FixedPressure = 0;
            var edges = new[]
            {
                Connect(graph, source, top), Connect(graph, top, outlet),
                Connect(graph, source, bottom), Connect(graph, bottom, outlet)
            };

            var result = new NetworkGraphFlowSolver().Solve(graph);

            Assert.True(result.Converged, result.Error);
            Assert.Equal(5, top.Pressure, 8);
            Assert.Equal(5, bottom.Pressure, 8);
            Assert.All(edges, edge => Assert.Equal(5, edge.Flow, 8));
            Assert.Single(graph.FindCycles());
        }

        [Fact]
        public void SolverSupportsNegativeEdgeFlowAndPortBoundaries()
        {
            var graph = new NetworkGraph();
            var a = graph.AddNode(new Vector3(0, 0, 0));
            var b = graph.AddNode(new Vector3(1, 0, 0));
            a.FixedPressure = 0;
            b.FixedPressure = 10;
            var edge = Connect(graph, a, b);
            graph.AddPort(a, "demand").FlowBoundary = -10;
            graph.AddPort(b, "supply").FlowBoundary = 10;

            var result = new NetworkGraphFlowSolver().Solve(graph);

            Assert.True(result.Converged, result.Error);
            Assert.Equal(-10, edge.Flow, 8);
            Assert.Equal(0, result.MaximumContinuityError, 8);
        }

        [Fact]
        public void HazenWilliamsModeSolvesPressureDrivenFlow()
        {
            var graph = new NetworkGraph();
            var source = graph.AddNode(new Vector3(0, 0, 0), NetworkGraphNodeKind.Source);
            source.FixedPressure = 10;
            var outlet = graph.AddNode(new Vector3(1, 0, 0), NetworkGraphNodeKind.Outlet);
            outlet.FixedPressure = 0;
            var edge = Connect(graph, source, outlet);
            var result = new NetworkGraphFlowSolver(new NetworkGraphFlowSolverSettings
            {
                FlowModel = NetworkGraphFlowModel.HazenWilliams
            }).Solve(graph);

            Assert.True(result.Converged, result.Error);
            Assert.Equal(Math.Pow(10, 1.0 / 1.852), edge.Flow, 8);
        }
        [Fact]
        public void TreeAdapterPreservesTreeFeaturesForCompatibleTopology()
        {
            var inlet = new Leaf(2, null, new Vector3(0, 0, 0), Guid.NewGuid(), "inlet");
            var middle = new Node(new Vector3(1, 0, 0), Guid.NewGuid(), "middle");
            var outlet = new Trunk(2, "A,2", new Vector3(2, 0, 0), Guid.NewGuid(), "outlet");
            var rectangular = new Connection(inlet, middle, 0.4, 0.2, ShapeType.Rectangle, 2) { Name = "Rectangular" };
            var oval = new Connection(middle, outlet, 0.3, 0.15, ShapeType.Oval, 2);
            var loop = new Connection(inlet, outlet, 0.1, 0.1, ShapeType.Circle, 0) { IsLoop = true };
            var tree = new Tree(new List<Node> { middle }, outlet, new List<Leaf> { inlet }, new List<Connection> { rectangular, oval, loop }, 2, new List<string> { "2", "A" }, "Supply");

            var roundTrip = NetworkGraphTreeAdapter.ToTree(NetworkGraphTreeAdapter.FromTree(tree));

            Assert.Equal(tree.Connections.Count, roundTrip.Connections.Count);
            Assert.Equal(tree.RegionReferences, roundTrip.RegionReferences);
            Assert.Equal(tree.Purpose, roundTrip.Purpose);
            Assert.Equal(2, roundTrip.Inlets.Single().Flow, 8);
            Assert.Contains(roundTrip.Connections, c => c.Id == rectangular.Id && c.Name == rectangular.Name);
            Assert.Contains(roundTrip.Connections, c => c.ShapeType == ShapeType.Rectangle && c.Width == 0.4 && c.Height == 0.2);
            Assert.Contains(roundTrip.Connections, c => c.ShapeType == ShapeType.Oval && c.Width == 0.3 && c.Height == 0.15);
            Assert.Single(roundTrip.GetLoopConnections());
            Assert.NotEmpty(roundTrip.GetSections());
        }

        [Fact]
        public void DeviceGraphCannotSilentlyLoseSemanticsWhenConvertedToTree()
        {
            var graph = new NetworkGraph();
            var device = graph.AddDevice();
            graph.AddPort(device, "port");
            Assert.Throws<InvalidOperationException>(() => graph.ToTree());
        }

        private static NetworkGraphEdge Connect(NetworkGraph graph, NetworkGraphNode start, NetworkGraphNode end)
        {
            return graph.Connect(graph.AddPort(start, "out", NetworkGraphPortDirection.Outlet), graph.AddPort(end, "in", NetworkGraphPortDirection.Inlet));
        }
    }
}