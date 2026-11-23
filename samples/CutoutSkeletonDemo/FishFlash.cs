using Godot;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>受击时把槽位颜色混向白色。一条鱼的槽共用一份材质。</summary>
internal static class FishFlash
{
    public const float Duration = 0.1f;

    public const string Parameter = "flash";

    private const string Code = """
        shader_type canvas_item;
        uniform float flash = 0.0;
        void fragment() {
            vec4 tex = texture(TEXTURE, UV);
            COLOR = vec4(mix(tex.rgb, vec3(1.0), clamp(flash, 0.0, 1.0)), tex.a);
        }
        """;

    private static Shader? _shader;

    public static ShaderMaterial CreateMaterial()
    {
        _shader ??= new Shader { Code = Code };
        return new ShaderMaterial { Shader = _shader };
    }
}
