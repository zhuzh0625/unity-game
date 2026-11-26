Shader "Galleon/Hold Water"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Name "Hold waterline"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _OpenDeck;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 xz : TEXCOORD1;
                half fog : TEXCOORD2;
            };
            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.xz = positionOS.xz;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.xz;
                float t = _Time.y;
                float a = sin(dot(p, float2(1.2, 5.1)) + 0.49*t + 0.85*sin(p.x*2.3));
                float b = sin(dot(p, float2(-2.8, 3.7)) - 0.36*t + 0.55*cos(p.y*1.4));
                half3 normal = normalize(half3(0.085h*a + 0.035h*b, 1.0h, 0.08h*b - 0.025h*a));
                half3 view = TransformWorldToObjectDir(GetWorldSpaceNormalizeViewDir(i.positionWS));
                half incidence = saturate(abs(dot(normal, view)));
                half grazing = (1.0h - incidence) * (1.0h - incidence);
                half crests = smoothstep(0.53h, 0.93h, 0.75h*a + 0.25h*b);
                // A narrow meniscus where actual depth meets the water catches ladder rails and barrels.
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                float depthGap = LinearEyeDepth(rawDepth, _ZBufferParams) + TransformWorldToView(i.positionWS).z;
                half contact = 1.0h - smoothstep(0.004, 0.052, abs(depthGap));
                half3 water;
                half alpha;
                if (view.y >= 0.0h)
                {
                    water = lerp(half3(0.014h, 0.075h, 0.081h), half3(0.081h, 0.218h, 0.218h), grazing + (1.0h - grazing)*crests*0.68h);
                    alpha = 0.63h + 0.27h*grazing + 0.08h*crests;
                    // On the exposed deck the water reflects the same sky and broad
                    // colour bands as the surrounding sea. Shallow planks remain visible.
                    float2 sea = i.positionWS.xz;
                    half bands = .5h + .5h * sin(sea.y*.15 + sea.x*.09 + .8*sin(sea.x*.09) + t*.5);
                    half3 daylight = lerp(half3(.019h,.165h,.19h), half3(.028h,.235h,.245h), bands);
                    daylight = lerp(daylight, half3(.30h,.53h,.56h), grazing * .94h);
                    water = lerp(water, daylight + .015h*crests, _OpenDeck);
                }
                else
                {
                    // The underside reflects strongly at oblique angles. The opening is lighter overhead.
                    half reflection = 1.0h - smoothstep(0.56h, 0.82h, incidence);
                    water = lerp(half3(0.070h, 0.204h, 0.202h), half3(0.025h, 0.107h, 0.117h), reflection);
                    water += half3(0.025h, 0.044h, 0.041h)*crests;
                    alpha = lerp(0.49h, 0.90h, reflection) + 0.045h*crests;
                }
                water = lerp(water, half3(0.13h, 0.28h, 0.267h), contact*0.77h);
                return half4(MixFog(water, i.fog), lerp(alpha, 0.92h, contact));
            }
            ENDHLSL
        }
    }
}
