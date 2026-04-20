Shader "UI/ExileVoidShatter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _ExileProgress ("Exile Progress", Range(0, 1)) = 0
        _CrackColor ("Crack Color", Color) = (0.70, 0.28, 1.0, 1.0)
        _CrackGlowColor ("Crack Glow Color", Color) = (0.35, 0.02, 1.0, 1.0)
        _EdgeFlashColor ("Edge Flash Color", Color) = (1.0, 0.70, 1.0, 1.0)

        _CrackScale ("Crack Scale", Range(3, 24)) = 10
        _CrackThickness ("Crack Thickness", Range(0.005, 0.12)) = 0.028
        _CrackSoftness ("Crack Softness", Range(0.003, 0.12)) = 0.018
        _CrackDrift ("Crack Drift", Range(0, 2)) = 0.14

        _ShardSpread ("Shard Spread", Range(0.5, 3.0)) = 1.45
        _FragmentOffset ("Fragment Offset", Range(0, 0.12)) = 0.028
        _DissolveFeather ("Dissolve Feather", Range(0.01, 0.25)) = 0.06
        _VanishStart ("Vanish Start", Range(0, 1)) = 0.45

        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1.6
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.35
        _ShatterCenter ("Shatter Center (UV)", Vector) = (0.5, 0.5, 0, 0)
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
            float _ExileProgress;
            fixed4 _CrackColor;
            fixed4 _CrackGlowColor;
            fixed4 _EdgeFlashColor;
            float _CrackScale;
            float _CrackThickness;
            float _CrackSoftness;
            float _CrackDrift;
            float _ShardSpread;
            float _FragmentOffset;
            float _DissolveFeather;
            float _VanishStart;
            float _GlowIntensity;
            float _TintStrength;
            float4 _ShatterCenter;

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

            float2 Hash22(float2 p)
            {
                float n = sin(dot(p, float2(127.1, 311.7)));
                float m = sin(dot(p, float2(269.5, 183.3)));
                return frac(float2(n, m) * 43758.5453);
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
                p *= 2.03;
                amplitude *= 0.5;

                value += Noise21(p) * amplitude;
                p *= 2.01;
                amplitude *= 0.5;

                value += Noise21(p) * amplitude;
                return value;
            }

            void VoronoiEdge(float2 uv, out float edgeDistance, out float2 nearestVector, out float cellSeed)
            {
                float2 cellBase = floor(uv);
                float2 cellFrac = frac(uv);

                float minDistance = 8.0;
                float2 bestVector = 0.0;
                float2 bestCell = 0.0;

                [unroll]
                for (int firstY = -1; firstY <= 1; firstY++)
                {
                    [unroll]
                    for (int firstX = -1; firstX <= 1; firstX++)
                    {
                        float2 firstNeighbor = float2(firstX, firstY);
                        float2 firstPoint = Hash22(cellBase + firstNeighbor);
                        float2 firstRelative = firstNeighbor + firstPoint - cellFrac;
                        float distanceSquared = dot(firstRelative, firstRelative);

                        if (distanceSquared < minDistance)
                        {
                            minDistance = distanceSquared;
                            bestVector = firstRelative;
                            bestCell = firstNeighbor;
                        }
                    }
                }

                edgeDistance = 8.0;

                [unroll]
                for (int secondY = -1; secondY <= 1; secondY++)
                {
                    [unroll]
                    for (int secondX = -1; secondX <= 1; secondX++)
                    {
                        float2 secondNeighbor = float2(secondX, secondY);
                        float2 secondPoint = Hash22(cellBase + secondNeighbor);
                        float2 secondRelative = secondNeighbor + secondPoint - cellFrac;
                        float2 secondDelta = secondRelative - bestVector;
                        float secondLengthDelta = length(secondDelta);

                        if (secondLengthDelta > 0.0001)
                        {
                            edgeDistance = min(edgeDistance, dot(0.5 * (bestVector + secondRelative), secondDelta / secondLengthDelta));
                        }
                    }
                }

                nearestVector = bestVector;
                cellSeed = Hash21(cellBase + bestCell);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float progress = saturate(_ExileProgress);
                float2 centerUv = _ShatterCenter.xy;

                float2 crackUv = i.uv * _CrackScale + float2(_Time.y * _CrackDrift, -_Time.y * _CrackDrift * 0.37);
                float edgeDistance;
                float2 nearestVector;
                float cellSeed;
                VoronoiEdge(crackUv, edgeDistance, nearestVector, cellSeed);

                float spreadNoise = Fbm(crackUv * 0.65 + cellSeed * 8.13);
                float radialDistance = distance(i.uv, centerUv);
                float spreadFront = progress * (_ShardSpread + 0.7 + spreadNoise * 0.35) - radialDistance * (1.25 + spreadNoise * 0.55);
                float spreadMask = smoothstep(-0.16, 0.16, spreadFront);

                float crackBand = 1.0 - smoothstep(_CrackThickness, _CrackThickness + _CrackSoftness, edgeDistance);
                float crackMask = crackBand * spreadMask;

                float2 shardDirection = normalize((i.uv - centerUv) + nearestVector * 0.45 + float2(0.001, 0.002));
                float shardProgress = saturate((progress - (0.16 + cellSeed * 0.18)) / (0.84 - cellSeed * 0.12));
                float2 displacedUv = i.uv - shardDirection * _FragmentOffset * shardProgress * spreadMask;

                fixed4 baseCol = tex2D(_MainTex, displacedUv) * i.color;
                float sourceAlpha = baseCol.a;

                float dissolveNoise = Fbm(crackUv * 0.52 + shardDirection * 3.4 + cellSeed * 11.7);
                float vanishProgress = saturate((progress - _VanishStart) / max(1.0 - _VanishStart, 0.0001));
                float vanishThreshold = lerp(1.08, -0.14, vanishProgress);
                float dissolveInput = dissolveNoise + radialDistance * 0.33 - spreadMask * 0.14;
                float remaining = 1.0 - smoothstep(
                    vanishThreshold - _DissolveFeather,
                    vanishThreshold + _DissolveFeather,
                    dissolveInput
                );
                float dissolveEdge = 1.0 - saturate(abs(dissolveInput - vanishThreshold) / max(_DissolveFeather, 0.0001));

                float glowPulse = sin(_Time.y * 13.0 + cellSeed * 21.0) * 0.5 + 0.5;
                float3 crackRgb = lerp(_CrackColor.rgb, _CrackGlowColor.rgb, saturate(spreadNoise * 0.85));
                float3 tintedBase = lerp(baseCol.rgb, baseCol.rgb * float3(0.78, 0.56, 1.0), progress * _TintStrength);
                float crackTailFade = 1.0 - smoothstep(0.68, 0.96, progress);
                crackTailFade *= 1.0 - vanishProgress * 0.55;
                float visibleCrackMask = crackMask * crackTailFade;

                float3 resultRgb = lerp(baseCol.rgb, tintedBase, spreadMask * 0.45);
                resultRgb += crackRgb * visibleCrackMask * _GlowIntensity * (0.4 + glowPulse * 0.25);
                resultRgb += _EdgeFlashColor.rgb * dissolveEdge * vanishProgress * 0.95 * spreadMask;

                float resultAlpha = sourceAlpha * remaining;
                resultAlpha = max(resultAlpha, sourceAlpha * visibleCrackMask * 0.18 * (1.0 - vanishProgress * 0.8));

                fixed4 result = fixed4(resultRgb, resultAlpha);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
