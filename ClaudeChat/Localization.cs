using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ClaudeChat;

/// <summary>
/// UI strings from lang\&lt;code&gt;.json next to the exe. Adding a language = adding a file;
/// no rebuild. Missing keys fall back to English (embedded), then to the key itself.
/// The "_meta" object holds the language's own name, culture and direction.
/// </summary>
public sealed class Localization
{
    public sealed record LanguageInfo(string Code, string Name, string File);

    private Dictionary<string, string> _strings = new();
    private Dictionary<string, string> _fallback = new();

    public string Code { get; private set; } = "en";
    public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;
    public bool RightToLeft { get; private set; }

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "lang");

    public Localization()
    {
        using var stream = typeof(Localization).Assembly.GetManifestResourceStream("ClaudeChat.lang.en.json");
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            _fallback = Parse(reader.ReadToEnd(), out _);
        }
    }

    public static IReadOnlyList<LanguageInfo> Available()
    {
        var list = new List<LanguageInfo>();
        if (!Directory.Exists(Folder)) return list;
        foreach (var file in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                Parse(File.ReadAllText(file), out var meta);
                string code = Path.GetFileNameWithoutExtension(file);
                list.Add(new LanguageInfo(code, meta.TryGetValue("name", out var n) ? n : code, file));
            }
            catch { /* a broken language file is skipped, not fatal */ }
        }
        return list.OrderBy(l => l.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>"auto" picks the Windows UI language if a file exists for it, else English.</summary>
    public void Load(string requested)
    {
        var available = Available();
        string code = requested;
        if (string.IsNullOrWhiteSpace(code) || code.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var ui = CultureInfo.CurrentUICulture;
            code = available.Any(l => l.Code.Equals(ui.Name, StringComparison.OrdinalIgnoreCase)) ? ui.Name
                 : available.Any(l => l.Code.Equals(ui.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)) ? ui.TwoLetterISOLanguageName
                 : "en";
        }

        var info = available.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        Dictionary<string, string> meta = new();
        _strings = info != null ? SafeParse(File.ReadAllText(info.File), out meta) : new Dictionary<string, string>();
        Code = info?.Code ?? "en";
        RightToLeft = meta.TryGetValue("rtl", out var rtl) && rtl.Equals("true", StringComparison.OrdinalIgnoreCase);
        try { Culture = CultureInfo.GetCultureInfo(meta.TryGetValue("culture", out var c) ? c : Code); }
        catch { Culture = CultureInfo.InvariantCulture; }
    }

    public string this[string key] => Get(key);

    public string Get(string key) =>
        _strings.TryGetValue(key, out var v) ? v : _fallback.TryGetValue(key, out var f) ? f : key;

    public string Format(string key, params object?[] args)
    {
        try { return string.Format(Culture, Get(key), args); }
        catch (FormatException) { return Get(key); }
    }

    private static Dictionary<string, string> SafeParse(string json, out Dictionary<string, string> meta)
    {
        try { return Parse(json, out meta); }
        catch { meta = new(); return new(); }
    }

    private static Dictionary<string, string> Parse(string json, out Dictionary<string, string> meta)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (JsonNode.ParseLenient(json) is not JsonObject root) return result;
        foreach (var (name, value) in root)
        {
            if (name == "_meta" && value is JsonObject metaObject)
            {
                foreach (var (key, item) in metaObject)
                    if (item != null) meta[key] = item.ToString();
            }
            else if (value is JsonValue text && text.GetValueKind() == JsonValueKind.String)
                result[name] = text.GetValue<string>();
        }
        return result;
    }
}
