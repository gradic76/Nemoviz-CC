using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace ClaudeChat;

public enum JsonValueKind { Null, String, Number, True, False, Object, Array }

public sealed class JsonException : Exception
{
    public JsonException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// A small JSON tree over the Framework's JavaScriptSerializer: nothing to ship with the program.
/// JSON null is a C# null, as in System.Text.Json.Nodes.
/// </summary>
public abstract class JsonNode
{
    public virtual JsonNode? this[string key]
    {
        get => null;
        set => throw new InvalidOperationException("not an object");
    }

    public abstract JsonValueKind GetValueKind();
    public abstract JsonNode DeepClone();
    public virtual T GetValue<T>() => throw new InvalidOperationException("not a value");
    public JsonObject AsObject() => (JsonObject)this;

    public string ToJsonString(bool indented = false)
    {
        string compact = Json.NewSerializer().Serialize(Json.ToPlain(this));
        return indented ? Json.Indent(compact) : compact;
    }

    public override string ToString() => ToJsonString();

    public static JsonNode? Parse(string text)
    {
        try { return Json.FromPlain(Json.NewSerializer().DeserializeObject(text)); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new JsonException(e.Message, e);
        }
    }

    /// <summary>For files people edit: // and /* */ comments and trailing commas are allowed.</summary>
    public static JsonNode? ParseLenient(string text) => Parse(Json.StripComments(text));

    public static implicit operator JsonNode?(string? v) => v == null ? null : new JsonValue(v);
    public static implicit operator JsonNode(bool v) => new JsonValue(v);
    public static implicit operator JsonNode(int v) => new JsonValue(v);
    public static implicit operator JsonNode(long v) => new JsonValue(v);
    public static implicit operator JsonNode(double v) => new JsonValue(v);
}

public sealed class JsonValue : JsonNode
{
    private readonly object _value; // string, bool, int, long, decimal or double

    internal JsonValue(object value) { _value = value; }

    public override JsonValueKind GetValueKind() => _value switch
    {
        string => JsonValueKind.String,
        bool b => b ? JsonValueKind.True : JsonValueKind.False,
        _ => JsonValueKind.Number,
    };

    public override JsonNode DeepClone() => new JsonValue(_value);
    internal object Raw => _value;

    public override T GetValue<T>() =>
        TryGetValue<T>(out T value) ? value : throw new InvalidOperationException($"not a {typeof(T).Name}");

    /// <summary>A number only converts to an integer type when it has no fraction.</summary>
    public bool TryGetValue<T>(out T value)
    {
        value = default!;
        Type t = typeof(T);
        if (_value is string or bool)
        {
            if (_value.GetType() != t) return false;
            value = (T)_value;
            return true;
        }
        if (t != typeof(int) && t != typeof(long) && t != typeof(double) && t != typeof(decimal)) return false;
        try
        {
            if ((t == typeof(int) || t == typeof(long)) && Convert.ToDouble(_value, CultureInfo.InvariantCulture) % 1 != 0) return false;
            value = (T)Convert.ChangeType(_value, t, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e) when (e is OverflowException or InvalidCastException or FormatException) { return false; }
    }

    public override string ToString() => _value as string ?? ToJsonString();
}

public sealed class JsonArray : JsonNode, IEnumerable<JsonNode?>
{
    private readonly List<JsonNode?> _items = new();

    public int Count => _items.Count;
    public JsonNode? this[int index] => _items[index];
    public void Add(JsonNode? item) => _items.Add(item);
    public override JsonValueKind GetValueKind() => JsonValueKind.Array;

    public override JsonNode DeepClone()
    {
        var copy = new JsonArray();
        foreach (var item in _items) copy.Add(item?.DeepClone());
        return copy;
    }

    public IEnumerator<JsonNode?> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

public sealed class JsonObject : JsonNode, IEnumerable<KeyValuePair<string, JsonNode?>>
{
    private readonly Dictionary<string, JsonNode?> _items = new(StringComparer.Ordinal);

