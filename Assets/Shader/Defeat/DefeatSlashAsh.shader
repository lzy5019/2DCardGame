Shader "UI/DefeatSlashAsh"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _DefeatProgress ("Defeat Progress", Range(0, 1)) = 0
        _SlashAngle ("Slash Angle", Range(-180, 180)) = -35
        _SlashWidth ("Slash Width", Range(0.01, 0.35)) = 0.05
        _SlashSoftness ("Slash Softness", Range(0.01, 0.4)) = 0.1
        _SplitOffset ("Split Offset", Range(0, 0.2)) = 0.03

        _SlashColor ("Slash Color", Color) = (1.0, 0.18, 0.06, 1.0)
        _SlashGlow ("Slash Glow", Range(0, 4)) = 1.8
        _DefeatTint ("Defeat Tint", Color) = (0.55, 0.15, 0.12, 1.0)

        _AshColor ("Ash Color", Color) = (0.32, 0.28, 0.26, 1.0)
        _AshDarkColor ("Ash Dark Color", Color) = (0.08, 0.05, 0.05, 1.0)
        _AshEdgeColor ("Ash Edge Color", Color) = (0.95, 0.28, 0.08, 1.0)
        _AshNoiseScale ("Ash Noise Scale", Range(2, 24)) = 9
        _AshFeather ("Ash Feather", Range(0.01, 0.25)) = 0.08
        _AshSpread ("Ash Spread", Range(0.2, 1.5)) = 0.95
        _AshDrift ("Ash Drift", Range(0, 4)) = 0.75
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
            fixed4 _Color;
            float _DefeatProgress;
            float _SlashAngle;
            float _SlashWidth;
            float _SlashSoftness;
            float _SplitOffset;
            fixed4 _SlashColor;
            float _SlashGlow;
            fixed4 _DefeatTint;
            fixed4 _AshColor;
            fixed4 _AshDarkColor;
            fixed4 _AshEdgeColor;
            float _AshNoiseScale;
            float _AshFeather;
            float _AshSpread;
            float _AshDrift;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
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

            fixed4 frag(v2f i) : SV_Target
            {
                float progress = saturate(_DefeatProgress);

                float angleRad = radians(_SlashAngle);
                float2 slashDir = normalize(float2(cos(angleRad), sin(angleRad)));
                float2 slashNormal = float2(-slashDir.y, slashDir.x);

                float2 centeredUv = i.uv - 0.5;
                float slashTravel = lerp(-1.15, 1.15, progress);
                float signedDistance = dot(centeredUv, slashNormal) - slashTravel;

                float splitStrength = saturate((progress - 0.08) / 0.35);
                float splitSideMask = smoothstep(0.0, _SlashSoftness * 0.8 + 0.0001, abs(signedDistance));
                float2 splitUv = i.uv - slashNormal * sign(signedDistance) * _SplitOffset * splitStrength * splitSideMask;

                fixed4 baseCol = tex2D(_MainTex, splitUv) * i.color;
                float sourceAlpha = baseCol.a;

                float slashBand = 1.0 - smoothstep(_SlashWidth, _SlashWidth + _SlashSoftness, abs(signedDistance));
                float slashPresence = slashBand * (1.0 - saturate(progress * 0.45));

                float behindSlash = smoothstep(-_SlashSoftness * 2.0, _SlashSoftness * 2.0, slashTravel - dot(centeredUv, slashNormal));
                float ashProgress = saturate((progress - 0.12) / 0.88);

                float2 noiseUv = splitUv * _AshNoiseScale + float2(0.0, _Time.y * _AshDrift);
                float ashNoise = Fbm(noiseUv);
                float ashDrive = saturate(ashProgress * 0.6 + behindSlash * _AshSpread);
                float ashThreshold = lerp(1.1, -0.1, ashDrive);
                float ashRemaining = 1.0 - smoothstep(ashThreshold - _AshFeather, ashThreshold + _AshFeather, ashNoise);
                float ashAmount = saturate(1.0 - ashRemaining);

                float luminance = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
                float3 ashRgb = lerp(_AshDarkColor.rgb, _AshColor.rgb, luminance);

                float tintStrength = saturate(progress * 0.35 + behindSlash * 0.3);
                float3 tintedBase = lerp(baseCol.rgb, baseCol.rgb * _DefeatTint.rgb, tintStrength);
                float3 finalRgb = lerp(tintedBase, ashRgb, ashAmount * 0.92);

                float ashEdge = 1.0 - saturate(abs(ashNoise - ashThreshold) / max(_AshFeather, 0.0001));
                finalRgb += _AshEdgeColor.rgb * ashEdge * ashAmount * behindSlash * 0.85;
                finalRgb += _SlashColor.rgb * slashPresence * _SlashGlow;

                float finalAlpha = sourceAlpha * ashRemaining;
                finalAlpha = max(finalAlpha, sourceAlpha * slashPresence * 0.55);

                fixed4 result = fixed4(finalRgb, finalAlpha);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
