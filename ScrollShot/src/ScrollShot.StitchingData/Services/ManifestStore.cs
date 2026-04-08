using System.Text.Json;
using System.Text.Json.Serialization;
using ScrollShot.StitchingData.Models;

namespace ScrollShot.StitchingData.Services;

public static class ManifestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static StitchDatasetManifest Load(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<StitchDatasetManifest>(json, SerializerOptions)
               ?? throw new InvalidOperationException("The dataset manifest could not be deserialized.");
    }

    public static void Save(StitchDatasetManifest manifest, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        File.WriteAllText(manifestPath, json);
    }

    public static void SaveReplayReport(ReplayReport report, string reportPath)
    {
        ArgumentNullException.ThrowIfNull(report);

        var json = JsonSerializer.Serialize(report, SerializerOptions);
        File.WriteAllText(reportPath, json);
    }
}
