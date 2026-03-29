Shader "WildCard/ZoneBoundary"
{
    // Transparent force-field / energy wall shader for the zone boundary.
    // Renders scrolling hex grid + energy lines on a cylinder's side faces.
    // Visible from both inside and outside.

    Properties
    {
        _Color ("Wall Color", Color) = (0.2, 0.9, 0.3, 0.35)
        _EdgeColor ("Edge Glow", Color) = (0.3, 1.0, 0.4, 0.8)
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
        _GridScale ("Grid Scale", Float) = 8.0
        _FresnelPower ("Fresnel Power", Float) = 2.0
        _PulseSpeed ("Pulse Speed", Float) = 1.0
        _LineThickness ("Line Thickness", Range(0.01, 0.15)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off  // Visible from both sides

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
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float  heightFrac : TEXCOORD4;
            };

            fixed4 _Color;
            fixed4 _EdgeColor;
            float _ScrollSpeed;
            float _GridScale;
            float _FresnelPower;
            float _PulseSpeed;
            float _LineThickness;

            // Hex grid distance function
            float hexDist(float2 p)
            {
                p = abs(p);
                float d = dot(p, normalize(float2(1.0, 1.73)));
                return max(d, p.x);
            }

            float hexGrid(float2 uv, float scale)
            {
                float2 p = uv * scale;
                float2 r = float2(1.0, 1.73);
                float2 h = r * 0.5;

                float2 a = fmod(p, r) - h;
                float2 b = fmod(p + h, r) - h;

                float2 g = length(a) < length(b) ? a : b;
                float d = hexDist(g);

                // Return line pattern (1 near edges, 0 in center of hex)
                return smoothstep(0.5 - _LineThickness, 0.5, d);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);

                // Height fraction (0 = bottom, 1 = top) in object space
                o.heightFrac = v.vertex.y + 0.5; // Unity cylinder is -0.5 to 0.5
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── Fresnel (stronger at grazing angles) ──
                float fresnel = 1.0 - saturate(dot(i.viewDir, i.worldNormal));
                fresnel = pow(fresnel, _FresnelPower);

                // ── Hex grid pattern ──
                float2 gridUV = float2(
                    atan2(i.worldNormal.x, i.worldNormal.z) / 6.283 + 0.5,  // angle around cylinder
                    i.heightFrac
                );
                gridUV.y += _Time.y * _ScrollSpeed; // Scroll upward
                float hex = hexGrid(gridUV, _GridScale);

                // ── Horizontal scan line ──
                float scanLine = sin((i.heightFrac + _Time.y * _ScrollSpeed * 2.0) * 40.0);
                scanLine = smoothstep(0.95, 1.0, scanLine) * 0.3;

                // ── Pulse ──
                float pulse = sin(_Time.y * _PulseSpeed) * 0.15 + 0.85;

                // ── Height fade (fade out at top and bottom) ──
                float heightFade = smoothstep(0.0, 0.15, i.heightFrac) *
                                   smoothstep(1.0, 0.85, i.heightFrac);

                // ── Combine ──
                float pattern = (hex * 0.7 + scanLine + fresnel * 0.5) * pulse * heightFade;

                // Color: blend between main and edge based on fresnel
                fixed4 col = lerp(_Color, _EdgeColor, fresnel);
                col.a = saturate(pattern * col.a);

                // Minimum visibility so the wall is always slightly visible
                col.a = max(col.a, 0.03 * heightFade);

                return col;
            }
            ENDCG
        }
    }
}
