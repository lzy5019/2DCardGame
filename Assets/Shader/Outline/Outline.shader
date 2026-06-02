Shader "Unlit/NewUnlitShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.25, 1.0, 0.85, 1.0)
        _OutlineSize ("Outline Size", Range(0, 8)) = 1.5
        _FrameSoftness ("Frame Softness", Range(0.1, 4)) = 1.0
        _UseSourceAlphaMask ("Use Source Alpha Mask", Range(0, 1)) = 0
        _MaskThreshold ("Mask Threshold", Range(0, 1)) = 0.05
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseMinAlpha ("Pulse Min Alpha", Range(0, 1)) = 0.35
        _PulseMaxAlpha ("Pulse Max Alpha", Range(0, 1)) = 0.9
        _PulseContrast ("Pulse Contrast", Range(0.5, 3.0)) = 1.8
        _PulseBrightness ("Pulse Brightness", Range(1.0, 4.0)) = 1.75
        _FlowSpeed ("Flow Speed", Range(0, 10)) = 1.2
        _FlowWidth ("Flow Width", Range(0.02, 0.5)) = 0.18
        _FlowStrength ("Flow Strength", Range(0, 2)) = 0.45
        _FlowBrightness ("Flow Brightness", Range(1.0, 4.0)) = 1.5
        _FlowAngle ("Flow Angle", Range(-180, 180)) = 35
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineSize;
            float _FrameSoftness;
            float _UseSourceAlphaMask;
            float _MaskThreshold;
            float _PulseSpeed;
            float _PulseMinAlpha;
            float _PulseMaxAlpha;
            float _PulseContrast;
            float _PulseBrightness;
            float _FlowSpeed;
            float _FlowWidth;
            float _FlowStrength;
            float _FlowBrightness;
            float _FlowAngle;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.worldPosition = v.vertex;
                return o;
            }

            fixed SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainCol = tex2D(_MainTex, i.uv) * i.color;

                float2 texel = _MainTex_TexelSize.xy * _OutlineSize;

                float neighborAlpha = 0.0;
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2( texel.x, 0.0)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2(-texel.x, 0.0)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2(0.0,  texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2(0.0, -texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2( texel.x,  texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2( texel.x, -texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2(-texel.x,  texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(i.uv + float2(-texel.x, -texel.y)));

                float alphaOutlineMask = saturate(neighborAlpha - mainCol.a);
                float frameWidth = max(max(_MainTex_TexelSize.x, _MainTex_TexelSize.y) * _OutlineSize, 0.0001);
                float frameEdgeDistance = min(
                    min(i.uv.x, 1.0 - i.uv.x),
                    min(i.uv.y, 1.0 - i.uv.y)
                );
                float frameMask = 1.0 - smoothstep(
                    frameWidth,
                    frameWidth * (1.0 + _FrameSoftness),
                    frameEdgeDistance
                );
                frameMask *= saturate(max(mainCol.a, neighborAlpha));

                float outlineMask = max(alphaOutlineMask, frameMask);
                float pulseWave = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                float pulseT = saturate(0.5 + (pulseWave - 0.5) * _PulseContrast);
                float pulse = lerp(
                    _PulseMinAlpha,
                    _PulseMaxAlpha,
                    pulseT
                );
                float pulseBrightness = lerp(1.0, _PulseBrightness, pulseT);

                float maskedAlpha = smoothstep(_MaskThreshold, 1.0, mainCol.a);
                float angleRad = radians(_FlowAngle);
                float2 flowDir = normalize(float2(cos(angleRad), sin(angleRad)));
                float sweepCoord = frac(dot(i.uv, flowDir) - _Time.y * _FlowSpeed);
                float sweepDistance = min(abs(sweepCoord - 0.5), 1.0 - abs(sweepCoord - 0.5));
                float flowBand = 1.0 - smoothstep(_FlowWidth * 0.5, _FlowWidth, sweepDistance);
                float flowBoost = 1.0 + flowBand * _FlowStrength;
                float flowBrightness = lerp(1.0, _FlowBrightness, flowBand);

                fixed4 outlineCol = _OutlineColor;
                outlineCol.a *= outlineMask * pulse * i.color.a;
                outlineCol.rgb *= pulseBrightness * flowBrightness;

                mainCol.rgb *= mainCol.a;
                outlineCol.rgb *= outlineCol.a;

                fixed4 result = mainCol + outlineCol * (1.0 - mainCol.a);

                float sourceModeAlpha = saturate(mainCol.a * pulse * lerp(1.0, 1.0 + _FlowStrength * 0.35, flowBand));
                fixed3 sourceModeRgb = _OutlineColor.rgb * i.color.rgb * pulseBrightness * flowBrightness * flowBoost;
                fixed4 sourceModeCol = fixed4(sourceModeRgb, sourceModeAlpha);
                sourceModeCol.a *= maskedAlpha;
                sourceModeCol.rgb *= sourceModeCol.a;

                fixed4 finalColor = lerp(result, sourceModeCol, saturate(_UseSourceAlphaMask));

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                finalColor.rgb *= finalColor.a;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
