using System.Reflection;
using System.Xml.Linq;
using Windows.ApplicationModel.Resources;

namespace WinPlus.Services;

public static class S
{
    private static ResourceLoader? _loader;
    private static bool? _hasPackage;
    private static readonly object _lock = new();
    private static readonly Dictionary<string, Dictionary<string, string>> _embedded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] Languages = { "en", "ja", "zh-Hant-TW", "pt", "fr", "es", "ru" };

    private static bool HasPackage()
    {
        if (_hasPackage.HasValue) return _hasPackage.Value;
        try
        {
            uint len = 0;
            int rc = Interop.Win32.GetCurrentPackageFullName(ref len, nint.Zero);
            _hasPackage = rc != 15700;
        }
        catch { _hasPackage = false; }
        return _hasPackage.Value;
    }

    private static ResourceLoader? Loader
    {
        get
        {
            if (!HasPackage()) return null;
            try { _loader ??= ResourceLoader.GetForViewIndependentUse(); } catch { }
            return _loader;
        }
    }

    public static string T(string key)
    {
        try
        {
            var loader = Loader;
            if (loader is not null)
            {
                var s = loader.GetString(key);
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        catch { }
        return Embedded(key);
    }

    public static string T(string key, params object[] args)
    {
        try { return string.Format(T(key), args); }
        catch { return key; }
    }

    private static string Embedded(string key)
    {
        try
        {
            var lang = ResolveLanguage();
            var table = EmbeddedTable(lang) ?? EmbeddedTable("en");
            if (table is not null && table.TryGetValue(key, out var s) && !string.IsNullOrEmpty(s))
                return s;
        }
        catch { }
        return key;
    }

    private static string ResolveLanguage()
    {
        try
        {
            string? ov = null;
            try { ov = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride; } catch { }
            string tag = !string.IsNullOrEmpty(ov) ? ov! : System.Globalization.CultureInfo.CurrentUICulture.Name;
            return MapLanguage(tag);
        }
        catch { return "en"; }
    }

    internal static string MapLanguage(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "en";
        foreach (var l in Languages)
            if (string.Equals(l, tag, StringComparison.OrdinalIgnoreCase)) return l;
        var t = tag.ToLowerInvariant();
        if (t.StartsWith("zh-hant") || t is "zh-tw" or "zh-hk" or "zh-mo") return "zh-Hant-TW";
        if (t.StartsWith("zh")) return "zh-Hant-TW";
        if (t.StartsWith("pt")) return "pt";
        if (t.StartsWith("fr")) return "fr";
        if (t.StartsWith("es")) return "es";
        if (t.StartsWith("ru")) return "ru";
        if (t.StartsWith("ja")) return "ja";
        return "en";
    }

    private static Dictionary<string, string>? EmbeddedTable(string lang)
    {
        lock (_lock)
        {
            if (_embedded.TryGetValue(lang, out var cached)) return cached;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var want = new[]
                {
                    $".Strings.{lang}.Resources.resw",
                    $".Strings.{lang.Replace('-', '_')}.Resources.resw",
                };
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => want.Any(w => n.EndsWith(w, StringComparison.OrdinalIgnoreCase)));
                if (resName is null) return null;
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream is null) return null;
                var doc = XDocument.Load(stream);
                var table = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var data in doc.Descendants("data"))
                {
                    var name = data.Attribute("name")?.Value;
                    var value = data.Element("value")?.Value;
                    if (!string.IsNullOrEmpty(name) && value is not null)
                        table[name] = value;
                }
                _embedded[lang] = table;
                return table;
            }
            catch { return null; }
        }
    }

    public static void ApplyLanguage(string language)
    {
        try
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
                string.Equals(language, "System", StringComparison.OrdinalIgnoreCase) ? "" : language;
        }
        catch { }
    }
}
