// Interact glow, part 2 of 2: the soft bands further out, drawn after "Mindscape/Interact Outline" has marked the
// object and its edge. See InteractOutline.hlsl.
Shader "Mindscape/Interact Outline Halo"
{
    Properties
    {
        _Color ("Color", Color) = (0.25, 0.62, 1, 1)
        _CenterWS ("Centre (set by script)", Vector) = (0, 0, 0, 0)
        _Width ("Glow width (pixels)", Float) = 14
        _Strength ("Strength", Float) = 1
        _Intensity ("Intensity (focus)", Float) = 1
        _Pulse ("Pulse amount", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+11" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        HLSLINCLUDE
        #include "InteractOutline.hlsl"
        ENDHLSL

        Pass
        {
            Name "Inner glow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 64 ReadMask 192 WriteMask 64 Comp Greater Pass Replace }   // once per pixel, never on the object
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes input) { return Grow(input, _Width * 0.45); }
            half4 frag(Varyings input) : SV_Target { return Band(0.34); }
            ENDHLSL
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 64 ReadMask 192 WriteMask 64 Comp Greater Pass Replace }   // once per pixel, never on the object
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes input) { return Grow(input, _Width * 0.7); }
            half4 frag(Varyings input) : SV_Target { return Band(0.2); }
            ENDHLSL
        }

        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 64 ReadMask 192 WriteMask 64 Comp Greater Pass Replace }   // once per pixel, never on the object
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes input) { return Grow(input, _Width); }
            half4 frag(Varyings input) : SV_Target { return Band(0.1); }
            ENDHLSL
        }
    }
}
