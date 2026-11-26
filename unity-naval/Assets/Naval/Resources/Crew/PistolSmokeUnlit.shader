Shader "Galleon/Pistol Smoke"
{
    Properties
    {
        [MainTexture] _MainTex ("Smoke", 2D) = "white" {}
        _Progress ("Age", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Progress;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float wave = sin(10.0 * uv.x + _Progress * 4.0) * sin(7.0 * uv.y - _Progress * 4.5);
                uv.y += wave * (.008 + .033 * _Progress);
                half4 smoke = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
                float folds = smoothstep(.02, .80, 1.0 + .35 * wave - .68 * _Progress);
                smoke.a *= folds * (1.0 - smoothstep(.20, 1.0, _Progress));
                return smoke;
            }
            ENDHLSL
        }
    }
}
