Shader "Custom/SimpleOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            ZTest LEqual
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            float _OutlineWidth;
            float4 _OutlineColor;
            
            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal);
                // Expand the mesh slightly outwards
                v.vertex.xyz += norm * _OutlineWidth;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            half4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
