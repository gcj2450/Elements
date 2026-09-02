using System;
using System.Collections.Generic;
using System.Linq;

namespace Elements.Flow
{
    public static class PidModelToolkit
    {
        public static IEnumerable<PidElement> FindByTag(PidNetwork network, string tag) { if (network == null) throw new ArgumentNullException(nameof(network)); return network.Elements.Values.Where(e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase)); }
        public static IEnumerable<PidElement> FindByKind(PidNetwork network, params PidElementKind[] kinds) { if (network == null) throw new ArgumentNullException(nameof(network)); return kinds == null || kinds.Length == 0 ? network.Elements.Values : network.Elements.Values.Where(e => kinds.Contains(e.Kind)); }
        public static IEnumerable<PidElement> FindByProperty(PidNetwork network, string key, string value = null) { if (network == null) throw new ArgumentNullException(nameof(network)); if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A property key is required.", nameof(key)); return network.Elements.Values.Where(e => { string candidate; return e.Properties.TryGetValue(key, out candidate) && (value == null || string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)); }); }
    }
    public static class PidPipingToolkit
    {
        private static readonly PidRelationKind[] PipeKinds = { PidRelationKind.PipingConnection, PidRelationKind.ProcessFlow };
        public static PidRelation Connect(PidNetwork network, PidElement source, PidElement target, string name = null) { if (network == null) throw new ArgumentNullException(nameof(network)); return network.AddRelation(source, target, PidRelationKind.PipingConnection, name); }
        public static IReadOnlyList<PidElement> GetConnectedPiping(PidNetwork network, PidElement start)
        {
            Check(network, start); var found = new List<PidElement>(); var seen = new HashSet<Guid> { start.Id }; var queue = new Queue<PidElement>(); queue.Enqueue(start);
            while (queue.Count > 0) { var current = queue.Dequeue(); found.Add(current); foreach (var r in network.Outgoing(current, PipeKinds).Concat(network.Incoming(current, PipeKinds))) { var next = r.SourceId == current.Id ? network.Target(r) : network.Source(r); if (next != null && seen.Add(next.Id)) queue.Enqueue(next); } }
            return found;
        }
        public static IReadOnlyList<PidElement> PropagateProperty(PidNetwork network, PidElement start, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A property key is required.", nameof(key)); var changed = new List<PidElement>();
            foreach (var e in GetConnectedPiping(network, start).Where(e => IsPipingElement(e.Kind))) { e.Properties[key] = value ?? string.Empty; changed.Add(e); } return changed;
        }
        public static IReadOnlyList<PidElement> TraceDownstream(PidNetwork network, PidElement start)
        {
            Check(network, start); var result = new List<PidElement>(); var seen = new HashSet<Guid> { start.Id }; var queue = new Queue<PidElement>(); queue.Enqueue(start);
            while (queue.Count > 0) { var current = queue.Dequeue(); foreach (var r in network.Outgoing(current, PipeKinds)) { var next = network.Target(r); if (next != null && seen.Add(next.Id)) { result.Add(next); queue.Enqueue(next); } } } return result;
        }
        internal static bool IsPipingElement(PidElementKind kind) { return kind == PidElementKind.PipingSegment || kind == PidElementKind.PipingComponent || kind == PidElementKind.Valve || kind == PidElementKind.Nozzle || kind == PidElementKind.Port || kind == PidElementKind.Junction; }
        private static void Check(PidNetwork network, PidElement element) { if (network == null) throw new ArgumentNullException(nameof(network)); if (element == null || network.GetElement(element.Id) != element) throw new ArgumentException("The start element is not in this P&ID network.", nameof(element)); }
    }
    public enum PidValidationSeverity { Warning, Error }
    public enum PidValidationCode { DuplicateTag, DanglingPhysicalNode, DanglingPhysicalEdge, InconsistentPipingSystem, DanglingPipingElement }
    public sealed class PidValidationIssue { public PidValidationSeverity Severity { get; private set; } public PidValidationCode Code { get; private set; } public PidElement Element { get; private set; } public string Message { get; private set; } internal PidValidationIssue(PidValidationSeverity severity, PidValidationCode code, PidElement element, string message) { Severity = severity; Code = code; Element = element; Message = message; } }
    public static class PidValidator
    {
        public static IReadOnlyList<PidValidationIssue> Validate(PidNetwork network)
        {
            if (network == null) throw new ArgumentNullException(nameof(network)); var issues = new List<PidValidationIssue>();
            foreach (var group in network.Elements.Values.Where(e => !string.IsNullOrWhiteSpace(e.Tag)).GroupBy(e => e.Tag, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) foreach (var e in group) issues.Add(new PidValidationIssue(PidValidationSeverity.Error, PidValidationCode.DuplicateTag, e, "The P&ID tag '" + group.Key + "' is not unique."));
            foreach (var e in network.Elements.Values)
            {
                if (network.PhysicalNetwork != null && e.PhysicalNode != null && !network.PhysicalNetwork.Nodes.Contains(e.PhysicalNode)) issues.Add(new PidValidationIssue(PidValidationSeverity.Error, PidValidationCode.DanglingPhysicalNode, e, "The associated physical node is not in the NetworkGraph."));
                if (network.PhysicalNetwork != null && e.PhysicalEdges.Any(edge => !network.PhysicalNetwork.Edges.Contains(edge))) issues.Add(new PidValidationIssue(PidValidationSeverity.Error, PidValidationCode.DanglingPhysicalEdge, e, "An associated physical edge is not in the NetworkGraph."));
                if (PidPipingToolkit.IsPipingElement(e.Kind) && !network.Outgoing(e, PidRelationKind.PipingConnection).Any() && !network.Incoming(e, PidRelationKind.PipingConnection).Any()) issues.Add(new PidValidationIssue(PidValidationSeverity.Warning, PidValidationCode.DanglingPipingElement, e, "The piping element has no piping connection."));
            }
            foreach (var r in network.Relations.Where(r => r.Kind == PidRelationKind.PipingConnection)) { string a; string b; var source = network.Source(r); var target = network.Target(r); if (source != null && target != null && source.Properties.TryGetValue("System", out a) && target.Properties.TryGetValue("System", out b) && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) issues.Add(new PidValidationIssue(PidValidationSeverity.Error, PidValidationCode.InconsistentPipingSystem, target, "Connected piping elements have different System values.")); }
            return issues;
        }
    }
}
