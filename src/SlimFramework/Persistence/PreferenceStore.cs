using System.Globalization;
using System.Text.Json;

namespace SlimFramework.Persistence;

/// <summary>与进度槽分离的键值偏好。脏数据在 Flush 或 Shutdown 时写入 <see cref="ITextStore"/>。</summary>
public sealed class PreferenceStore : IModule
{
    private readonly ITextStore _store;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private bool _dirty;

    public PreferenceStore(ITextStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public void Init(SlimApp app) => Load();

    public void Shutdown()
    {
        if (_dirty)
            Flush();
    }

    public void Load()
    {
        _values.Clear();
        _dirty = false;
        if (!_store.TryRead(out var text) || string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
            if (parsed == null)
                return;
            foreach (var pair in parsed)
                _values[pair.Key] = pair.Value;
        }
        catch (JsonException)
        {
            _values.Clear();
        }
    }

    public void Flush()
    {
        if (!_dirty)
            return;
        _store.Write(JsonSerializer.Serialize(_values));
        _dirty = false;
    }

    public string Get(string key, string fallback = "") =>
        _values.TryGetValue(key, out var value) ? value : fallback;

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value ?? "";
        _dirty = true;
    }

    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(Get(key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public void SetInt(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public bool GetBool(string key, bool fallback = false) =>
        bool.TryParse(Get(key, ""), out var value) ? value : fallback;

    public void SetBool(string key, bool value) => Set(key, value ? "true" : "false");

    public float GetFloat(string key, float fallback = 0f) =>
        float.TryParse(Get(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public void SetFloat(string key, float value) => Set(key, value.ToString(CultureInfo.InvariantCulture));
}
