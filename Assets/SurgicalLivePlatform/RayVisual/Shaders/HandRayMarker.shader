Shader "SurgicalLivePlatform/HandRayMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.9, 1, 1)
        _HoleRadius ("Hole Radius", Range(0, 0.5)) = 0.31
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objectPos : TEXCOORD0;
            };

            fixed4 _Color;
            float _HoleRadius;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.objectPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(length(i.objectPos.xy) - _HoleRadius);
                return _Color;
            }
            ENDCG
        }
    }
}
