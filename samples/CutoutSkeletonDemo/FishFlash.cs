using Godot;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>受击时把槽位颜色混向白色。一条鱼的槽共用一份材质。</summary>
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
            vec4 tint = COLOR;
            vec4 tex = texture(TEXTURE, UV);
            float amount = clamp(flash, 0.0, 1.0);
            vec3 rgb = mix(tex.rgb, vec3(1.0), amount);
            COLOR = vec4(rgb * tint.rgb, tex.a * tint.a);
        }
        """;

    private static Shader? _shader;

    public static ShaderMaterial CreateMaterial()
    {
        _shader ??= new Shader { Code = Source };
        return new ShaderMaterial { Shader = _shader };
    }
}
