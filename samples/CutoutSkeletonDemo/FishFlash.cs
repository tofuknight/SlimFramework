using Godot;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 受击时把槽位颜色混向白色。一条鱼的槽共用一份材质。
/// Godot 4 的 canvas_item 里，fragment 的 COLOR 已经是贴图乘过顶点色。再采样 TEXTURE 会把颜色乘第二次。
/// </summary>
internal static class FishFlash
{
    public const float Hold = 0.06f;

    public const float Fade = 0.08f;

    public const float Duration = Hold + Fade;

    public const string Parameter = "flash";

    internal const string Source = """
        shader_type canvas_item;
        uniform float flash = 0.0;
        void fragment() {
            float amount = clamp(flash, 0.0, 1.0);
            COLOR.rgb = mix(COLOR.rgb, vec3(1.0), amount);
        }
        """;

    private static Shader? _shader;

    public static ShaderMaterial CreateMaterial()
    {
        _shader ??= new Shader { Code = Source };
        return new ShaderMaterial { Shader = _shader };
    }
}
