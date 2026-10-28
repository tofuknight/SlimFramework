namespace SlimFramework.Audio;

public enum AudioBus
{
    Music,
    Sfx,
    Ui,
}

/// <summary>音频端口。路径由实现解释；Godot 实现要求 res://。</summary>
public interface IAudio : IModule
{
    void SetVolume(AudioBus bus, float linear01);

    float GetVolume(AudioBus bus);

    void PlayMusic(string path);

    void StopMusic();

    /// <summary>在 <see cref="AudioBus.Sfx"/> 上播放一次。</summary>
    void PlaySfx(string path);

    /// <summary>在 <see cref="AudioBus.Ui"/> 上播放一次。UI 点击不走 <see cref="PlaySfx"/>。</summary>
    void PlayUi(string path);
}
