using System.Collections.Generic;
using Software.Boll.EasyUtils;

namespace EasyOpenVR.Data.Manifest;

public partial class ActionManifestBuilder
{
    private readonly ActionManifest _actionManifest = new();

    public ActionManifestBuilder AddVersion(int version, int minimumRequiredVersion)
    {
        _actionManifest.Version = version;
        _actionManifest.MinimumRequiredVersion = minimumRequiredVersion;
        return this;
    }

    public ActionManifestBuilder AddDefaultBindings(string type, string url)
    {
        var defaultBindings = new DefaultBindings
        {
            ControllerType = type,
            BindingUrl = url
        };
        _actionManifest.DefaultBindings.Add(defaultBindings);
        return this;
    }

    /// <summary>
    /// The <c>name</c> is the path to an action.
    /// Paths take the form <c>/actions/actionsetname/in/actionname</c> for input actions or <c>/actions/actionsetname/out/actionname</c> for output actions (like haptics).
    /// </summary>
    /// <param name="name"></param> 
    /// <param name="type"></param>
    /// <param name="requirement"></param>
    /// <param name="skeleton"></param>
    /// <returns></returns>
    public ActionManifestBuilder AddAction(
        string name,
        ActionType type,
        ActionRequirement? requirement = null,
        ActionSkeleton? skeleton = null
    )
    {
        var actionItem = new ActionItem
        {
            Name = name,
            Type = type,
            Requirement = requirement,
            Skeleton = skeleton
        };
        _actionManifest.Actions.Add(actionItem);
        return this;
    }

    /// <summary>
    /// The <c>languageTag</c> is the ISO-639-1 + ISO-3166-1 alpha-2 code for the locale that this section of the action manifest file refers to.
    /// <para>All localization entries use the path of the action or action set as the key and the localized string as the value. These strings will be shown to the user instead of the action or action set name whenever the user is using that language. If the user's language is not present, English strings will be used. Steam supports over 25 languages, users have come to expect that Applications present details in their native language.</para>
    /// </summary>
    /// <param name="languageTag"></param>
    /// <param name="prompts"></param>
    /// <returns></returns>
    public ActionManifestBuilder AddLocalization(string languageTag, OrderedDictionary<string, string> prompts)
    {
        prompts.Remove("language_tag"); // To avoid exception on duplicate key
        prompts.Insert(0, "language_tag", SharedUtils.FixLanguageTag(languageTag)); // To put this at the top and format
        _actionManifest.Localization.Add(prompts);
        return this;
    }

    /// <summary>
    /// The <c>name</c> is the path of the action set.
    /// Action set names are of the form <c>/actions/actionsetname</c>
    /// </summary>
    /// <param name="name"></param>
    /// <param name="usage"></param>
    /// <returns></returns>
    public ActionManifestBuilder AddActionSet(string name, ActionSetUsage usage)
    {
        var actionSet = new ActionSet
        {
            Name = name,
            Usage = usage
        };
        _actionManifest.ActionSets.Add(actionSet);
        return this;
    }

    //  add Localization

    public JsonResult<ActionManifest> BuildAndSerialize()
    {
        var ctx = new Manifest.ActionManifestJsonSerializerContext(ManifestJsonSerializerPreset.Options);
        var json = new JsonUtils(ctx);
        return json.Serialize(_actionManifest);
    }
}