using Software.Boll.EasyUtils;

namespace EasyOpenVR.Data.Manifest;

public class VrManifestBuilder
{
    private readonly VrManifest _vrManifest = new();
    private readonly JsonUtils _jsonUtils = new(new Manifest.VrManifestJsonSerializerContext());

    public VrManifestBuilder(string source = "builtin")
    {
        _vrManifest.Source = source;
    }

    public VrManifestBuilder AddApplication(Application application)
    {
        _vrManifest.Applications.Add(application);
        return this;
    }

    public JsonResult<VrManifest> BuildAndSerialize()
    {
        var ctx = new Manifest.VrManifestJsonSerializerContext(ManifestJsonSerializerPreset.Options);
        var json = new JsonUtils(ctx);
        return json.Serialize(_vrManifest);
    }
}


public class ApplicationBuilder
{
    private readonly Application _application = new();

    public ApplicationBuilder(string appKey)
    {
        _application.AppKey = appKey;
    }

    public ApplicationBuilder SetBinaryPathWindows(string path)
    {
        _application.BinaryPathWindows = path;
        return this;
    }

    public ApplicationBuilder SetBinaryPathLinux(string path)
    {
        _application.BinaryPathLinux = path;
        return this;
    }

    public ApplicationBuilder SetBinaryPathOsx(string path)
    {
        _application.BinaryPathOsx = path;
        return this;
    }

    public ApplicationBuilder IsDashboardOverlay()
    {
        _application.IsDashboardOverlay = true;
        return this;
    }

    public ApplicationBuilder SetLaunchType(LaunchTypeEnum launchType)
    {
        _application.LaunchType = launchType;
        return this;
    }

    public ApplicationBuilder AddStrings(string posixLocale, Strings strings)
    {
        _application.Strings.Add(SharedUtils.FixLanguageTag(posixLocale), strings);
        return this;
    }

    public ApplicationBuilder SetImagePath(string imagePath)
    {
        _application.ImagePath = imagePath;
        return this;
    }

    public ApplicationBuilder SetActionManifestPath(string actionManifestPath)
    {
        _application.ActionManifestPath = actionManifestPath;
        return this;
    }

    public Application Build()
    {
        return _application;
    }
}