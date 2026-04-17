Shader "UI/Purchase2"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _PurchaseProgress ("Purchase Progress", Range(0, 1)) = 0
        _ChargeColorA ("Charge Color A", Color) = (0.35, 0.95, 1.0, 1.0)
        _ChargeColorB ("Charge Color B", Color) = (1.0, 0.88, 0.42, 1.0)
        _EdgeColor ("Edge Color", Color) = (1.0, 0.95, 0.68, 1.0)

        _FlowAngle ("Flow Angle", Range(-180, 180)) = -35
        _EdgeBoost ("Edge Boost", Range(0, 3)) = 0.7

        _NoiseScale ("Noise Scale", Range(2, 24)) = 9
        _DissolveFeather ("Dissolve Feather", Range(0.01, 0.3)) = 0.08
        _DissolveBias ("Dissolve Bias", Range(0.2, 1.5)) = 0.85
        _DriftSpeed ("Drift Speed", Range(0, 4)) = 0.8
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
            float _PurchaseProgress;
            fixed4 _ChargeColorA;
            fixed4 _ChargeColorB;
            fixed4 _EdgeColor;
            float _FlowAngle;
            float _EdgeBoost;
            float _NoiseScale;
            float _DissolveFeather;
            float _DissolveBias;
            float _DriftSpeed;

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
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;
                float progress = saturate(_PurchaseProgress);

                float2 centeredUv = i.uv - 0.5;
                float angleRad = radians(_FlowAngle);
                float2 flowDir = normalize(float2(cos(angleRad), sin(angleRad)));
                float projected = dot(centeredUv, flowDir) + 0.5;

                float pulse = sin(_Time.y * 9.0 + projected * 11.0) * 0.5 + 0.5;
                float energyPeak = saturate(1.0 - abs(progress - 0.35) / 0.35);
                float edgeMask = smoothstep(0.78, 1.0, max(abs(centeredUv.x), abs(centeredUv.y)));

                float2 noiseUv = i.uv * _NoiseScale + flowDir * (_Time.y * _DriftSpeed);
                float noise = Fbm(noiseUv);
                float directionalBias = dot(centeredUv, normalize(float2(0.75, -1.0))) * 0.28;
                float dissolveDrive = saturate((progress - 0.18) / 0.82);
                float dissolveThreshold = lerp(1.15, -0.1, dissolveDrive * _DissolveBias);
                float remaining = 1.0 - smoothstep(
                    dissolveThreshold - _DissolveFeather,
                    dissolveThreshold + _DissolveFeather,
                    noise + directionalBias
                );
                float dissolveEdge = 1.0 - saturate(abs((noise + directionalBias) - dissolveThreshold) / max(_DissolveFeather, 0.0001));

                fixed3 chargeRgb = lerp(_ChargeColorA.rgb, _ChargeColorB.rgb, saturate(projected + pulse * 0.18));
                fixed3 resultRgb = baseCol.rgb;

                resultRgb = lerp(resultRgb, resultRgb * 0.65 + chargeRgb * 0.35, energyPeak * 0.5);
                resultRgb += _EdgeColor.rgb * edgeMask * _EdgeBoost * (0.35 + energyPeak * 0.65);
                resultRgb += _EdgeColor.rgb * dissolveEdge * dissolveDrive * 1.35;

                float resultAlpha = baseCol.a * remaining;

                fixed4 result = fixed4(resultRgb, resultAlpha);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
