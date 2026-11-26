Shader "Galleon/R13 Surface"
{
    Properties
    {
        [HideInInspector] _BaseMap("Base", 2D) = "white" {}
        [HideInInspector] _BaseColor("Color", Color) = (1,1,1,1)
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _SHIP_PAINT_MAP
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Attributes { float3 pos : POSITION; float3 normal : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half4 color : COLOR; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.pos);
                o.pos = p.positionCS; o.world = p.positionWS;
                o.normal = TransformObjectToWorldNormal(i.normal);
                o.color = i.color; o.uv = i.uv;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 pigment = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    pigment = SRGBToLinear(pigment);
                #endif
                float2 uv = i.uv;
                #if defined(_SHIP_PAINT_MAP)
                    pigment = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                #endif
                half metal = step(.9h,i.color.a);
                if (i.color.a > .2h && i.color.a < .5h)
                {
                    float grain = uv.y * 170 + 4.5*sin(uv.x*1.65 + uv.y*6) + 2.1*sin(uv.x*3.9 - uv.y*12);
                    float fade = saturate(1 - fwidth(grain)*.28);
                    float band = sin(grain) + .45*sin(grain*1.94 + .8);
                    pigment *= 1 + fade*band*.075 + .055*sin(uv.y*21 + sin(uv.x*.43)*1.7);
                }
                else if (i.color.a > .55h && i.color.a < .85h)
                {
                    float2 weave = uv*180;
                    pigment *= 1 + .034*(sin(weave.x)+sin(weave.y))*saturate(1-length(fwidth(weave))*.3);
                }
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
                half direct = saturate(dot(n,sun.direction));
                half3 sky = max(SampleSH(n), half3(.18,.17,.16));
                half3 lit = pigment*(sky + sun.color * (direct * sun.shadowAttenuation));
                half3 h = SafeNormalize(sun.direction+GetWorldSpaceNormalizeViewDir(i.world));
                lit += sun.color * pow(saturate(dot(n,h)),34)*metal*.26*sun.shadowAttenuation;
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
