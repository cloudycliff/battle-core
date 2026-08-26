using System.Globalization;
using System.Text.Json;
using BattleCore.Configuration;
using BattleCore.Math;

namespace BattleCore.Serialization;

internal sealed class JsonParse
{
    public List<string> Errors { get; } = [];

    public bool HasErrors => Errors.Count > 0;

    public void Fail(string message) => Errors.Add(message);

    public void ThrowIfErrors()
    {
        if (Errors.Count > 0)
        {
            throw new ConfigurationException(Errors);
        }
    }

    public static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException([$"JSON parse error: {exception.Message}"]);
        }
    }

    public JsonElement Require(JsonElement parent, string name, string path)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            Fail($"{path} must be an object.");
            return default;
        }

        if (!parent.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            Fail($"{path}.{name} is required.");
            return default;
        }

        return value;
    }

    public bool TryGet(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(name, out value) &&
               value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
    }

    public string RequireString(JsonElement parent, string name, string path)
    {
        var value = Require(parent, name, path);
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Fail($"{path}.{name} must be a string.");
            return string.Empty;
        }

        var text = value.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            Fail($"{path}.{name} must be non-empty.");
        }

        return text;
    }

    public string? OptionalString(JsonElement parent, string name, string path)
    {
        if (!TryGet(parent, name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Fail($"{path}.{name} must be a string.");
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public int RequireInt32(JsonElement parent, string name, string path) =>
        checked((int)RequireInt64(parent, name, path));

    public long RequireInt64(JsonElement parent, string name, string path)
    {
        var value = Require(parent, name, path);
        return value.ValueKind == JsonValueKind.Undefined ? 0 : ReadInt64(value, $"{path}.{name}");
    }

    public int OptionalInt32(JsonElement parent, string name, string path, int fallback)
    {
        if (!TryGet(parent, name, out var value))
        {
            return fallback;
        }

        return checked((int)ReadInt64(value, $"{path}.{name}"));
    }

    public long OptionalInt64(JsonElement parent, string name, string path, long fallback)
    {
        if (!TryGet(parent, name, out var value))
        {
            return fallback;
        }

        return ReadInt64(value, $"{path}.{name}");
    }

    public ulong RequireUInt64(JsonElement parent, string name, string path)
    {
        var value = Require(parent, name, path);
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            Fail($"{path}.{name} must be an unsigned integer.");
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var number))
        {
            return number;
        }

        Fail($"{path}.{name} must be an unsigned integer.");
        return 0;
    }

    public bool OptionalBool(JsonElement parent, string name, string path, bool fallback)
    {
        if (!TryGet(parent, name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind is JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind is JsonValueKind.False)
        {
            return false;
        }

        Fail($"{path}.{name} must be a boolean.");
        return fallback;
    }

    public TEnum RequireEnum<TEnum>(JsonElement parent, string name, string path)
        where TEnum : struct, Enum
    {
        var text = RequireString(parent, name, path);
        if (string.IsNullOrEmpty(text))
        {
            return default;
        }

        if (Enum.TryParse<TEnum>(text, ignoreCase: false, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        Fail($"{path}.{name} has unknown value '{text}'.");
        return default;
    }

    public TEnum OptionalEnum<TEnum>(JsonElement parent, string name, string path, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!TryGet(parent, name, out _))
        {
            return fallback;
        }

        return RequireEnum<TEnum>(parent, name, path);
    }

    public Fix64 RequireFix64(JsonElement parent, string name, string path)
    {
        var value = Require(parent, name, path);
        return value.ValueKind == JsonValueKind.Undefined
            ? Fix64.Zero
            : ReadFix64(value, $"{path}.{name}");
    }

    public Fix64 OptionalFix64(JsonElement parent, string name, string path, Fix64 fallback)
    {
        if (!TryGet(parent, name, out var value))
        {
            return fallback;
        }

        return ReadFix64(value, $"{path}.{name}");
    }

    public FixVector2 RequireVector(JsonElement parent, string name, string path)
    {
        var value = Require(parent, name, path);
        return value.ValueKind == JsonValueKind.Undefined
            ? FixVector2.Zero
            : ReadVector(value, $"{path}.{name}");
    }

    public FixAabb RequireAabb(JsonElement parent, string name, string path)
    {
        var value = Require(parent, name, path);
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return new FixAabb(FixVector2.Zero, FixVector2.Zero);
        }

        return ReadAabbElement(value, $"{path}.{name}");
    }

    public FixAabb ReadAabbElement(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Fail($"{path} must be an object with min and max.");
            return new FixAabb(FixVector2.Zero, FixVector2.Zero);
        }

        var min = RequireVector(value, "min", path);
        var max = RequireVector(value, "max", path);
        try
        {
            return new FixAabb(min, max);
        }
        catch (ArgumentException exception)
        {
            Fail($"{path}: {exception.Message}");
            return new FixAabb(FixVector2.Zero, FixVector2.Zero);
        }
    }

    public IReadOnlyList<T> RequireArray<T>(
        JsonElement parent,
        string name,
        string path,
        Func<JsonElement, string, T> map)
    {
        var value = Require(parent, name, path);
        return value.ValueKind == JsonValueKind.Undefined
            ? []
            : ReadArray(value, $"{path}.{name}", map);
    }

    public IReadOnlyList<T> OptionalArray<T>(
        JsonElement parent,
        string name,
        string path,
        Func<JsonElement, string, T> map)
    {
        if (!TryGet(parent, name, out var value))
        {
            return [];
        }

        return ReadArray(value, $"{path}.{name}", map);
    }

    public Dictionary<string, long> OptionalLongMap(
        JsonElement parent,
        string name,
        string path)
    {
        if (!TryGet(parent, name, out var value))
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Fail($"{path}.{name} must be an object of integer values.");
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            map[property.Name] = ReadInt64(property.Value, $"{path}.{name}.{property.Name}");
        }

        return map;
    }

    public IReadOnlyList<string> OptionalStringArray(JsonElement parent, string name, string path) =>
        OptionalArray(parent, name, path, (element, itemPath) =>
        {
            if (element.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(element.GetString()))
            {
                Fail($"{itemPath} must be a non-empty string.");
                return string.Empty;
            }

            return element.GetString()!;
        });

    public IReadOnlyList<T> ReadArray<T>(
        JsonElement value,
        string path,
        Func<JsonElement, string, T> map)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            Fail($"{path} must be an array.");
            return [];
        }

        var items = new List<T>();
        var index = 0;
        foreach (var element in value.EnumerateArray())
        {
            items.Add(map(element, $"{path}[{index}]"));
            index++;
        }

        return items;
    }

    public long ReadInt64(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        Fail($"{path} must be an integer.");
        return 0;
    }

    public Fix64 ReadFix64(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out var integer))
            {
                return Fix64.FromInt(integer);
            }

            Fail($"{path} must be an integer, {{\"n\", \"d\"}}, or {{\"raw\"}} — JSON floating-point is not allowed.");
            return Fix64.Zero;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Fail($"{path} must be an integer, {{\"n\", \"d\"}}, or {{\"raw\"}}.");
            return Fix64.Zero;
        }

        if (TryGet(value, "raw", out var raw))
        {
            return Fix64.FromRaw(ReadInt64(raw, $"{path}.raw"));
        }

        if (TryGet(value, "n", out _) || TryGet(value, "d", out _))
        {
            var numerator = RequireInt64(value, "n", path);
            var denominator = RequireInt64(value, "d", path);
            if (denominator == 0)
            {
                Fail($"{path}.d must be non-zero.");
                return Fix64.Zero;
            }

            return Fix64.FromRatio(numerator, denominator);
        }

        Fail($"{path} must include raw or n/d.");
        return Fix64.Zero;
    }

    public FixVector2 ReadVector(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Fail($"{path} must be an object with x and y.");
            return FixVector2.Zero;
        }

        return new FixVector2(RequireFix64(value, "x", path), RequireFix64(value, "y", path));
    }
}
