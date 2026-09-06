using System;
using System.Collections.Generic;
using Software.Boll.EasyUtils;

namespace EasyOpenVR.Data.Manifest;

public class ActionManifestBuilder
{
    internal const string FallbackLanguage = "en_US";
    internal readonly ActionManifest ActionManifest = new();

    public ActionManifestBuilder AddVersion(int version, int minimumRequiredVersion)
    {
        ActionManifest.Version = version;
        ActionManifest.MinimumRequiredVersion = minimumRequiredVersion;
        return this;
    }

    public ActionManifestBuilder AddDefaultBindings(string type, string url)
    {
        var defaultBindings = new DefaultBindings
        {
            ControllerType = type,
            BindingUrl = url
        };
        ActionManifest.DefaultBindings.Add(defaultBindings);
        return this;
    }

    /// <summary>
    /// The <c>name</c> is the path of the action set.
    /// Action set names are of the form <c>/actions/actionsetname</c>
    /// </summary>
    /// <param name="name"></param>
    /// <param name="usage"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public ActionManifestBuilder AddActionSet(
        string name,
        ActionSetUsage usage,
        Action<ActionSetBuilder>? configure = null
    )
    {
        var actionSet = new ActionSet
        {
            Name = $"/actions/{name.ToLowerInvariant()}",
            Usage = usage
        };
        ActionManifest.ActionSets.Add(actionSet);
        configure?.Invoke(new ActionSetBuilder(this, actionSet));
        return this;
    }

    public JsonResult<ActionManifest> BuildAndSerialize()
    {
        var ctx = new ActionManifestJsonSerializerContext(ManifestJsonSerializerPreset.Options);
        var json = new JsonUtils(ctx);
        return json.Serialize(ActionManifest);
    }
}

public class ActionSetBuilder(ActionManifestBuilder root, ActionSet parent)
{
    /// <summary>
    /// The <c>name</c> is the path to an action.
    /// Paths take the form <c>/actions/actionsetname/in/actionname</c> for input actions or <c>/actions/actionsetname/out/actionname</c> for output actions (like haptics).
    /// Returns the generated action for reuse elsewhere, instead of the ActionBuilder, as we have the configure argument to perform additional build tasks.
    /// </summary>
    /// <param name="name"></param> 
    /// <param name="type"></param>
    /// <param name="direction"></param>
    /// <param name="requirement"></param>
    /// <param name="skeleton"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public ActionItem AddAction(
        string name,
        ActionType type = ActionType.Boolean,
        ActionDirection direction = ActionDirection.In,
        ActionRequirement requirement = ActionRequirement.Suggested,
        ActionSkeleton? skeleton = null,
        Action<ActionBuilder>? configure = null
    )
    {
        var actionItem = new ActionItem
        {
            Name = $"{parent.Name}/{Enum.GetName(direction)?.ToLowerInvariant()}/{name.ToLowerInvariant()}",
            Type = type,
            Requirement = requirement,
            Skeleton = skeleton
        };
        root.ActionManifest.Actions.Add(actionItem);
        configure?.Invoke(new ActionBuilder(root, actionItem));
        return actionItem;
    }

    /// <summary>
    /// The <c>languageTag</c> is the ISO-639-1 + ISO-3166-1 alpha-2 code for the locale that this section of the action manifest file refers to.
    /// <para>All localization entries use the path of the action or action set as the key and the localized string as the value. These strings will be shown to the user instead of the action or action set name whenever the user is using that language. If the user's language is not present, English strings will be used. Steam supports over 25 languages, users have come to expect that Applications present details in their native language.</para>
    /// </summary>
    /// <param name="languageTag"></param>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public ActionSetBuilder AddLocalization(string languageTag, string prompt)
    {
        var fixedLanguageTag = SharedUtils.FixLanguageTag(languageTag, ActionManifestBuilder.FallbackLanguage);
        var od = root.ActionManifest.Localization.Find(it => it.ContainsKey("language_tag") && it["language_tag"] == fixedLanguageTag);
        if (od == null)
        {
            od = new OrderedDictionary<string, string> { { "language_tag", fixedLanguageTag } };
            root.ActionManifest.Localization.Add(od);
        }

        od.Add(parent.Name, prompt);
        return this;
    }
}

public class ActionBuilder(ActionManifestBuilder root, ActionItem parent)
{
    public ActionBuilder AddLocalization(string languageTag, string prompt)
    {
        var fixedLanguageTag = SharedUtils.FixLanguageTag(languageTag, ActionManifestBuilder.FallbackLanguage);
        var od = root.ActionManifest.Localization.Find(it => it.ContainsKey("language_tag") && it["language_tag"] == fixedLanguageTag);
        if (od == null)
        {
            od = new OrderedDictionary<string, string> { { "language_tag", fixedLanguageTag } };
            root.ActionManifest.Localization.Add(od);
        }

        od.Add(parent.Name, prompt);
        return this;
    }
}