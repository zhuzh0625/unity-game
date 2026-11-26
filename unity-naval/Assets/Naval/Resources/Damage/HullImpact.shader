Shader "Galleon/Hull Impact"
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
            Name "Hull Impact"
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

                half3 oak = lerp(half3(0.051h, 0.021h, 0.008h), half3(0.36h, 0.159h, 0.052h), damage.r);
                oak = lerp(oak, half3(0.009h, 0.003h, 0.001h), damage.g);
                half daylight = saturate(dot(normalize(input.normalWS), GetMainLight().direction));
                half3 split = oak * (0.62h + daylight * 0.48h);
                return half4(MixFog(split, input.fog), damage.a);
            }
            ENDHLSL
        }
    }
}