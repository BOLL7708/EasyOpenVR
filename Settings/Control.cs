namespace EasyOpenVR.Settings;

public class Control
{
    /** Will be prepended with /settings/section/ when used */
    public string? name;

    /** Decides which type of control this represents */
    public string? control;

    /** The type of value that is expected, if any */
    public string? type;

    /** The visible label for this control */
    public string? label;

    /** Mouse over help text tooltip */
    public string? text;

    /** Appears to place controls below the first one with the group, indented, arbitrary name. */
    public string? group;

    /** A collection of options for select or radio controls */
    public Option[]? options;

    /** Will indicate a restart of SteamVR is required if changed */
    public bool? requires_restart;

    /** Only show this setting if advanced settings have been toggled on */
    public bool? advanced_only;

    /** Only show this setting if the platform is Windows */
    public bool? windows_only;

    /** Unsure */
    public string? on_label;

    /** Unsure */
    public string? off_label;
}