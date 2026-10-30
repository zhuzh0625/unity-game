Shader "CoastRush/Sea"
{
    Properties { _DeepColor("Deep water",Color)=(0.015,0.32,0.43,1) _ShallowColor("Sunlit water",Color)=(0.08,0.64,0.65,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;};
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;half4 _ShallowColor;
            CBUFFER_END
            Varyings vert(Attributes a)
            {
                Varyings o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y;
                float wave=sin(p.x*.085+p.y*.12+t*.42)*sin(p.x*.062-p.y*.17-t*.3);
                float shine=pow(saturate(sin(p.x*.7+p.y*.19+t*.9)*sin(p.y*.49-t*.62)),24);
                half3 col=lerp(_DeepColor.rgb,_ShallowColor.rgb,.4+wave*.18)+shine*.13;
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
    }
}
