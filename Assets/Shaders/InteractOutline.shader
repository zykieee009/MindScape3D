// Interact glow, part 1 of 2 (InteractOutline.cs): marks the object's visible pixels in the stencil buffer, then the
// crisp edge and the bright rim right outside the silhouette. "Mindscape/Interact Outline Halo" (one queue later)
// continues outward with the softer bands. See InteractOutline.hlsl.
Shader "Mindscape/Interact Outline"
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        HLSLINCLUDE
        #include "InteractOutline.hlsl"
        ENDHLSL

        // The object itself: nothing is drawn over it.
        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual ColorMask 0
            Stencil { Ref 128 WriteMask 128 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes input) { return Grow(input, 0); }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Edge"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 64 ReadMask 192 WriteMask 64 Comp Greater Pass Replace }   // once per pixel, never on the object
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes input) { return Grow(input, max(1.5, _Width * 0.12)); }
            half4 frag(Varyings input) : SV_Target { return Band(0.9); }
            ENDHLSL
        }

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 64 ReadMask 192 WriteMask 64 Comp Greater Pass Replace }   // once per pixel, never on the object
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes input) { return Grow(input, _Width * 0.26); }
            half4 frag(Varyings input) : SV_Target { return Band(0.55); }
            ENDHLSL
        }
    }
}
