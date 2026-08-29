using System;
using System.Formats.Asn1;
using System.Text.RegularExpressions;

namespace EasyOpenVR.Data;

public static partial class SharedUtils
{

    public static string FixLanguageTag(string tag, string fallback, bool forceLower = false)
    {
        if (string.IsNullOrWhiteSpace(tag)) return fallback;

        var parts = NonAlphaRegex().Split(tag);
        var validParts = Array.FindAll(parts, p => !string.IsNullOrEmpty(p));
        
        if (validParts.Length != 2) return fallback;

        var language = validParts[0];
        var region = validParts[1];

        return forceLower 
            ? $"{language.ToLowerInvariant()}_{region.ToLowerInvariant()}" 
            : $"{language.ToLowerInvariant()}_{region.ToUpperInvariant()}";
    }
    
    [GeneratedRegex(@"[^a-zA-Z]+")]
    private static partial Regex NonAlphaRegex();
}
