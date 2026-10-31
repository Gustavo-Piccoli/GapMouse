using System.Text.Json;
using System.Text.Json.Serialization;

namespace GapMouse.Infrastructure.State;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StateDocument))]
internal sealed partial class StateJsonContext : JsonSerializerContext;
