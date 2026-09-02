using System;
using System.Collections.Generic;
using System.Linq;

namespace Elements.Flow
{
    public enum PidGraphAbstractionLevel { Complete, Process, Conceptual }
    public sealed class PidGraphViewRelation
    {
        public Guid SourceId { get; private set; } public Guid TargetId { get; private set; } public PidRelationKind Kind { get; private set; }
        public bool IsCollapsed { get; private set; } public IReadOnlyList<Guid> SourceRelationIds { get; private set; }
        internal PidGraphViewRelation(Guid source, Guid target, PidRelationKind kind, bool collapsed, IEnumerable<Guid> ids) { SourceId = source; TargetId = target; Kind = kind; IsCollapsed = collapsed; SourceRelationIds = ids.ToList(); }
    }
    public sealed class PidGraphView
    {
        public PidGraphAbstractionLevel Level { get; private set; } public IReadOnlyDictionary<Guid, PidElement> Elements { get; private set; } public IReadOnlyList<PidGraphViewRelation> Relations { get; private set; }
        internal PidGraphView(PidGraphAbstractionLevel level, IDictionary<Guid, PidElement> elements, IEnumerable<PidGraphViewRelation> relations) { Level = level; Elements = new Dictionary<Guid, PidElement>(elements); Relations = relations.ToList(); }
    }
    /// <summary>Builds complete, process and conceptual P&amp;ID views without modifying the source graph.</summary>
    public sealed class PidGraphAbstractor
    {
        public PidGraphView CreateView(PidNetwork network, PidGraphAbstractionLevel level)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            var retained = network.Elements.Values.Where(e => Keep(e.Kind, level)).ToDictionary(e => e.Id);
            var viewRelations = new List<PidGraphViewRelation>(); var seen = new HashSet<string>();
            foreach (var source in retained.Values)
            {
                var queue = new Queue<Step>();
                foreach (var relation in network.Outgoing(source)) queue.Enqueue(new Step(relation.TargetId, relation.Kind, new[] { relation.Id }, false));
                var visited = new HashSet<string>();
                while (queue.Count > 0)
                {
                    var step = queue.Dequeue(); if (!visited.Add(step.ElementId + "|" + step.Kind)) continue;
                    if (retained.ContainsKey(step.ElementId))
                    {
                        if ((level == PidGraphAbstractionLevel.Complete || step.Kind != PidRelationKind.Composition) && seen.Add(source.Id + "|" + step.ElementId + "|" + step.Kind)) viewRelations.Add(new PidGraphViewRelation(source.Id, step.ElementId, step.Kind, step.Collapsed, step.Ids));
                        continue;
                    }
                    var intermediate = network.GetElement(step.ElementId); if (intermediate == null) continue;
                    foreach (var next in network.Outgoing(intermediate)) queue.Enqueue(new Step(next.TargetId, step.Kind == PidRelationKind.Composition ? next.Kind : step.Kind, step.Ids.Concat(new[] { next.Id }), true));
                }
            }
            return new PidGraphView(level, retained, viewRelations);
        }
        private static bool Keep(PidElementKind kind, PidGraphAbstractionLevel level)
        {
            if (level == PidGraphAbstractionLevel.Complete) return true;
            if (level == PidGraphAbstractionLevel.Conceptual) return kind == PidElementKind.Equipment || kind == PidElementKind.ProcessUnit || kind == PidElementKind.ProcessStream;
            return kind == PidElementKind.Equipment || kind == PidElementKind.ProcessUnit || kind == PidElementKind.PipingComponent || kind == PidElementKind.Valve || kind == PidElementKind.Instrument || kind == PidElementKind.Controller || kind == PidElementKind.Actuator || kind == PidElementKind.ProcessStream || kind == PidElementKind.Junction;
        }
        private sealed class Step { public Guid ElementId { get; private set; } public PidRelationKind Kind { get; private set; } public IEnumerable<Guid> Ids { get; private set; } public bool Collapsed { get; private set; } public Step(Guid id, PidRelationKind kind, IEnumerable<Guid> ids, bool collapsed) { ElementId = id; Kind = kind; Ids = ids; Collapsed = collapsed; } }
    }
}
