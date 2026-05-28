using System.Formats.Asn1;
using System.Text.RegularExpressions;

namespace EasyOpenVR.Data;

public partial class SharedUtils
{

    public static string FixLanguageTag(string tag)
    {
        return LanguageTagRegex().Replace(tag, "_");
    }

    [GeneratedRegex("[^a-z]")]
    private static partial Regex LanguageTagRegex();
}
