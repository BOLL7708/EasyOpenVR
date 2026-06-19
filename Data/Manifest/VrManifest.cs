using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EasyOpenVR.Data.Manifest;

public class VrManifest
{
    public string Source = string.Empty;
    public List<Application> Applications = [];
}

public class Application
{
    public string AppKey = string.Empty;
    public LaunchTypeEnum LaunchType = LaunchTypeEnum.Binary;
    public string? Url = null;
    public string? BinaryPathWindows = null;
    public string? BinaryPathLinux = null;
    public string? BinaryPathOsx = null;
    public string? ActionManifestPath = null;
    public string? ImagePath = null;
    public bool? IsDashboardOverlay = false;
    public Dictionary<string, Strings> Strings = new();
}

public record struct Strings(
    string? Name,
    string? Description
);

public enum LaunchTypeEnum
{
    Binary,
    Url
}

[JsonSerializable(typeof(VrManifest))]
public partial class VrManifestJsonSerializerContext : JsonSerializerContext;