using System;
using System.Collections.Generic;

namespace Elements.Flow
{
    public enum PidPatternConnectorDirection { Inlet, Outlet }
    public sealed class PidPatternConnector
    {
        public string Name { get; private set; } public PidElement Element { get; private set; } public PidPatternConnectorDirection Direction { get; private set; } public PidRelationKind RelationKind { get; private set; } public bool IsActive { get; private set; }
        internal PidPatternConnector(string name, PidElement element, PidPatternConnectorDirection direction, PidRelationKind relationKind) { Name = name; Element = element; Direction = direction; RelationKind = relationKind; IsActive = true; }
        internal void Deactivate() { IsActive = false; }
    }
    public sealed class PidPatternInstance
    {
        private readonly PidNetwork network;
        public IReadOnlyDictionary<string, PidElement> Elements { get; private set; }
        public IReadOnlyDictionary<string, PidPatternConnector> Connectors { get; private set; }
        internal PidPatternInstance(PidNetwork network, IDictionary<string, PidElement> elements, IDictionary<string, PidPatternConnector> connectors) { this.network = network; Elements = new Dictionary<string, PidElement>(elements, StringComparer.OrdinalIgnoreCase); Connectors = new Dictionary<string, PidPatternConnector>(connectors, StringComparer.OrdinalIgnoreCase); }
        public PidRelation Connect(string ownConnector, PidPatternInstance counterpart, string counterpartConnector)
        {
            if (counterpart == null || counterpart.network != network) throw new ArgumentException("Both pattern instances must belong to the same P&ID network.", nameof(counterpart));
            PidPatternConnector own; PidPatternConnector other;
            if (!Connectors.TryGetValue(ownConnector, out own) || !counterpart.Connectors.TryGetValue(counterpartConnector, out other)) throw new ArgumentException("The requested connector does not exist.");
            if (!own.IsActive || !other.IsActive) throw new InvalidOperationException("A pattern connector can only be used once.");
            if (own.Direction == other.Direction || own.RelationKind != other.RelationKind) throw new InvalidOperationException("Pattern connectors must have opposite directions and the same relation kind.");
            var source = own.Direction == PidPatternConnectorDirection.Outlet ? own.Element : other.Element;
            var target = own.Direction == PidPatternConnectorDirection.Inlet ? own.Element : other.Element;
            var relation = network.AddRelation(source, target, own.RelationKind); own.Deactivate(); other.Deactivate(); return relation;
        }
    }
    /// <summary>Reusable P&amp;ID fragment with typed elements, internal relations and named interfaces.</summary>
    public sealed class PidPattern
    {
        private readonly Dictionary<string, Definition> elements = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RelationDefinition> relations = new List<RelationDefinition>();
        private readonly List<ConnectorDefinition> connectors = new List<ConnectorDefinition>();
        public string Name { get; private set; }
        public PidPattern(string name) { Name = name ?? string.Empty; }
        public PidPattern AddElement(string key, PidElementKind kind, string tag = null, string name = null, IDictionary<string, string> properties = null)
        {
            if (string.IsNullOrWhiteSpace(key) || elements.ContainsKey(key)) throw new ArgumentException("The element key is missing or already exists.", nameof(key)); elements.Add(key, new Definition(key, kind, tag, name, properties)); return this;
        }
        public PidPattern AddRelation(string source, string target, PidRelationKind kind, string name = null) { Check(source); Check(target); relations.Add(new RelationDefinition(source, target, kind, name)); return this; }
        public PidPattern AddConnector(string name, string element, PidPatternConnectorDirection direction, PidRelationKind kind = PidRelationKind.PipingConnection)
        {
            Check(element); if (string.IsNullOrWhiteSpace(name) || connectors.Exists(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("The connector name is missing or already exists.", nameof(name)); connectors.Add(new ConnectorDefinition(name, element, direction, kind)); return this;
        }
        public PidPatternInstance Instantiate(PidNetwork network, string tagPrefix = null)
        {
            if (network == null) throw new ArgumentNullException(nameof(network)); var result = new Dictionary<string, PidElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in elements.Values) { var tag = string.IsNullOrEmpty(tagPrefix) || string.IsNullOrEmpty(definition.Tag) ? definition.Tag : tagPrefix + definition.Tag; var element = network.CreateElement(definition.Kind, tag, definition.Name); foreach (var p in definition.Properties) element.Properties[p.Key] = p.Value; result.Add(definition.Key, element); }
            foreach (var relation in relations) network.AddRelation(result[relation.Source], result[relation.Target], relation.Kind, relation.Name);
            var interfaces = new Dictionary<string, PidPatternConnector>(StringComparer.OrdinalIgnoreCase); foreach (var connector in connectors) interfaces.Add(connector.Name, new PidPatternConnector(connector.Name, result[connector.Element], connector.Direction, connector.Kind));
            return new PidPatternInstance(network, result, interfaces);
        }
        private void Check(string key) { if (string.IsNullOrWhiteSpace(key) || !elements.ContainsKey(key)) throw new ArgumentException("The pattern does not contain the requested element key.", nameof(key)); }
        private sealed class Definition { public string Key; public PidElementKind Kind; public string Tag; public string Name; public IDictionary<string, string> Properties; public Definition(string key, PidElementKind kind, string tag, string name, IDictionary<string, string> properties) { Key = key; Kind = kind; Tag = tag ?? string.Empty; Name = name ?? string.Empty; Properties = properties == null ? new Dictionary<string, string>() : new Dictionary<string, string>(properties); } }
        private sealed class RelationDefinition { public string Source; public string Target; public PidRelationKind Kind; public string Name; public RelationDefinition(string source, string target, PidRelationKind kind, string name) { Source = source; Target = target; Kind = kind; Name = name ?? string.Empty; } }
        private sealed class ConnectorDefinition { public string Name; public string Element; public PidPatternConnectorDirection Direction; public PidRelationKind Kind; public ConnectorDefinition(string name, string element, PidPatternConnectorDirection direction, PidRelationKind kind) { Name = name; Element = element; Direction = direction; Kind = kind; } }
    }
}
