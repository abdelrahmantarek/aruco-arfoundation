Shader "AR/TransparentPlane"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.3)
        _GridColor ("Grid Color", Color) = (0,1,0,0.8)
        _GridScale ("Grid Scale", Float) = 10.0
        _GridWidth ("Grid Width", Float) = 0.05
        _FadeDistance ("Fade Distance", Float) = 5.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GridColor;
                float _GridScale;
                float _GridWidth;
                float _FadeDistance;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.fogFactor = ComputeFogFactor(o.vertex.z);
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Calculate distance from camera for fading
                float3 cameraPos = GetCameraPositionWS();
                float distance = length(i.worldPos - cameraPos);
                float fadeAlpha = saturate(1.0 - (distance / _FadeDistance));

                // Create grid pattern
                float2 grid = abs(frac(i.uv * _GridScale) - 0.5) / fwidth(i.uv * _GridScale);
                float gridLine = min(grid.x, grid.y);
                float gridMask = 1.0 - min(gridLine / _GridWidth, 1.0);

                // Blend base color with grid
                float4 baseColor = _Color;
                float4 finalColor = lerp(baseColor, _GridColor, gridMask);
                
                // Apply distance fading
                finalColor.a *= fadeAlpha;
                
                // Apply fog
                finalColor.rgb = MixFog(finalColor.rgb, i.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    // Fallback for Built-in Render Pipeline
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
            };

            fixed4 _Color;
            fixed4 _GridColor;
            float _GridScale;
            float _GridWidth;
            float _FadeDistance;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate distance from camera for fading
                float distance = length(i.worldPos - _WorldSpaceCameraPos);
                float fadeAlpha = saturate(1.0 - (distance / _FadeDistance));

                // Create grid pattern
                float2 grid = abs(frac(i.uv * _GridScale) - 0.5) / fwidth(i.uv * _GridScale);
                float gridLine = min(grid.x, grid.y);
                float gridMask = 1.0 - min(gridLine / _GridWidth, 1.0);

                // Blend base color with grid
                fixed4 baseColor = _Color;
                fixed4 finalColor = lerp(baseColor, _GridColor, gridMask);
                
                // Apply distance fading
                finalColor.a *= fadeAlpha;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                
                return finalColor;
            }
            ENDCG
        }
    }
}
