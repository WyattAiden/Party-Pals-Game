// Marks the pixels where a player can be seen normally (not hidden).
// Draws nothing visible; it only writes to the stencil buffer so the
// silhouette pass knows not to draw over the player's own body.
Shader "PartyPals/SilhouetteMask"
{
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Cull Back

            Stencil
            {
                Ref 64
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
