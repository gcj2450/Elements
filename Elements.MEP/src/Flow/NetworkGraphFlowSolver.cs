using System;
using System.Collections.Generic;
using System.Linq;

namespace Elements.Flow
{
    /// <summary>Hydraulic relation used by <see cref="NetworkGraphFlowSolver"/>.</summary>
    public enum NetworkGraphFlowModel { LinearResistance, HazenWilliams }

    /// <summary>Settings for pressure-based graph flow solving.</summary>
    public sealed class NetworkGraphFlowSolverSettings
    {
        public NetworkGraphFlowModel FlowModel { get; set; } = NetworkGraphFlowModel.LinearResistance;
        public int MaximumIterations { get; set; } = 100;
        public double FlowTolerance { get; set; } = 1e-8;
        public double MinimumFlowForLinearization { get; set; } = 1e-7;
    }

    /// <summary>Outcome of a graph flow solve.</summary>
    public sealed class NetworkGraphFlowResult
    {
        public bool Converged { get; internal set; }
        public int Iterations { get; internal set; }
        public double MaximumContinuityError { get; internal set; }
        public string Error { get; internal set; }
        public IDictionary<NetworkGraphNode, double> NodePressures { get; } = new Dictionary<NetworkGraphNode, double>();
        public IDictionary<NetworkGraphEdge, double> EdgeFlows { get; } = new Dictionary<NetworkGraphEdge, double>();
    }

    /// <summary>
    /// Solves flow in a general graph from nodal pressure and continuity equations.
    /// Positive external flow injects fluid into the graph, and positive edge flow is Start to End.
    /// </summary>
    public sealed class NetworkGraphFlowSolver
    {
        private const double HazenWilliamsExponent = 1.852;
        public NetworkGraphFlowSolverSettings Settings { get; }

        public NetworkGraphFlowSolver(NetworkGraphFlowSolverSettings settings = null)
        {
            Settings = settings ?? new NetworkGraphFlowSolverSettings();
        }

        public NetworkGraphFlowResult Solve(NetworkGraph graph)
        {
            var result = new NetworkGraphFlowResult();
            if (graph == null) { result.Error = "The graph is null."; return result; }
            if (!graph.Nodes.Any()) { result.Converged = true; return result; }
            if (graph.Edges.Any(e => e.Resistance <= 0)) { result.Error = "Every edge resistance must be greater than zero."; return result; }

            var fixedPressures = new Dictionary<NetworkGraphNode, double>();
            foreach (var node in graph.Nodes)
            {
                var values = node.Ports.Where(p => p.FixedPressure.HasValue).Select(p => p.FixedPressure.Value).ToList();
                if (node.FixedPressure.HasValue) values.Add(node.FixedPressure.Value);
                if (values.Any() && values.Any(v => Math.Abs(v - values[0]) > Settings.FlowTolerance))
                {
                    result.Error = "A graph node has conflicting fixed pressures.";
                    return result;
                }
                if (values.Any()) fixedPressures[node] = values[0];
            }

            foreach (var component in ConnectedComponents(graph))
            {
                if (!component.Any(n => fixedPressures.ContainsKey(n)))
                {
                    var injection = component.Sum(ExternalFlow);
                    if (Math.Abs(injection) > Settings.FlowTolerance)
                    {
                        result.Error = "Each connected component without a fixed-pressure node must have zero net external flow.";
                        return result;
                    }
                    fixedPressures[component[0]] = 0;
                }
            }

            var pressures = graph.Nodes.ToDictionary(n => n, n => fixedPressures.ContainsKey(n) ? fixedPressures[n] : n.Pressure);
            var flows = graph.Edges.ToDictionary(e => e, e => e.Flow);
            for (var iteration = 1; iteration <= Settings.MaximumIterations; iteration++)
            {
                var unknown = graph.Nodes.Where(n => !fixedPressures.ContainsKey(n)).ToList();
                var indices = unknown.Select((node, index) => new { node, index }).ToDictionary(x => x.node, x => x.index);
                var matrix = new double[unknown.Count, unknown.Count];
                var rhs = new double[unknown.Count];
                foreach (var node in unknown) rhs[indices[node]] = ExternalFlow(node);

                foreach (var edge in graph.Edges)
                {
                    Linearize(edge, flows[edge], out var conductance, out var offset);
                    AddEdgeEquation(edge.Start.Node, edge.End.Node, conductance, offset, fixedPressures, indices, matrix, rhs);
                }

                var solution = SolveLinearSystem(matrix, rhs);
                if (solution == null)
                {
                    result.Error = "The pressure equation matrix is singular. Add a fixed-pressure boundary for every disconnected component.";
                    return result;
                }
                foreach (var pair in fixedPressures) pressures[pair.Key] = pair.Value;
                foreach (var node in unknown) pressures[node] = solution[indices[node]];

                foreach (var edge in graph.Edges)
                {
                    var difference = pressures[edge.Start.Node] - pressures[edge.End.Node];
                    if (Settings.FlowModel == NetworkGraphFlowModel.LinearResistance)
                    {
                        flows[edge] = difference / edge.Resistance;
                    }
                    else
                    {
                        flows[edge] = Math.Sign(difference) * Math.Pow(Math.Abs(difference) / NonlinearResistance(edge), 1.0 / HazenWilliamsExponent);
                    }
                }

                var continuity = graph.Nodes.Where(node => !fixedPressures.ContainsKey(node) || Math.Abs(ExternalFlow(node)) > Settings.FlowTolerance).Select(node => Math.Abs(NetOutflow(graph, node, flows) - ExternalFlow(node))).DefaultIfEmpty(0).Max();
                result.Iterations = iteration;
                result.MaximumContinuityError = continuity;
                if (continuity <= Settings.FlowTolerance)
                {
                    result.Converged = true;
                    break;
                }
            }

            if (!result.Converged && Settings.FlowModel == NetworkGraphFlowModel.HazenWilliams)
            {
                result.Error = "The Hazen-Williams iteration did not converge within the configured iteration limit.";
            }
            foreach (var node in graph.Nodes) { node.Pressure = pressures[node]; result.NodePressures[node] = node.Pressure; }
            foreach (var edge in graph.Edges)
            {
                edge.Flow = flows[edge]; edge.Start.Flow = -edge.Flow; edge.End.Flow = edge.Flow;
                result.EdgeFlows[edge] = edge.Flow;
            }
            return result;
        }

