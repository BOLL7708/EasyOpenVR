using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyOpenVR.Settings;

public class FauxDriverSettings
{
    private readonly Page[] _pages = [];
    private readonly Dictionary<string, object?> _defaults = new();

    private readonly JsonSerializerOptions _options = new()
    {
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FauxDriverSettings(params Page[] pages)
    {
        _pages = pages;
        foreach (var page in _pages)
        {
            try
            {
                _defaults.Add(page.GetSection(), page.GetDefaults());
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }
    }

    public string RenderManifest()
    {
        return "";
    }

    public string RenderSchema()
    {
        try
        {
            return JsonSerializer.Serialize(_pages, _options);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
        }

        return "";
    }

    public string RenderDefaults()
    {
        try
        {
            return JsonSerializer.Serialize(_defaults, _options);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }

        return "";
    }
}