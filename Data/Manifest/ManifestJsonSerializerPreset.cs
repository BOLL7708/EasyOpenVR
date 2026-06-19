using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyOpenVR.Data.Manifest;

public static class ManifestJsonSerializerPreset
{
    public static JsonSerializerOptions Options => new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // To prevent ' to become \u0027
        IncludeFields = true, // Else the objects become empty due to the use of fields
        WriteIndented = true, // To make the manifest human-readable, and it matches what is common.
        PropertyNameCaseInsensitive = true, // Just convenient
        NumberHandling = JsonNumberHandling.AllowReadingFromString, // For safety
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, // To ensure compatibility with SteamVR
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // To simplify the output
        Converters =
        { // To ensure compatible values from enums
            // VrManifest
            new LowercaseEnumConverter<LaunchTypeEnum>(),
            // ActionManifest
            new LowercaseEnumConverter<ActionType>(),
            new LowercaseEnumConverter<ActionSetUsage>(),
            new LowercaseEnumConverter<ActionRequirement>(),
            new JsonStringEnumConverter<ActionSkeleton>()
        },
    };
}

public class LowercaseEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.SnakeCaseLower) where T : struct, Enum;