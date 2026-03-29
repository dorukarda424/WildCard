Shader "WildCard/PoisonGas"
{
    // This shader renders a scrolling, semi-transparent poison gas effect.
    // It ONLY draws where the stencil buffer is NOT 1 (outside the safe zone).
    // Attach this to a LARGE cylinder that covers the entire map.

    Properties
    {
        _MainColor ("Gas Color", Color) = (0.2, 0.8, 0.1, 0.3)
        _EdgeColor ("Edge Glow Color", Color) = (0.4, 1.0, 0.2, 0.6)
        _ScrollSpeed ("Scroll Speed", Float) = 0.3
        _NoiseScale ("Noise Scale", Float) = 2.0
        _Density ("Gas Density", Range(0, 1)) = 0.4
        _EdgeWidth ("Edge Blend Width", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        // Render AFTER the stencil mask, and as transparent
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            // Only render where stencil != 1 (outside the safe zone)
            Stencil
            {
                Ref 1
                Comp NotEqual
            }

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
                float3 viewDir : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
            };

            fixed4 _MainColor;
            fixed4 _EdgeColor;
            float _ScrollSpeed;
            float _NoiseScale;
            float _Density;
            float _EdgeWidth;

            // Simple pseudo-noise function
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.13);
                p3 += dot(p3, p3.yzx + 3.333);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Scrolling noise for gas movement
                float2 scrollUV = i.worldPos.xz * _NoiseScale * 0.1;
                scrollUV += _Time.y * _ScrollSpeed * float2(0.7, 0.3);

                float n = fbm(scrollUV);
                float n2 = fbm(scrollUV * 1.5 + float2(5.2, 1.3) + _Time.y * _ScrollSpeed * 0.5);
                
                float gasMask = saturate(n * n2 * 2.0);

                // Edge fresnel — gas is denser when viewed at grazing angles
                float fresnel = 1.0 - saturate(dot(i.viewDir, i.worldNormal));
                fresnel = pow(fresnel, 1.5);

                // Combine
                float alpha = gasMask * _Density * (0.5 + fresnel * 0.5);
                
                // Blend between main color and edge color based on noise
                fixed4 col = lerp(_MainColor, _EdgeColor, n * fresnel);
                col.a = alpha;

                return col;
            }
            ENDCG
        }
    }
}
