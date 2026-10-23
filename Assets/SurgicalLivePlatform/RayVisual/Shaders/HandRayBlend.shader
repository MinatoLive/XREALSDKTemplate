Shader "SurgicalLivePlatform/HandRayBlend"
{
    Properties
    {
        _MainTex ("Dash Texture", 2D) = "white" {}
        _SolidAmount ("Solid Amount", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _SolidAmount;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed dashAlpha = tex2D(_MainTex, i.uv).a;
                fixed solidAmount = saturate(_SolidAmount);
                fixed alpha = lerp(dashAlpha, 1.0, solidAmount);
                fixed4 color = i.color;
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}
