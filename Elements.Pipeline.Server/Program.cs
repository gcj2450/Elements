using Elements.Pipeline.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddSingleton<SceneStore>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/scenes/import", async (HttpRequest request, SceneStore scenes, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected a multipart form containing a JSON file." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Select a non-empty JSON file." });
    }
    if (file.Length > SceneStore.MaximumImportBytes)
    {
        return Results.BadRequest(new { error = $"The JSON file exceeds the {SceneStore.MaximumImportBytes / 1024 / 1024} MB limit." });
    }

    try
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var json = await reader.ReadToEndAsync(cancellationToken);
        return Results.Ok(await scenes.ImportAsync(file.FileName, json, cancellationToken));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or Newtonsoft.Json.JsonException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/api/scenes/sample", async (SceneStore scenes, CancellationToken cancellationToken) =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "Samples", "Duct.json");
    if (!File.Exists(path))
    {
        return Results.NotFound(new { error = "The sample Duct.json file was not found." });
    }

    var json = await File.ReadAllTextAsync(path, cancellationToken);
    return Results.Ok(await scenes.ImportAsync("Duct.json", json, cancellationToken));
});

app.MapGet("/api/scenes/{sceneId:guid}/manifest", (Guid sceneId, SceneStore scenes) =>
    scenes.TryGetManifest(sceneId, out var manifest)
        ? Results.Ok(manifest)
        : Results.NotFound(new { error = "Scene not found." }));

app.MapGet("/api/scenes/{sceneId:guid}/chunks/{chunkId}.glb", (Guid sceneId, string chunkId, SceneStore scenes) =>
    chunkId == "root" && scenes.TryGetGlb(sceneId, out var glb)
        ? Results.File(glb, "model/gltf-binary", enableRangeProcessing: true)
        : Results.NotFound(new { error = "Scene chunk not found." }));

app.MapGet("/api/scenes/{sceneId:guid}/elements/{elementId:guid}", (Guid sceneId, Guid elementId, SceneStore scenes) =>
    scenes.TryGetElementSource(sceneId, elementId, out var sourceJson)
        ? Results.Content(sourceJson, "application/json")
        : Results.NotFound(new { error = "Element metadata not found." }));

app.MapFallbackToFile("index.html");

app.Run();
