using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EasyOpenVR.Data.Manifest;

/// <a href="https://github.com/ValveSoftware/openvr/wiki/Action-manifest">Reference</a>
public class ActionManifest
{
    public int? Version;
    public int? MinimumRequiredVersion;
    public List<DefaultBindings> DefaultBindings = [];
    public List<ActionItem> Actions = [];
    public List<ActionSet> ActionSets = [];
    public List<OrderedDictionary<string, string>> Localization = [];
}

///  <a href="https://github.com/ValveSoftware/openvr/wiki/Action-manifest#default_bindings">Reference</a>
public class DefaultBindings
{
    /// This is the name of the controller type that this binding file is for.
    public string ControllerType = string.Empty;
    /// The URL or relative file path of the binding config file for this controller type. Relative paths are relative to the action manifest JSON file itself, so files in the same directory only need to provide a filename. Relative paths may not contain ".." , you must load your binding from the same directory as your action file or a sub-directory.
    public string BindingUrl = string.Empty;
}

/// <a href="https://github.com/ValveSoftware/openvr/wiki/Action-manifest#actions">Reference</a>  
public class ActionItem
{
    /// The path to an action. Paths take the form /actions/actionsetname/in/actionname for input actions or /actions/actionsetname/out/actionname for output actions (like haptics.) 
    public string Name = string.Empty;
    /// The type of the action. 
    public ActionType Type = ActionType.Boolean;
    /// The degree to which the user should be prompted to bind this action in the binding editor. 
    public ActionRequirement? Requirement;
    public ActionSkeleton? Skeleton;
}

/// <a href="https://github.com/ValveSoftware/openvr/wiki/Action-manifest#action-sets">Reference</a>  
public class ActionSet
{
    public string Name = string.Empty;
    public ActionSetUsage Usage = ActionSetUsage.Leftright;
}

public enum ActionType
{
    /// The action is a simple on/off event like pulling a trigger or pressing a switch.
    Boolean,
    /// The action is a 1-dimensional float. These are used for throttles, etc.
    Vector1,
    /// The action is a 2-dimensional float. These are used for position on a trackpad surface, etc.
    Vector2,
    /// The action is a 3-dimensional float. These are used for smooth motion, etc.
    Vector3,
    /// The action is a output haptic vibration. Include one of these per type of haptic output in your application so they can be bound to different output devices if the user has them available.
    Vibration,
    /// The action is the 6-DOF position and orientation of a device tracked by the tracking system.
    Pose,
    /// The action is used to retrieve bone transform data from controllers that support skeletal animation. Actions of type Skeleton also need to set the skeleton parameter to identify which skeleton they are expecting.
    Skeleton
}

public enum ActionDirection
{
    /// Inputs, e.g. buttons and sticks.
    In,
    /// Output, i.e. haptics.
    Out
}

public enum ActionSetUsage
{
    /// The user will see left and right hand controllers and be able to bind each one independently.
    Leftright,
    /// The user will see just one controller by default and anything bound on the left controller will automatically be copied to the right controller.
    Single,
    /// This action set will not be shown to the user.
    Hidden
}

public enum ActionRequirement
{
    /// The action must be bound or the binding file cannot be saved. Use this sparingly.
    Mandatory,
    /// The user will get a warning if this action is not bound. This is the default if no requirement is specified.
    Suggested,
    /// The user can bind this action, but will not be warned if it is not bound. This is often used for actions that cannot be bound on all hardware, or actions of a secondary nature.
    Optional
}

public enum ActionSkeleton
{
    [JsonStringEnumMemberName("/skeleton/hand/left")]
    SkeletonHandLeft,
    [JsonStringEnumMemberName("/skeleton/hand/right")]
    SkeletonHandRight
}

[JsonSerializable(typeof(ActionManifest))]
public partial class ActionManifestJsonSerializerContext : JsonSerializerContext;