Shader "UI/HighLigh"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _HighlightColor ("Highlight Color", Color) = (0.35, 1.0, 0.78, 1.0)
        _BaseGlow ("Base Glow", Range(0, 2)) = 0.35

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.4
        _PulseStrength ("Pulse Strength", Range(0, 2)) = 0.4

        _SweepCycle ("Sweep Cycle", Range(0.5, 6)) = 2.2
        _SweepActiveTime ("Sweep Active Time", Range(0.1, 3)) = 0.55
        _SweepWidth ("Sweep Width", Range(0.02, 0.5)) = 0.18
        _SweepStrength ("Sweep Strength", Range(0, 3)) = 1.1
        _SweepAngle ("Sweep Angle", Range(-180, 180)) = 28

        _EdgeBoost ("Edge Boost", Range(0, 3)) = 0.55
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.5)) = 0.18
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
            fixed4 _HighlightColor;
            float _BaseGlow;
            float _PulseSpeed;
            float _PulseStrength;
            float _SweepCycle;
            float _SweepActiveTime;
            float _SweepWidth;
            float _SweepStrength;
            float _SweepAngle;
            float _EdgeBoost;
            float _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainCol = tex2D(_MainTex, i.uv) * i.color;

                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                float pulseGlow = _BaseGlow + pulse * _PulseStrength;

                float angleRad = radians(_SweepAngle);
                float2 sweepDir = normalize(float2(cos(angleRad), sin(angleRad)));

                float cycle = max(_SweepCycle, 0.001);
                float activeTime = min(_SweepActiveTime, cycle);
                float cycleT = frac(_Time.y / cycle) * cycle;

                float sweepBand = 0.0;
                if (cycleT < activeTime)
                {
                    float sweepProgress = cycleT / max(activeTime, 0.001);

                    float2 centeredUv = i.uv - 0.5;
                    float projected = dot(centeredUv, sweepDir) + 0.5;
                    float bandCenter = lerp(-_SweepWidth, 1.0 + _SweepWidth, sweepProgress);
                    float bandDistance = abs(projected - bandCenter);
                    float activeFade =
                        smoothstep(0.0, 0.12, sweepProgress) *
                        (1.0 - smoothstep(0.82, 1.0, sweepProgress));

                    sweepBand = 1.0 - smoothstep(_SweepWidth * 0.5, _SweepWidth, bandDistance);
                    sweepBand *= activeFade;
                }

                float2 centeredUv = abs(i.uv * 2.0 - 1.0);
                float edgeMask = smoothstep(1.0 - _EdgeSoftness, 1.0, max(centeredUv.x, centeredUv.y));

                float glowStrength = pulseGlow + sweepBand * _SweepStrength + edgeMask * _EdgeBoost;

                fixed3 highlightRgb = _HighlightColor.rgb * glowStrength;
                fixed alphaMask = mainCol.a;

                fixed4 result;
                result.a = alphaMask;
                result.rgb = mainCol.rgb * mainCol.a + highlightRgb * alphaMask;
                return result;
            }
            ENDCG
        }
    }
}
