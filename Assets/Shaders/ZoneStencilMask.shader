Shader "WildCard/ZoneStencilMask"
{
    // This shader renders NOTHING visible — it only writes to the stencil buffer.
    // Attach this to the ZONE CYLINDER (the safe area).
    // Any pixel covered by this object gets stencil value = 1.
    // The gas shader will skip those pixels (only render where stencil != 1).

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        
        Pass
        {
            // Don't write any color or depth — invisible
            ColorMask 0
            ZWrite Off

            // Write stencil value 1 wherever this mesh is drawn
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 0; // Nothing visible
            }
            ENDCG
        }
    }
}
