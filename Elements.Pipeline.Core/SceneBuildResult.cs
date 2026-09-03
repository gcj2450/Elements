using Elements;

namespace Elements.Pipeline.Core;

public sealed class SceneBuildResult
{
    public SceneBuildResult(Model model, IReadOnlyDictionary<Guid, ElementMetadata> elements)
    {
        Model = model;
        Elements = elements;
    }

    public Model Model { get; }

    public IReadOnlyDictionary<Guid, ElementMetadata> Elements { get; }
}

public sealed record ElementMetadata(
    Guid Id,
    string Number,
    string SourceType,
    string System,
    string SubSystem,
    string LayerName,
    string Style,
    string SourceJson);
