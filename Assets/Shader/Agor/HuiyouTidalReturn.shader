Shader "UI/HuiyouTidalReturn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _ReturnProgress ("Return Progress", Range(0, 1)) = 0

        _WaveColorA ("Wave Color A", Color) = (0.10, 0.55, 0.92, 1.0)
        _WaveColorB ("Wave Color B", Color) = (0.05, 0.82, 0.95, 1.0)
        _FoamColor ("Foam Color", Color) = (0.84, 0.97, 1.0, 1.0)
        _AbyssTint ("Abyss Tint", Color) = (0.02, 0.12, 0.28, 1.0)

        _WaveAngle ("Wave Angle", Range(-180, 180)) = -18
        _WaveWidth ("Wave Width", Range(0.03, 0.55)) = 0.18
        _WaveSoftness ("Wave Softness", Range(0.01, 0.4)) = 0.11
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.12)) = 0.035
        _WaveFrequency ("Wave Frequency", Range(2, 22)) = 10.5
        _WaveSpeed ("Wave Speed", Range(0, 8)) = 2.2

        _FoamWidth ("Foam Width", Range(0.003, 0.12)) = 0.028
        _FoamSoftness ("Foam Softness", Range(0.003, 0.15)) = 0.03
        _FoamGlow ("Foam Glow", Range(0, 4)) = 1.35

        _TintStrength ("Tint Strength", Range(0, 1)) = 0.56
        _AbyssStrength ("Abyss Strength", Range(0, 1)) = 0.32
        _DistortionStrength ("Distortion Strength", Range(0, 0.08)) = 0.018

        _NoiseScale ("Noise Scale", Range(2, 32)) = 8.5
        _NoiseDrift ("Noise Drift", Range(0, 4)) = 0.85
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
            float _ReturnProgress;
            fixed4 _WaveColorA;
            fixed4 _WaveColorB;
            fixed4 _FoamColor;
            fixed4 _AbyssTint;
            float _WaveAngle;
            float _WaveWidth;
            float _WaveSoftness;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _FoamWidth;
            float _FoamSoftness;
            float _FoamGlow;
            float _TintStrength;
            float _AbyssStrength;
            float _DistortionStrength;
            float _NoiseScale;
            float _NoiseDrift;

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
                float progress = saturate(_ReturnProgress);
                float life = sin(progress * UNITY_PI);

                float2 centeredUv = i.uv - 0.5;
                float angleRad = radians(_WaveAngle);
                float2 waveDir = normalize(float2(cos(angleRad), sin(angleRad)));
                float2 waveNormal = float2(-waveDir.y, waveDir.x);

                float along = dot(centeredUv, waveDir);
                float across = dot(centeredUv, waveNormal);

                float noise = Fbm(i.uv * _NoiseScale + float2(_Time.y * _NoiseDrift, -_Time.y * _NoiseDrift * 0.45));
                float waveCenter = lerp(-0.78, 0.78, progress);
                float waveOffset = sin(across * _WaveFrequency + _Time.y * _WaveSpeed + noise * 4.6) * _WaveAmplitude;
                float waveSignedDistance = along - (waveCenter + waveOffset);

                float bandMask = 1.0 - smoothstep(_WaveWidth, _WaveWidth + _WaveSoftness, abs(waveSignedDistance));
                bandMask *= life;

                float trailingDistance = waveCenter - along;
                float trailMask = smoothstep(-_WaveSoftness * 1.2, _WaveWidth * 1.65, trailingDistance);
                trailMask *= 1.0 - smoothstep(_WaveWidth * 1.1, _WaveWidth * 2.45, trailingDistance);
                trailMask *= life;

                float foamMask = 1.0 - smoothstep(_FoamWidth, _FoamWidth + _FoamSoftness, abs(waveSignedDistance));
                foamMask *= life;

                float2 distortion = waveNormal * (bandMask * 0.55 + trailMask * 0.35) * _DistortionStrength;
                distortion += waveDir * (noise - 0.5) * _DistortionStrength * 0.22 * life;

                fixed4 baseCol = tex2D(_MainTex, i.uv - distortion) * i.color;
                float sourceAlpha = baseCol.a;

                float edgePulse = sin(_Time.y * 8.2 + across * 9.0) * 0.5 + 0.5;
                float3 waveRgb = lerp(_WaveColorA.rgb, _WaveColorB.rgb, saturate(0.35 + noise * 0.85 + edgePulse * 0.2));
                float3 abyssRgb = lerp(baseCol.rgb, baseCol.rgb * _AbyssTint.rgb, trailMask * _AbyssStrength);
                float3 tintedBase = lerp(abyssRgb, waveRgb, (bandMask * 0.58 + trailMask * 0.34) * _TintStrength);

                float3 resultRgb = lerp(baseCol.rgb, tintedBase, saturate(bandMask + trailMask));
                resultRgb += _FoamColor.rgb * foamMask * _FoamGlow * (0.45 + edgePulse * 0.3);

                fixed4 result = fixed4(resultRgb, sourceAlpha);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
