using System.Globalization;
using SlimFramework.Persistence;

namespace SlimFramework.Audio;

/// <summary>
/// 把 <see cref="PreferenceStore"/> 里的线性音量套到 <see cref="IAudio"/>。
/// 缺键或解析失败时保持音频模块的当前值。本类不写回偏好。
/// </summary>
public static class AudioPreferences
{
    public const string MusicKey = "audio.music";

    public const string SfxKey = "audio.sfx";

    public const string UiKey = "audio.ui";

    public static void Apply(SlimApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.TryGet<IAudio>(out var audio) || audio == null)
            return;
        if (!app.TryGet<PreferenceStore>(out var prefs) || prefs == null)
            return;

        ApplyBus(prefs, audio, MusicKey, AudioBus.Music);
        ApplyBus(prefs, audio, SfxKey, AudioBus.Sfx);
        ApplyBus(prefs, audio, UiKey, AudioBus.Ui);
    }

    private static void ApplyBus(PreferenceStore prefs, IAudio audio, string key, AudioBus bus)
    {
        if (!prefs.TryGet(key, out var text))
            return;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var linear))
            return;
        audio.SetVolume(bus, linear);
    }
}
