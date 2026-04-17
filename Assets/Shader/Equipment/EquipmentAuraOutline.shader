Shader "UI/EquipmentAuraOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _AuraColor ("Aura Color", Color) = (1.0, 0.78, 0.25, 1.0)
        _ShowSource ("Show Source", Range(0, 1)) = 1

        _OutlineSize ("Outline Size", Range(0, 8)) = 1.2
        _OutlineSoftness ("Outline Softness", Range(0.01, 4.0)) = 1.0
        _OutlineStrength ("Outline Strength", Range(0, 3)) = 1.0

        _GlowSize ("Glow Size", Range(0, 16)) = 4.0
        _GlowSoftness ("Glow Softness", Range(0.01, 6.0)) = 2.0
        _GlowStrength ("Glow Strength", Range(0, 4)) = 1.2

        _FrameInset ("Frame Inset", Range(0.0, 0.25)) = 0.025
        _FrameWidth ("Frame Width", Range(0.001, 0.15)) = 0.025
        _FrameSoftness ("Frame Softness", Range(0.001, 0.12)) = 0.02
        _FrameStrength ("Frame Strength", Range(0, 3)) = 0.8
        _FrameNoiseScale ("Frame Noise Scale", Range(2, 30)) = 11
        _FrameNoiseStrength ("Frame Noise Strength", Range(0, 0.08)) = 0.018
        _CornerRoundness ("Corner Roundness", Range(0, 0.2)) = 0.05

        _BurnColorA ("Burn Color A", Color) = (1.0, 0.92, 0.58, 1.0)
        _BurnColorB ("Burn Color B", Color) = (1.0, 0.42, 0.08, 1.0)
        _BurnNoiseScale ("Burn Noise Scale", Range(2, 24)) = 10
        _BurnSpeed ("Burn Speed", Range(0, 6)) = 1.4
        _BurnStrength ("Burn Strength", Range(0, 3)) = 1.15
        _BurnWidth ("Burn Width", Range(0.1, 2.5)) = 1.0

        _MaskThreshold ("Mask Threshold", Range(0, 1)) = 0.05
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _AuraColor;
            float _ShowSource;
            float _OutlineSize;
            float _OutlineSoftness;
            float _OutlineStrength;
            float _GlowSize;
            float _GlowSoftness;
            float _GlowStrength;
            float _FrameInset;
            float _FrameWidth;
            float _FrameSoftness;
            float _FrameStrength;
            float _FrameNoiseScale;
            float _FrameNoiseStrength;
            float _CornerRoundness;
            fixed4 _BurnColorA;
            fixed4 _BurnColorB;
            float _BurnNoiseScale;
            float _BurnSpeed;
            float _BurnStrength;
            float _BurnWidth;
            float _MaskThreshold;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise21(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += Noise21(p) * amplitude;
                p *= 2.02;
                amplitude *= 0.5;

                value += Noise21(p) * amplitude;
                p *= 2.03;
                amplitude *= 0.5;

                value += Noise21(p) * amplitude;
                return value;
            }

            float MaxNeighborAlpha(float2 uv, float radius)
            {
                float2 texel = _MainTex_TexelSize.xy * radius;
                float maxAlpha = 0.0;

                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2( texel.x, 0.0)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(-texel.x, 0.0)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(0.0,  texel.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(0.0, -texel.y)));

                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2( texel.x,  texel.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2( texel.x, -texel.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(-texel.x,  texel.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(-texel.x, -texel.y)));

                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2( texel.x * 1.5, 0.0)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(-texel.x * 1.5, 0.0)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(0.0,  texel.y * 1.5)));
                maxAlpha = max(maxAlpha, SampleAlpha(uv + float2(0.0, -texel.y * 1.5)));

                return maxAlpha;
            }

            float RoundedRectSdf(float2 centeredUv, float2 halfSize, float radius)
            {
                radius = max(radius, 0.0001);
                float2 q = abs(centeredUv) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainCol = tex2D(_MainTex, i.uv) * i.color;
                float sourceAlpha = smoothstep(_MaskThreshold, 1.0, mainCol.a);
                float2 centeredUv = i.uv - 0.5;

                float2 frameNoiseUv = i.uv * _FrameNoiseScale + float2(_Time.y * _BurnSpeed * 0.18, _Time.y * 0.09);
                float frameNoise = (Fbm(frameNoiseUv) - 0.5) * 2.0;
                float frameNoiseOffset = frameNoise * _FrameNoiseStrength;

                float outlineNeighbor = MaxNeighborAlpha(i.uv, max(_OutlineSize, 0.001));
                float glowNeighbor = MaxNeighborAlpha(i.uv, max(_GlowSize, _OutlineSize + 0.001));

                float outlineMask = saturate(outlineNeighbor - sourceAlpha);
                outlineMask = smoothstep(0.0, max(0.0001, 1.0 / (_OutlineSoftness * 8.0 + 1.0)), outlineMask);

                float glowMask = saturate(glowNeighbor - max(sourceAlpha, outlineMask));
                glowMask = pow(glowMask, max(0.0001, 1.0 + _GlowSoftness));

                float edgeIrregularity = lerp(
                    1.0 - _FrameNoiseStrength * 9.0,
                    1.0 + _FrameNoiseStrength * 9.0,
                    saturate(frameNoise * 0.5 + 0.5)
                );
                outlineMask = saturate(outlineMask * edgeIrregularity);
                glowMask = saturate(glowMask * lerp(0.92, 1.12, saturate(frameNoise * 0.5 + 0.5)));

                float2 outerHalfSize = float2(0.5 - _FrameInset, 0.5 - _FrameInset);
                float2 innerHalfSize = max(outerHalfSize - _FrameWidth, 0.0001);
                float outerRadius = min(_CornerRoundness, min(outerHalfSize.x, outerHalfSize.y) - 0.0001);
                float innerRadius = min(max(_CornerRoundness - _FrameWidth * 0.45, 0.0001), min(innerHalfSize.x, innerHalfSize.y) - 0.0001);

                float outerSdf = RoundedRectSdf(centeredUv, outerHalfSize + frameNoiseOffset, outerRadius);
                float innerSdf = RoundedRectSdf(centeredUv, innerHalfSize + frameNoiseOffset * 0.45, innerRadius);

                float outerInside = 1.0 - smoothstep(0.0, _FrameSoftness, outerSdf);
                float innerInside = 1.0 - smoothstep(0.0, _FrameSoftness, innerSdf);
                float frameMask = saturate(outerInside - innerInside);

                float burnMask = saturate(outlineMask * 0.85 + glowMask * 0.55 + frameMask * _BurnWidth);
                float2 burnUv = i.uv * _BurnNoiseScale + float2(0.0, _Time.y * _BurnSpeed);
                float burnNoise = Fbm(burnUv);
                float flicker = 0.5 + 0.5 * sin(_Time.y * (_BurnSpeed * 4.2) + i.uv.y * 11.0 + burnNoise * 6.0);
                float burnBand = smoothstep(0.28, 0.85, burnNoise + flicker * 0.45);
                float burnAlpha = burnMask * burnBand * _BurnStrength;
                fixed3 burnRgb = lerp(_BurnColorA.rgb, _BurnColorB.rgb, saturate(burnNoise + flicker * 0.35));

                float auraAlpha = 0.0;
                auraAlpha += outlineMask * _OutlineStrength;
                auraAlpha += glowMask * _GlowStrength;
                auraAlpha += frameMask * _FrameStrength;
                auraAlpha = saturate(auraAlpha) * i.color.a;

                fixed3 auraRgb = _AuraColor.rgb * auraAlpha;
                auraRgb += burnRgb * burnAlpha * i.color.a;
                auraAlpha = saturate(auraAlpha + burnAlpha * 0.45 * i.color.a);

                fixed4 sourceCol;
                sourceCol.a = mainCol.a * _ShowSource;
                sourceCol.rgb = mainCol.rgb * sourceCol.a;

                fixed4 auraCol = fixed4(auraRgb, auraAlpha);
                return sourceCol + auraCol * (1.0 - sourceCol.a);
            }
            ENDCG
        }
    }
}
