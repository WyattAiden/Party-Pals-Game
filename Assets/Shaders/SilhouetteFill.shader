// Draws a see-through, coloured silhouette of a player only where the
// player is hidden behind something (ZTest Greater).
Shader "PartyPals/SilhouetteFill"
{
    Properties
    {
        _Color ("Silhouette Color", Color) = (1, 1, 1, 0.5)
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2
        _RimBoost ("Rim Brightness", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+11" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite Off
            ZTest Greater
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            // Skip pixels where a player is visible, and only draw once per
            // pixel so overlapping body parts don't stack up and get darker.
            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp NotEqual
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _RimPower;
            float _RimBoost;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float rim : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldNormal = normalize(UnityObjectToWorldNormal(v.normal));
                float3 viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.rim = pow(1.0 - saturate(dot(worldNormal, viewDir)), _RimPower);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _Color;
                c.rgb += i.rim * _RimBoost;
                c.a = saturate(_Color.a + i.rim * _RimBoost * 0.5);
                return c;
            }
            ENDCG
        }
    }
}