    public int Count => _items.Count;

    public override JsonNode? this[string key]
    {
        get => _items.TryGetValue(key, out var v) ? v : null;
        set => _items[key] = value;
    }

    public bool Remove(string key) => _items.Remove(key);
    public override JsonValueKind GetValueKind() => JsonValueKind.Object;

    public override JsonNode DeepClone()
    {
        var copy = new JsonObject();
        foreach (var pair in _items) copy[pair.Key] = pair.Value?.DeepClone();
        return copy;
    }

    public IEnumerator<KeyValuePair<string, JsonNode?>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

/// <summary>Conversions between the tree, the serializer's plain objects, classes and text.</summary>
public static class Json
{
    internal static JavaScriptSerializer NewSerializer() =>
        new() { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };

    /// <summary>A class as indented JSON with camelCase names (settings, state, drafts).</summary>
    public static string Write(object? value) => ToNode(value)?.ToJsonString(indented: true) ?? "null";

    public static T? Read<T>(string text) where T : class => To<T>(JsonNode.ParseLenient(text));

    public static T? To<T>(JsonNode? node) where T : class => (T?)FromNode(node, typeof(T));

    public static JsonNode? ToNode(object? value)
    {
        switch (value)
        {
            case null: return null;
            case JsonNode n: return n;
            case string or bool or int or long or double or decimal: return new JsonValue(value);
            case DateTime d: return new JsonValue(d.ToString("O", CultureInfo.InvariantCulture));
            case DateTimeOffset d: return new JsonValue(d.ToString("O", CultureInfo.InvariantCulture));
            case IDictionary map:
            {
                var o = new JsonObject();
                foreach (DictionaryEntry e in map) o[(string)e.Key] = ToNode(e.Value);
                return o;
            }
            case IEnumerable list:
            {
                var a = new JsonArray();
                foreach (var item in list) a.Add(ToNode(item));
                return a;
            }
        }
        var obj = new JsonObject();
        foreach (var p in Properties(value.GetType())) obj[Camel(p.Name)] = ToNode(p.GetValue(value, null));
        return obj;
    }

    private static object? FromNode(JsonNode? node, Type type)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        if (node == null) return null;
        if (target == typeof(JsonNode) || target.IsInstanceOfType(node)) return node;

        if (node is JsonValue v)
        {
            object raw = v.Raw;
            if (target == typeof(string)) return raw as string ?? throw new JsonException($"expected text, found {raw}");
            if (target == typeof(DateTime) && raw is string s1)
                return DateTime.Parse(s1, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (target == typeof(DateTimeOffset) && raw is string s2)
                return DateTimeOffset.Parse(s2, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (raw is string || raw is bool != (target == typeof(bool))) throw new JsonException($"{raw} is not a {target.Name}");
            return Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }

        if (node is JsonArray array)
        {
            if (!target.IsGenericType || target.GetGenericTypeDefinition() != typeof(List<>))
                throw new JsonException($"a list is not a {target.Name}");
            Type item = target.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(target)!;
            foreach (var n in array) list.Add(FromNode(n, item));
            return list;
        }

        var source = (JsonObject)node;
        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            Type item = target.GetGenericArguments()[1];
            var map = (IDictionary)Activator.CreateInstance(target)!;
            foreach (var pair in source) map[pair.Key] = FromNode(pair.Value, item);
            return map;
        }

        object result = Activator.CreateInstance(target)!;
        var properties = Properties(target).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (!properties.TryGetValue(pair.Key, out var p)) continue;
            object? value = FromNode(pair.Value, p.PropertyType);
            if (value != null || !p.PropertyType.IsValueType || Nullable.GetUnderlyingType(p.PropertyType) != null)
                p.SetValue(result, value, null);
        }
        return result;
    }

