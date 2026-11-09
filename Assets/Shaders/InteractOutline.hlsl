// Shared by the two Interact Outline shaders. The hull (InteractOutline.cs) is the object's own mesh pushed outward
// in screen space, a fixed number of pixels, away from the object's centre (_CenterWS, set every frame). Growing away
// from the centre gives a clean, enlarged silhouette even for flat props seen face-on, where surface normals point at
// the camera and give no usable direction. The bands go from a crisp edge out to a faint halo; the stencil buffer lets
// each pixel be drawn once (by the innermost band that reaches it) and never over the object, and they are
// alpha-blended so the glow stays blue on bright surfaces.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float4 _CenterWS;
    float _Width, _Strength, _Intensity, _Pulse;
CBUFFER_END

struct Attributes { float4 positionOS : POSITION; };
struct Varyings { float4 positionCS : SV_POSITION; };

Varyings Grow(Attributes input, float pixels)
{
    Varyings output;
    float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
    float4 centerCS = TransformWorldToHClip(_CenterWS.xyz);
    float2 halfScreen = _ScreenParams.xy * 0.5;
    float2 away = positionCS.xy / positionCS.w * halfScreen - centerCS.xy / max(centerCS.w, 1e-4) * halfScreen;
    float2 dir = dot(away, away) > 1e-6 ? normalize(away) : float2(0, 0);
    // Focused things (the one [E] acts on) reach a little further.
    pixels *= lerp(1.0, 1.25, saturate(_Intensity - 1.0));
    positionCS.xy += dir * pixels / halfScreen * positionCS.w;
    output.positionCS = positionCS;
    return output;
}

half4 Band(float alpha)
{
    half pulse = 1.0 - _Pulse * 0.5 * (1.0 + sin(_Time.y * 3.2));
    return half4(_Color.rgb, saturate(alpha * _Strength * _Intensity * pulse));
}
