using System;
using System.Collections.Generic;
using System.Linq;

namespace Elements.Flow
{
    public enum PidElementKind { Unknown, Equipment, ProcessUnit, PipingSegment, PipingComponent, Valve, Nozzle, Port, Junction, Instrument, Controller, Actuator, ProcessStream, Annotation, Group, Reference }
    public enum PidRelationKind { Composition, Reference, PipingConnection, ProcessFlow, Signal, InstrumentConnection, Association }

    /// <summary>A P&amp;ID item with optional associations to the physical flow graph.</summary>
    public sealed class PidElement
    {
        public Guid Id { get; private set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public PidElementKind Kind { get; set; }
        public NetworkGraphNode PhysicalNode { get; internal set; }
        public IList<NetworkGraphEdge> PhysicalEdges { get; } = new List<NetworkGraphEdge>();
        public IList<string> Classifications { get; } = new List<string>();
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public PidElement(PidElementKind kind, string tag = null, string name = null, Guid? id = null) { Id = id ?? Guid.NewGuid(); Kind = kind; Tag = tag ?? string.Empty; Name = name ?? string.Empty; }
    }

    /// <summary>A directed semantic relation. Unlike a physical edge, any number may join the same items.</summary>
    public sealed class PidRelation
    {
        public Guid Id { get; private set; }
        public Guid SourceId { get; private set; }
        public Guid TargetId { get; private set; }
        public PidRelationKind Kind { get; set; }
        public string Name { get; set; }
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public PidRelation(Guid sourceId, Guid targetId, PidRelationKind kind, string name = null, Guid? id = null) { Id = id ?? Guid.NewGuid(); SourceId = sourceId; TargetId = targetId; Kind = kind; Name = name ?? string.Empty; }
    }

    /// <summary>Semantic P&amp;ID graph layered over an optional <see cref="NetworkGraph"/>.</summary>
    public sealed class PidNetwork
    {
        private readonly Dictionary<Guid, PidElement> elements = new Dictionary<Guid, PidElement>();
        private readonly List<PidRelation> relations = new List<PidRelation>();
        public NetworkGraph PhysicalNetwork { get; private set; }
        public IReadOnlyDictionary<Guid, PidElement> Elements { get { return elements; } }
        public IReadOnlyList<PidRelation> Relations { get { return relations; } }
        public string Name { get; set; }
        public PidNetwork(NetworkGraph physicalNetwork = null, string name = null) { PhysicalNetwork = physicalNetwork; Name = name ?? string.Empty; }

        public PidElement CreateElement(PidElementKind kind, string tag = null, string name = null, NetworkGraphNode physicalNode = null) { return AddElement(new PidElement(kind, tag, name) { PhysicalNode = physicalNode }); }
        public PidElement AddElement(PidElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (elements.ContainsKey(element.Id)) throw new ArgumentException("An element with the same id already exists.", nameof(element));
            ValidateNode(element.PhysicalNode); foreach (var edge in element.PhysicalEdges) ValidateEdge(edge); elements.Add(element.Id, element); return element;
        }
        public bool RemoveElement(PidElement element, bool removePhysicalNode = false)
        {
            if (element == null || !elements.Remove(element.Id)) return false;
            relations.RemoveAll(r => r.SourceId == element.Id || r.TargetId == element.Id);
            if (removePhysicalNode && PhysicalNetwork != null && element.PhysicalNode != null) PhysicalNetwork.RemoveNode(element.PhysicalNode);
            return true;
        }
        public void AttachPhysicalNode(PidElement element, NetworkGraphNode node) { ValidateElement(element); ValidateNode(node); element.PhysicalNode = node; }
        public void AttachPhysicalEdge(PidElement element, NetworkGraphEdge edge) { ValidateElement(element); ValidateEdge(edge); if (!element.PhysicalEdges.Contains(edge)) element.PhysicalEdges.Add(edge); }
        public PidRelation AddRelation(PidElement source, PidElement target, PidRelationKind kind, string name = null)
        {
            ValidateElement(source); ValidateElement(target); var relation = new PidRelation(source.Id, target.Id, kind, name); relations.Add(relation); return relation;
        }
        public bool RemoveRelation(PidRelation relation) { return relation != null && relations.Remove(relation); }
        public PidElement GetElement(Guid id) { PidElement element; return elements.TryGetValue(id, out element) ? element : null; }
        public PidElement Source(PidRelation relation) { if (relation == null) throw new ArgumentNullException(nameof(relation)); return GetElement(relation.SourceId); }
        public PidElement Target(PidRelation relation) { if (relation == null) throw new ArgumentNullException(nameof(relation)); return GetElement(relation.TargetId); }
        public IEnumerable<PidRelation> Outgoing(PidElement element, params PidRelationKind[] kinds) { ValidateElement(element); return Filter(relations.Where(r => r.SourceId == element.Id), kinds); }
        public IEnumerable<PidRelation> Incoming(PidElement element, params PidRelationKind[] kinds) { ValidateElement(element); return Filter(relations.Where(r => r.TargetId == element.Id), kinds); }
        private static IEnumerable<PidRelation> Filter(IEnumerable<PidRelation> candidates, PidRelationKind[] kinds) { return kinds == null || kinds.Length == 0 ? candidates : candidates.Where(r => kinds.Contains(r.Kind)); }
        private void ValidateElement(PidElement element) { if (element == null || !elements.ContainsKey(element.Id) || !ReferenceEquals(elements[element.Id], element)) throw new ArgumentException("The element is not in this P&ID network.", nameof(element)); }
        private void ValidateNode(NetworkGraphNode node) { if (node != null && PhysicalNetwork != null && !PhysicalNetwork.Nodes.Contains(node)) throw new ArgumentException("The physical node is not in the associated NetworkGraph."); }
        private void ValidateEdge(NetworkGraphEdge edge) { if (edge != null && PhysicalNetwork != null && !PhysicalNetwork.Edges.Contains(edge)) throw new ArgumentException("The physical edge is not in the associated NetworkGraph."); }
    }
}
