namespace Elements.Pipeline.Server;

public sealed record SceneManifest(
    Guid SceneId,
    string Name,
    int ElementCount,
    IReadOnlyDictionary<string, int> TypeCounts,
    IReadOnlyList<SceneChunk> Chunks,
    IReadOnlyList<ElementSummary> Elements);

public sealed record SceneChunk(string Id, string Uri, int ElementCount);

public sealed record ElementSummary(
    Guid Id,
    string Number,
    string SourceType,
    string System,
    string SubSystem,
    string LayerName,
    string Style);
