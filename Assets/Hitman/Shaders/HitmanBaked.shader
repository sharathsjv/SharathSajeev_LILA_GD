// Static scenery lit for the day/night cycle (see DayNight.cs):
//   colour = albedo * (live sun + sky colour * baked sky layer + lamp light (baked lightmap) * lamp factor) + emission
// The baked lightmap (unity_Lightmap) holds the lamps/bulbs layer; the sky-occlusion layer is a second lightmap
// handed in per renderer as _SkyLightmap. The sun has no real-time shadows: shadows come from the bake.
Shader "Hitman/Baked"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle] _NightEmission ("Glow only at night (lamps, lit windows)", Float) = 0
        [NoScaleOffset] _SkyLightmap ("Sky Lightmap (set by DayNight)", 2D) = "white" {}
        // Used by the URP Lit meta pass when baking.
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_SkyLightmap); SAMPLER(sampler_SkyLightmap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _EmissionMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Cutoff;
                half _NightEmission;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            // Set every frame by DayNight.
            half4 _HitmanSky;     // sky/ambient colour for the time of day
            half _HitmanLamps;    // 0 = lamps off (day), 1 = full night
            half4 _HitmanSun;     // the scenery's sun colour x intensity (the real light is toned down for characters)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uvLM : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvLM : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.uvLM = i.uvLM * unity_LightmapST.xy + unity_LightmapST.zw;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif
                Light sun = GetMainLight();
                half3 n = normalize(i.normalWS);
                half3 light = _HitmanSun.rgb * saturate(dot(n, sun.direction));

                half sky = 1;
                half3 lamps = 0;
                #ifdef LIGHTMAP_ON
                    half4 enc = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, i.uvLM);
                    lamps = DecodeLightmap(enc, half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0, 0));
                    sky = SAMPLE_TEXTURE2D(_SkyLightmap, sampler_SkyLightmap, i.uvLM).r;
                #endif
                light += _HitmanSky.rgb * sky + lamps * _HitmanLamps;

                half3 glow = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                glow *= lerp(1, _HitmanLamps, _NightEmission);

                half3 col = albedo.rgb * light + glow;
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/META"
    }
    FallBack "Universal Render Pipeline/Lit"
}