    private static IEnumerable<PropertyInfo> Properties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.CanRead && p.GetSetMethod() != null && p.GetGetMethod() != null && p.GetIndexParameters().Length == 0);

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    internal static object? ToPlain(JsonNode? node)
    {
        switch (node)
        {
            case null: return null;
            case JsonValue v: return v.Raw;
            case JsonArray a: return a.Select(ToPlain).ToArray();
        }
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in (JsonObject)node) map[pair.Key] = ToPlain(pair.Value);
        return map;
    }

    internal static JsonNode? FromPlain(object? value)
    {
        switch (value)
        {
            case null: return null;
            case string or bool or int or long or double or decimal: return new JsonValue(value);
            case DateTime d: return new JsonValue(d.ToString("O", CultureInfo.InvariantCulture)); // the serializer's \/Date(n)\/ form
            case IDictionary<string, object> map:
            {
                var o = new JsonObject();
                foreach (var pair in map) o[pair.Key] = FromPlain(pair.Value);
                return o;
            }
            case IEnumerable list:
            {
                var a = new JsonArray();
                foreach (var item in list) a.Add(FromPlain(item));
                return a;
            }
            default: throw new JsonException("unexpected value: " + value.GetType().Name);
        }
    }

    /// <summary>Removes comments and trailing commas outside strings.</summary>
    internal static string StripComments(string text)
    {
        var o = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                int end = i + 1;
                while (end < text.Length && text[end] != '"') end += text[end] == '\\' ? 2 : 1;
                end = Math.Min(end, text.Length - 1);
                o.Append(text, i, end - i + 1);
                i = end;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i + 1 < text.Length && text[i + 1] != '\n') i++;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? text.Length : end + 1;
            }
            else
            {
                if (c == '}' || c == ']')
                {
                    int last = o.Length - 1;
                    while (last >= 0 && char.IsWhiteSpace(o[last])) last--;
                    if (last >= 0 && o[last] == ',') o.Remove(last, 1);
                }
                o.Append(c);
            }
        }
        return o.ToString();
    }

    /// <summary>Indents compact JSON and puts back the characters the serializer escapes for
    /// web pages (' &amp; &lt; &gt;), so files stay readable.</summary>
    internal static string Indent(string compact)
    {
        var o = new StringBuilder(compact.Length * 2);
        int depth = 0;
        void NewLine() { o.Append('\n'); o.Append(' ', depth * 2); }

        for (int i = 0; i < compact.Length; i++)
        {
            char c = compact[i];
            switch (c)
            {
                case '"':
                    o.Append(c);
                    for (i++; i < compact.Length && compact[i] != '"'; i++)
                    {
                        if (compact[i] != '\\') { o.Append(compact[i]); continue; }
                        if (i + 5 < compact.Length && compact[i + 1] == 'u' && Readable(compact.Substring(i + 2, 4)) is { } plain)
                        {
                            o.Append(plain);
                            i += 5;
                        }
                        else
                        {
                            o.Append(compact[i]).Append(compact[i + 1]);
                            i++;
                        }
                    }
                    o.Append('"');
                    break;
                case '{':
                case '[':
                    o.Append(c);
                    if (i + 1 < compact.Length && (compact[i + 1] == '}' || compact[i + 1] == ']')) o.Append(compact[++i]);
                    else { depth++; NewLine(); }
                    break;
                case '}':
                case ']':
                    depth--;
                    NewLine();
                    o.Append(c);
                    break;
                case ',':
                    o.Append(c);
                    NewLine();
                    break;
                case ':':
                    o.Append(": ");
                    break;
                default:
                    o.Append(c);
                    break;
            }
        }
        return o.ToString();
    }

    private static char? Readable(string hex) => hex.ToLowerInvariant() switch
    {
        "0026" => '&',
        "0027" => '\'',
        "003c" => '<',
        "003e" => '>',
        _ => null,
    };
}
