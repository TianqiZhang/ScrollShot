using System.Text.Json;
using System.Text.Json.Serialization;
using ScrollShot.Tooling.Models;

namespace ScrollShot.Tooling.Services;

public static class BenchmarkSuiteStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static BenchmarkSuiteDefinition Load(string suitePath)
    {
        var json = File.ReadAllText(suitePath);
        return JsonSerializer.Deserialize<BenchmarkSuiteDefinition>(json, SerializerOptions)
               ?? throw new InvalidOperationException("The benchmark suite could not be deserialized.");
    }

    public static void SaveReport(BenchmarkSuiteReport report, string reportPath)
    {
        ArgumentNullException.ThrowIfNull(report);

        var json = JsonSerializer.Serialize(report, SerializerOptions);
        File.WriteAllText(reportPath, json);
    }
}
