// The Chapter 3 mirror: shows the reflection camera's picture, mirrored left to right, and can cross-fade to a
// second reflection (the same view with younger Joey standing beside you). _Blend 0 = first picture, 1 = second.
// Lives in Resources so builds include it (SocialMirror loads it with Resources.Load).
Shader "Mindscape/MirrorBlend"
{
    Properties
    {
        _MainTex ("Reflection", 2D) = "black" {}
        _SecondTex ("Reflection with younger self", 2D) = "black" {}
        _Blend ("Blend", Range(0, 1)) = 0
        _Glow ("Glow", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SecondTex); SAMPLER(sampler_SecondTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Blend;
                float _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = float2(1 - input.uv.x, input.uv.y);   // a mirror: left and right swap
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 b = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, i.uv);
                return half4(lerp(a.rgb, b.rgb, saturate(_Blend)) * _Glow, 1);
            }
            ENDHLSL
        }
    }
}
