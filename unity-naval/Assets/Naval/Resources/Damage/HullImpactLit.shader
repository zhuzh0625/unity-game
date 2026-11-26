Shader "Galleon/Hull Impact Lit"
{
    Properties
    {
        [NoScaleOffset] _ImpactMap ("Split oak, fissure, pulse, coverage", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Name "Hull Impact Lit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZTest LEqual
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ImpactMap);
            SAMPLER(sampler_ImpactMap);


            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 damage = SAMPLE_TEXTURE2D(_ImpactMap, sampler_ImpactMap, input.uv);
                clip(damage.a - 0.02h);

                // The full impact silhouette remains legible even on unlit planks below deck.
                half fissure = max(damage.b, damage.g);
                half cover = smoothstep(0.025h, 0.55h, damage.a);
                return half4(1.65h + fissure, 0.009h + 0.012h * damage.r, 0.004h, cover);
            }
            ENDHLSL
        }
    }
}
