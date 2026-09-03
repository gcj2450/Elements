using System.Collections.Concurrent;
using Elements.Pipeline.Core;
using Elements.Serialization.glTF;

namespace Elements.Pipeline.Server;

public sealed class SceneStore
{
    public const long MaximumImportBytes = 50 * 1024 * 1024;
    private const int MaximumScenes = 8;

    private readonly ConcurrentDictionary<Guid, SceneRecord> _scenes = new();
    private readonly SemaphoreSlim _buildLock = new(1, 1);

    public async Task<SceneManifest> ImportAsync(string fileName, string json, CancellationToken cancellationToken)
    {
        await _buildLock.WaitAsync(cancellationToken);
        try
        {
            var result = await Task.Run(() => DuctSceneBuilder.Build(json), cancellationToken);
            var glb = await Task.Run(() => result.Model.ToGlTF(), cancellationToken);
            if (glb is null || glb.Length == 0)
            {
                throw new InvalidDataException("Elements did not produce a renderable glTF scene.");
            }

            var sceneId = Guid.NewGuid();
            var elements = result.Elements.Values
                .OrderBy(element => element.SourceType)
                .ThenBy(element => element.Number, StringComparer.OrdinalIgnoreCase)
                .Select(element => new ElementSummary(
                    element.Id,
                    element.Number,
                    element.SourceType,
                    element.System,
                    element.SubSystem,
                    element.LayerName,
                    element.Style))
                .ToArray();
            var typeCounts = elements
                .GroupBy(element => element.SourceType)
                .ToDictionary(group => group.Key, group => group.Count());
            var manifest = new SceneManifest(
                sceneId,
                Path.GetFileName(fileName),
                elements.Length,
                typeCounts,
                new[] { new SceneChunk("root", $"/api/scenes/{sceneId}/chunks/root.glb", elements.Length) },
                elements);

            _scenes[sceneId] = new SceneRecord(
                manifest,
                glb,
                result.Elements.ToDictionary(pair => pair.Key, pair => pair.Value.SourceJson),
                DateTimeOffset.UtcNow);
            RemoveOldScenes();
            return manifest;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public bool TryGetManifest(Guid sceneId, out SceneManifest manifest)
    {
        if (_scenes.TryGetValue(sceneId, out var scene))
        {
            manifest = scene.Manifest;
            return true;
        }

        manifest = null!;
        return false;
    }

    public bool TryGetGlb(Guid sceneId, out byte[] glb)
    {
        if (_scenes.TryGetValue(sceneId, out var scene))
        {
            glb = scene.Glb;
            return true;
        }

        glb = Array.Empty<byte>();
        return false;
    }

    public bool TryGetElementSource(Guid sceneId, Guid elementId, out string sourceJson)
    {
        if (_scenes.TryGetValue(sceneId, out var scene) && scene.SourceByElementId.TryGetValue(elementId, out sourceJson!))
        {
            return true;
        }

        sourceJson = string.Empty;
        return false;
    }

    private void RemoveOldScenes()
    {
        foreach (var scene in _scenes.Values.OrderByDescending(value => value.CreatedAt).Skip(MaximumScenes))
        {
            _scenes.TryRemove(scene.Manifest.SceneId, out _);
        }
    }

    private sealed record SceneRecord(
        SceneManifest Manifest,
        byte[] Glb,
        IReadOnlyDictionary<Guid, string> SourceByElementId,
        DateTimeOffset CreatedAt);
}
