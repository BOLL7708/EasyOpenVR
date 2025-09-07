using System;
using System.Collections.Generic;

namespace EasyOpenVR.Settings;

public class Page
{
    // region JSON Properties
    /** The title of the settings section in SteamVR. */
    public string title;

    /** Set to true to allow this settings page to be shown even when no HMD is detected. */
    public bool show_without_hmd = true;

    /** The list of controls that are visible on the settings page. */
    public Control[] values = [];
    // endregion

    private readonly string _section;
    private readonly Dictionary<string, object?> _defaults = new();

    /**
     * Represents the JSON structure for a SteamVR settings page.
     *
     * Will process the registered controls and defaults.
     */
    public Page(string title, string section, params Factory[] controlFactories)
    {
        this.title = title;
        _section = section;

        _defaults.Clear();
        var processedControls = new List<Control>();
        foreach (var factory in controlFactories)
        {
            var control = factory.GetControl();
            var prefix = $"/settings/{_section}/";
            if (control.name != null && !control.name.StartsWith(prefix))
            {
                try
                {
                    var defaultValue = factory.GetDefaultValue();
                    if (defaultValue != null)
                    {
                        _defaults.Add(
                            control.name,
                            factory.GetDefaultValue()
                        );
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }

                control.name = $"{prefix}{control.name}";
            }

            processedControls.Add(control);
        }

        values = processedControls.ToArray();
    }

    /**
     * Before getting defaults, make sure to run Build to fill the dictionary.
     *
     * This is used to generate a default.vrsettings document based on registered controls.
     */
    public Dictionary<string, object?> GetDefaults()
    {
        return _defaults;
    }

    /**
     * Get the private section value.
     */
    public string GetSection()
    {
        return _section;
    }
}