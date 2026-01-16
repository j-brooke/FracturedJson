using System.Text.Json;
using System.Text.Json.Serialization;
using FracturedJson;

namespace CliNew;

/// <summary>
/// Subclass of FracturedJsonOptions with compile-time JSON deserialization data.  This lets us read
/// config files without using reflection, since reflection doesn't go well with the trim compile option.
/// </summary>
[JsonSerializable(typeof(FracturedJsonOptions))]
[JsonSourceGenerationOptions(
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
internal partial class FjOptsSerializationContext : JsonSerializerContext
{
}
