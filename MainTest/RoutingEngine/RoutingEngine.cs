using Elements.Fittings;
using Elements.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoutingEngine.Core
{

    #region Routing Engine

    //没有自动打断功能

    public sealed class RoutingEngine
    {
        private readonly RoutingOptions _options;

        public RoutingEngine(
            RoutingOptions options = null)
        {
            _options =
                options ??
                new RoutingOptions();
        }

        public RoutingResult Build(
            IEnumerable<RoutePolyline> inputRoutes)
        {
            var routes = NormalizeRoutes(
                inputRoutes);

            var graph = BuildGraph(routes);

            ClassifyNodes(graph);

            var result = new RoutingResult
            {
                Graph = graph
            };

            GeneratePipeSegments(
                graph,
                result);

            GenerateJunctionComponents(
                graph,
                result);

            Validate(
                graph,
                result);

            return result;
        }

        #region Normalize

        private List<RoutePolyline> NormalizeRoutes(
            IEnumerable<RoutePolyline> input)
        {
            var result =
                new List<RoutePolyline>();

            foreach (var source in input)
            {
                if (source == null)
                    continue;

                if (source.Points == null ||
                    source.Points.Count < 2)
                    continue;

                var route = new RoutePolyline
                {
                    Id = source.Id ??
                         Guid.NewGuid().ToString(),

                    Profile =
                        source.Profile?.Clone()
                        ?? PipeProfile.Round(100),

                    SystemId = source.SystemId
                };

                Vector3? previous = null;

                foreach (var p in source.Points)
                {
                    if (previous.HasValue &&
                        Vector3.AlmostEqual(
                            previous.Value,
                            p,
                            _options.SnapTolerance))
                    {
                        continue;
                    }

                    route.Points.Add(p);

                    previous = p;
                }

                if (route.Points.Count >= 2)
                    result.Add(route);
            }

            return result;
        }

        #endregion

        #region Graph Construction

        private RouteGraph BuildGraph(
            List<RoutePolyline> routes)
        {
            var graph = new RouteGraph();

            foreach (var route in routes)
            {
                for (int i = 0;
                     i < route.Points.Count - 1;
                     i++)
                {
                    Vector3 a =
                        route.Points[i];

                    Vector3 b =
                        route.Points[i + 1];

                    if (Vector3.Distance(a, b) <
                        _options.MinimumPipeLength)
                        continue;

                    string nodeA =
                        FindOrCreateNode(
                            graph,
                            a);

                    string nodeB =
                        FindOrCreateNode(
                            graph,
                            b);

                    AddEdge(
                        graph,
                        nodeA,
                        nodeB,
                        route.Profile,
                        route.SystemId);
                }
            }

            if (_options.DetectIntersections)
            {
                SplitIntersections(graph);
            }

            RemoveDuplicateEdges(graph);

            return graph;
        }

        private string FindOrCreateNode(
            RouteGraph graph,
            Vector3 position)
        {
            foreach (var node in graph.Nodes.Values)
            {
                if (Vector3.Distance(
                    node.Position,
                    position)
                    <= _options.SnapTolerance)
                {
                    return node.Id;
                }
            }

            string id =
                "N" +
                (graph.Nodes.Count + 1);

            graph.Nodes.Add(
                id,
                new RouteNode
                {
                    Id = id,
                    Position = position
                });

            return id;
        }

        private void AddEdge(
            RouteGraph graph,
            string startNodeId,
            string endNodeId,
            PipeProfile profile,
            string systemId)
        {
            if (startNodeId == endNodeId)
                return;

            var a =
                graph.Nodes[startNodeId];

            var b =
                graph.Nodes[endNodeId];

            var direction =
                Vector3.NormalizeSafe(
                    b.Position - a.Position);

            string id =
                "E" +
                (graph.Edges.Count + 1);

            var edge = new RouteEdge
            {
                Id = id,

                StartNodeId =
                    startNodeId,

                EndNodeId =
                    endNodeId,

                Profile =
                    profile.Clone(),

                SystemId =
                    systemId,

                Length =
                    Vector3.Distance(
                        a.Position,
                        b.Position),

                Direction =
                    direction
            };

            graph.Edges.Add(id, edge);

            a.EdgeIds.Add(id);
            b.EdgeIds.Add(id);
        }

        #endregion

        #region Intersection

        private void SplitIntersections(
            RouteGraph graph)
        {
            /*
             * The first implementation deliberately focuses on
             * intersections between existing graph edges.
             *
             * For production-scale systems this should be replaced
             * by a spatial index such as an RTree/STRtree.
             */

            var edges =
                graph.Edges.Values.ToList();

            foreach (var e1 in edges)
            {
                foreach (var e2 in edges)
                {
                    if (e1.Id == e2.Id)
                        continue;

                    if (e1.Id.CompareTo(e2.Id) >= 0)
                        continue;

                    Vector3 p1 =
                        graph.Nodes[e1.StartNodeId].Position;

                    Vector3 p2 =
                        graph.Nodes[e1.EndNodeId].Position;

                    Vector3 q1 =
                        graph.Nodes[e2.StartNodeId].Position;

                    Vector3 q2 =
                        graph.Nodes[e2.EndNodeId].Position;

                    if (!Vector3.TrySegmentIntersection(
                        p1,
                        p2,
                        q1,
                        q2,
                        _options.IntersectionTolerance,
                        out Vector3 intersection,
                        out double t1,
                        out double t2))
                    {
                        continue;
                    }

                    if (t1 <= 1e-8 ||
                        t1 >= 1 - 1e-8)
                        continue;

                    if (t2 <= 1e-8 ||
                        t2 >= 1 - 1e-8)
                        continue;

                    SplitEdge(
                        graph,
                        e1,
                        intersection);

                    SplitEdge(
                        graph,
                        e2,
                        intersection);
                }
            }
        }

        private void SplitEdge(
            RouteGraph graph,
            RouteEdge edge,
            Vector3 position)
        {
            string oldStart =
                edge.StartNodeId;

            string oldEnd =
                edge.EndNodeId;

            PipeProfile profile =
                edge.Profile.Clone();

            string systemId =
                edge.SystemId;

            graph.Edges.Remove(edge.Id);

            graph.Nodes[oldStart]
                .EdgeIds.Remove(edge.Id);

            graph.Nodes[oldEnd]
                .EdgeIds.Remove(edge.Id);

            string middle =
                FindOrCreateNode(
                    graph,
                    position);

            AddEdge(
                graph,
                oldStart,
                middle,
                profile,
                systemId);

            AddEdge(
                graph,
                middle,
                oldEnd,
                profile,
                systemId);
        }

        #endregion

        #region Duplicate

        private void RemoveDuplicateEdges(
            RouteGraph graph)
        {
            var groups =
                graph.Edges.Values
                    .GroupBy(e =>
                    {
                        string a =
                            string.CompareOrdinal(
                                e.StartNodeId,
                                e.EndNodeId) < 0
                                ? e.StartNodeId
                                : e.EndNodeId;

                        string b =
                            a == e.StartNodeId
                                ? e.EndNodeId
                                : e.StartNodeId;

                        return a + "|" + b;
                    })
                    .ToList();

            foreach (var group in groups)
            {
                var first =
                    group.First();

                foreach (var duplicate in
                         group.Skip(1))
                {
                    graph.Nodes[
                        duplicate.StartNodeId]
                        .EdgeIds.Remove(
                            duplicate.Id);

                    graph.Nodes[
                        duplicate.EndNodeId]
                        .EdgeIds.Remove(
                            duplicate.Id);

                    graph.Edges.Remove(
                        duplicate.Id);
                }
            }
        }

        #endregion

        #region Classification

        private void ClassifyNodes(
            RouteGraph graph)
        {
            foreach (var node in graph.Nodes.Values)
            {
                node.JunctionType =
                    JunctionClassifier.Classify(
                        node,
                        graph,
                        _options.AngleToleranceDegrees);
            }
        }

        #endregion

        #region Pipe

        private void GeneratePipeSegments(
            RouteGraph graph,
            RoutingResult result)
        {
            foreach (var edge in graph.Edges.Values)
            {
                var start =
                    graph.Nodes[
                        edge.StartNodeId];

                var end =
                    graph.Nodes[
                        edge.EndNodeId];

                if (edge.Length <
                    _options.MinimumPipeLength)
                {
                    result.Diagnostics.Add(
                        new RoutingDiagnostic
                        {
                            Severity =
                                DiagnosticSeverity.Error,

                            Code =
                                "ZERO_LENGTH_PIPE",

                            Message =
                                "Pipe segment length is too small.",

                            EdgeId =
                                edge.Id
                        });

                    continue;
                }

                var pipe =
                    new Straight
                    {
                        Id =
                            "PIPE_" + edge.Id,

                        Start =
                            start.Position,

                        End =
                            end.Position,

                        Profile =
                            edge.Profile.Clone()
                    };

                pipe.Ports.Add(
                    CreatePort(
                        pipe.Id + "_P1",
                        start.Position,
                        Vector3.NormalizeSafe(
                            end.Position -
                            start.Position),
                        pipe.Profile,
                        pipe.Id));

                pipe.Ports.Add(
                    CreatePort(
                        pipe.Id + "_P2",
                        end.Position,
                        Vector3.NormalizeSafe(
                            start.Position -
                            end.Position),
                        pipe.Profile,
                        pipe.Id));

                result.Components.Add(pipe);
            }
        }

        #endregion

        #region Junction Components

        private void GenerateJunctionComponents(
            RouteGraph graph,
            RoutingResult result)
        {
            foreach (var node in graph.Nodes.Values)
            {
                switch (node.JunctionType)
                {
                    case JunctionType.Elbow:
                        GenerateElbow(
                            node,
                            graph,
                            result);
                        break;

                    case JunctionType.Tee:
                        GenerateTee(
                            node,
                            graph,
                            result);
                        break;

                    case JunctionType.Cross:
                        GenerateCross(
                            node,
                            graph,
                            result);
                        break;

                    case JunctionType.MultiWay:
                        result.Diagnostics.Add(
                            new RoutingDiagnostic
                            {
                                Severity =
                                    DiagnosticSeverity.Error,

                                Code =
                                    "UNSUPPORTED_JUNCTION",

                                Message =
                                    "Junction degree is greater than 4.",

                                NodeId =
                                    node.Id
                            });
                        break;
                }
            }
        }

        private void GenerateElbow(
            RouteNode node,
            RouteGraph graph,
            RoutingResult result)
        {
            var edges =
                node.EdgeIds
                    .Select(id => graph.Edges[id])
                    .ToList();

            var e1 = edges[0];
            var e2 = edges[1];

            Vector3 d1 =
                DirectionFromNode(
                    node,
                    e1,
                    graph);

            Vector3 d2 =
                DirectionFromNode(
                    node,
                    e2,
                    graph);

            double angle =
                Vector3.AngleDegrees(
                    d1,
                    d2);

            var elbow =
                new Elbow
                {
                    Id =
                        "ELBOW_" + node.Id,

                    Position =
                        node.Position,

                    AngleDegrees =
                        180.0 - angle,

                    BendRadius =
                        _options.DefaultBendRadius,

                    Profile =
                        e1.Profile.Clone()
                };

            elbow.Ports.Add(
                CreatePort(
                    elbow.Id + "_P1",
                    node.Position,
                    d1,
                    elbow.Profile,
                    elbow.Id));

            elbow.Ports.Add(
                CreatePort(
                    elbow.Id + "_P2",
                    node.Position,
                    d2,
                    elbow.Profile,
                    elbow.Id));

            result.Components.Add(elbow);
        }

        private void GenerateTee(
            RouteNode node,
            RouteGraph graph,
            RoutingResult result)
        {
            var tee =
                new Tee
                {
                    Id =
                        "TEE_" + node.Id,

                    Position =
                        node.Position,

                    Profile =
                        graph.Edges[
                            node.EdgeIds[0]]
                            .Profile.Clone()
                };

            foreach (string edgeId
                     in node.EdgeIds)
            {
                var edge =
                    graph.Edges[edgeId];

                Vector3 direction =
                    DirectionFromNode(
                        node,
                        edge,
                        graph);

                tee.Ports.Add(
                    CreatePort(
                        tee.Id +
                        "_P" +
                        (tee.Ports.Count + 1),

                        node.Position,

                        direction,

                        edge.Profile,

                        tee.Id));
            }

            result.Components.Add(tee);
        }

        private void GenerateCross(
            RouteNode node,
            RouteGraph graph,
            RoutingResult result)
        {
            var cross =
                new Cross
                {
                    Id =
                        "CROSS_" + node.Id,

                    Position =
                        node.Position,

                    Profile =
                        graph.Edges[
                            node.EdgeIds[0]]
                            .Profile.Clone()
                };

            foreach (string edgeId
                     in node.EdgeIds)
            {
                var edge =
                    graph.Edges[edgeId];

                Vector3 direction =
                    DirectionFromNode(
                        node,
                        edge,
                        graph);

                cross.Ports.Add(
                    CreatePort(
                        cross.Id +
                        "_P" +
                        (cross.Ports.Count + 1),

                        node.Position,

                        direction,

                        edge.Profile,

                        cross.Id));
            }

            result.Components.Add(cross);
        }

        #endregion

        #region Port

        private Port CreatePort(
            string id,
            Vector3 position,
            Vector3 direction,
            PipeProfile profile,
            string owner)
        {
            return new Port
            {
                Id = id,

                Position = position,

                Direction =
                    Vector3.NormalizeSafe(
                        direction),
                ShapeType = profile.Type,

                OwnerComponentId =
                    owner
            };
        }

        private Vector3 DirectionFromNode(
            RouteNode node,
            RouteEdge edge,
            RouteGraph graph)
        {
            RouteNode other;

            if (edge.StartNodeId == node.Id)
            {
                other =
                    graph.Nodes[
                        edge.EndNodeId];
            }
            else
            {
                other =
                    graph.Nodes[
                        edge.StartNodeId];
            }

            return Vector3.NormalizeSafe(
                other.Position -
                node.Position);
        }

        #endregion

        #region Validation

        private void Validate(
            RouteGraph graph,
            RoutingResult result)
        {
            ValidateNodes(
                graph,
                result);

            ValidateEdges(
                graph,
                result);

            ValidateComponents(
                result);
        }

        private void ValidateNodes(
            RouteGraph graph,
            RoutingResult result)
        {
            foreach (var node in graph.Nodes.Values)
            {
                if (node.Degree == 0)
                {
                    result.Diagnostics.Add(
                        new RoutingDiagnostic
                        {
                            Severity =
                                DiagnosticSeverity.Warning,

                            Code =
                                "ISOLATED_NODE",

                            Message =
                                "Node is isolated.",

                            NodeId =
                                node.Id
                        });
                }

                if (node.Degree > 4)
                {
                    result.Diagnostics.Add(
                        new RoutingDiagnostic
                        {
                            Severity =
                                DiagnosticSeverity.Error,

                            Code =
                                "INVALID_JUNCTION",

                            Message =
                                "Junction has more than four connections.",

                            NodeId =
                                node.Id
                        });
                }
            }
        }

        private void ValidateEdges(
            RouteGraph graph,
            RoutingResult result)
        {
            foreach (var edge in graph.Edges.Values)
            {
                if (edge.Length <
                    _options.MinimumPipeLength)
                {
                    result.Diagnostics.Add(
                        new RoutingDiagnostic
                        {
                            Severity =
                                DiagnosticSeverity.Error,

                            Code =
                                "SHORT_EDGE",

                            Message =
                                "Pipe segment is shorter than minimum length.",

                            EdgeId =
                                edge.Id
                        });
                }
            }
        }

        private void ValidateComponents(
            RoutingResult result)
        {
            foreach (var component
                     in result.Components)
            {
                if (component.Ports.Count == 0)
                {
                    result.Diagnostics.Add(
                        new RoutingDiagnostic
                        {
                            Severity =
                                DiagnosticSeverity.Error,

                            Code =
                                "NO_PORT",

                            Message =
                                "Component has no ports."
                        });
                }
            }
        }

        #endregion
    }

    #endregion
}