        private double ExternalFlow(NetworkGraphNode node)
        {
            return node.ExternalFlow + node.Ports.Where(p => p.FlowBoundary.HasValue).Sum(p => p.FlowBoundary.Value);
        }

        private void Linearize(NetworkGraphEdge edge, double flow, out double conductance, out double offset)
        {
            if (Settings.FlowModel == NetworkGraphFlowModel.LinearResistance)
            {
                conductance = 1.0 / edge.Resistance;
                offset = 0;
                return;
            }
            var magnitude = Math.Max(Math.Abs(flow), Settings.MinimumFlowForLinearization);
            var resistance = NonlinearResistance(edge);
            var derivative = HazenWilliamsExponent * resistance * Math.Pow(magnitude, HazenWilliamsExponent - 1.0);
            conductance = 1.0 / derivative;
            var pressureDrop = resistance * Math.Sign(flow) * Math.Pow(Math.Abs(flow), HazenWilliamsExponent);
            offset = flow - conductance * pressureDrop;
        }

        private double NonlinearResistance(NetworkGraphEdge edge)
        {
            if (edge.HazenWilliamsCoefficient <= 0) return edge.Resistance;
            var diameter = edge.Diameter > 0 ? edge.Diameter : Math.Sqrt(Math.Max(edge.Width * edge.Height, 0));
            if (diameter <= 0) return edge.Resistance;
            var length = edge.Length > 0 ? edge.Length : 1.0;
            return 9810.0 * 10.67 * length /
                   (Math.Pow(edge.HazenWilliamsCoefficient, HazenWilliamsExponent) * Math.Pow(diameter, 4.87));
        }
        private static void AddEdgeEquation(NetworkGraphNode start, NetworkGraphNode end, double conductance, double offset, Dictionary<NetworkGraphNode, double> fixedPressures, Dictionary<NetworkGraphNode, int> indices, double[,] matrix, double[] rhs)
        {
            AddNodeTerm(start, end, conductance, offset, fixedPressures, indices, matrix, rhs);
            AddNodeTerm(end, start, conductance, -offset, fixedPressures, indices, matrix, rhs);
        }

        private static void AddNodeTerm(NetworkGraphNode node, NetworkGraphNode other, double conductance, double offset, Dictionary<NetworkGraphNode, double> fixedPressures, Dictionary<NetworkGraphNode, int> indices, double[,] matrix, double[] rhs)
        {
            int index;
            if (!indices.TryGetValue(node, out index)) return;
            matrix[index, index] += conductance;
            if (indices.TryGetValue(other, out var otherIndex)) matrix[index, otherIndex] -= conductance;
            else rhs[index] += conductance * fixedPressures[other];
            rhs[index] -= offset;
        }

        private static double NetOutflow(NetworkGraph graph, NetworkGraphNode node, Dictionary<NetworkGraphEdge, double> flows)
        {
            return graph.Incident(node).Sum(edge => edge.Start.Node == node ? flows[edge] : -flows[edge]);
        }

        private static List<List<NetworkGraphNode>> ConnectedComponents(NetworkGraph graph)
        {
            var components = new List<List<NetworkGraphNode>>();
            var seen = new HashSet<NetworkGraphNode>();
            foreach (var first in graph.Nodes)
            {
                if (!seen.Add(first)) continue;
                var component = new List<NetworkGraphNode>();
                var queue = new Queue<NetworkGraphNode>(); queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue(); component.Add(node);
                    foreach (var adjacent in graph.Incident(node).Select(e => e.Start.Node == node ? e.End.Node : e.Start.Node)) if (seen.Add(adjacent)) queue.Enqueue(adjacent);
                }
                components.Add(component);
            }
            return components;
        }

        private static double[] SolveLinearSystem(double[,] matrix, double[] rhs)
        {
            var n = rhs.Length;
            if (n == 0) return new double[0];
            var a = (double[,])matrix.Clone(); var b = (double[])rhs.Clone();
            for (var column = 0; column < n; column++)
            {
                var pivot = column;
                for (var row = column + 1; row < n; row++) if (Math.Abs(a[row, column]) > Math.Abs(a[pivot, column])) pivot = row;
                if (Math.Abs(a[pivot, column]) < 1e-12) return null;
                if (pivot != column)
                {
                    for (var c = column; c < n; c++) { var value = a[column, c]; a[column, c] = a[pivot, c]; a[pivot, c] = value; }
                    var rhsValue = b[column]; b[column] = b[pivot]; b[pivot] = rhsValue;
                }
                for (var row = column + 1; row < n; row++)
                {
                    var scale = a[row, column] / a[column, column];
                    for (var c = column; c < n; c++) a[row, c] -= scale * a[column, c];
                    b[row] -= scale * b[column];
                }
            }
            var solution = new double[n];
            for (var row = n - 1; row >= 0; row--)
            {
                var sum = b[row];
                for (var column = row + 1; column < n; column++) sum -= a[row, column] * solution[column];
                solution[row] = sum / a[row, row];
            }
            return solution;
        }
    }
}