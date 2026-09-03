using Elements.Pipeline.Core.Import;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Elements.Pipeline.Core;

public static class DuctSceneBuilder
{
    private static readonly (string JsonProperty, string SourceType)[] SourceCollections =
    {
        ("pipes", "Pipe"),
        ("elbows", "Elbow"),
        ("tees", "Tee"),
        ("crosses", "Cross"),
        ("reducers", "Reducer")
    };

    public static SceneBuildResult Build(string json, double unitScale = 0.001)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content is required.", nameof(json));
        }

        var root = JObject.Parse(json);
        var data = DuctDataForRevit.FromJson(json)
                   ?? throw new InvalidDataException("The JSON could not be parsed as duct data.");
        var model = RevitDuctJsonImporter.CreateModel(data, unitScale);
        var sources = ReadSources(root);
        var metadata = new Dictionary<Guid, ElementMetadata>();

        foreach (var element in model.Elements.Values)
        {
            if (string.IsNullOrWhiteSpace(element.Name) || !sources.TryGetValue(element.Name, out var source))
            {
                continue;
            }

            metadata[element.Id] = new ElementMetadata(
                element.Id,
                element.Name,
                source.SourceType,
                ReadString(source.Json, "System"),
                ReadString(source.Json, "SubSystem"),
                ReadString(source.Json, "LayerName"),
                ReadString(source.Json, "Style"),
                source.Json.ToString(Formatting.None));
        }

        return new SceneBuildResult(model, metadata);
    }

    private static Dictionary<string, SourceRecord> ReadSources(JObject root)
    {
        var result = new Dictionary<string, SourceRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var (jsonProperty, sourceType) in SourceCollections)
        {
            if (root.GetValue(jsonProperty, StringComparison.OrdinalIgnoreCase) is not JArray items)
            {
                continue;
            }

            foreach (var item in items.OfType<JObject>())
            {
                var number = ReadString(item, "Number");
                if (!string.IsNullOrWhiteSpace(number))
                {
                    result[number] = new SourceRecord(sourceType, item);
                }
            }
        }

        return result;
    }

    private static string ReadString(JObject value, string propertyName)
    {
        return value.GetValue(propertyName, StringComparison.OrdinalIgnoreCase)?.Value<string>() ?? string.Empty;
    }

    private sealed record SourceRecord(string SourceType, JObject Json);
}
