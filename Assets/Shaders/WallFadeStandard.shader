// Standard-like surface shader that dithers out only the pixels sitting between
// the camera and a player (a see-through "tunnel"), leaving the rest of the mesh solid.
// WallFader.cs swaps blocking renderers onto copies of this at runtime and feeds the
// player positions in as globals.
Shader "PartyPals/WallFadeStandard"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _FadeAmount ("Fade Amount", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _METALLICGLOSSMAP
        #pragma shader_feature_local _EMISSION

        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _EmissionMap;
        fixed4 _Color;
        half _Metallic, _Glossiness, _BumpScale, _FadeAmount;
        half4 _EmissionColor;

        // Set globally by WallFader.cs
        float4 _WallFadePos[4];   // xyz = player target point
        float _WallFadeCount;
        float _WallFadeRadius;    // tunnel radius (world units)
        float _WallFadeMargin;    // don't cut within this distance in front of the player
        float _WallFadeAlpha;     // fraction of pixels kept inside the tunnel

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 screenPos;
        };

        static const float bayer[16] = {
             0.0/16,  8.0/16,  2.0/16, 10.0/16,
            12.0/16,  4.0/16, 14.0/16,  6.0/16,
             3.0/16, 11.0/16,  1.0/16,  9.0/16,
            15.0/16,  7.0/16, 13.0/16,  5.0/16 };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float hole = 0;
            float3 cam = _WorldSpaceCameraPos;
            [unroll] for (int i = 0; i < 4; i++)
            {
                if (i < (int)_WallFadeCount)
                {
                    float3 seg = _WallFadePos[i].xyz - cam;
                    float len = length(seg);
                    float3 dir = seg / max(len, 1e-4);
                    float along = dot(IN.worldPos - cam, dir);
                    float d = length((IN.worldPos - cam) - dir * along);
                    float radial = 1 - smoothstep(_WallFadeRadius * 0.6, _WallFadeRadius, d);
                    float inFront = 1 - smoothstep(len - _WallFadeMargin - 0.5, len - _WallFadeMargin, along);
                    float afterCam = step(0, along);
                    hole = max(hole, radial * inFront * afterCam);
                }
            }

            float keep = lerp(1, _WallFadeAlpha, hole * _FadeAmount);
            float2 pix = floor(IN.screenPos.xy / max(IN.screenPos.w, 1e-4) * _ScreenParams.xy);
            int idx = (int)(fmod(pix.y, 4) * 4 + fmod(pix.x, 4));
            clip(keep - bayer[idx] - 0.001);

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            #ifdef _NORMALMAP
                o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #endif
            #ifdef _METALLICGLOSSMAP
                fixed4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
                o.Metallic = mg.r;
                o.Smoothness = mg.a * _Glossiness;
            #else
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
            #endif
            #ifdef _EMISSION
                o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            #endif
            o.Alpha = 1;
        }
        ENDCG
    }
    // Shadows come from the fallback, so walls keep casting full shadows.
    FallBack "Legacy Shaders/VertexLit"
}
