Shader "Galleon/Hold Water Volume"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "Light absorption through standing water"
            Tags { "LightMode"="UniversalForward" }
            // The back of the closed hull is the ray exit, even when the eye is in water.
            // Opaque geometry between the eye and this exit is handled by the depth sample.
            Cull Front
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HoldBounds;  // floor, waterline, aft, fore in hull space
                float4 _HullBounds;   // length, section height, maximum half beam, sea overlap
                float _SideInset;
                float _SectionSpan;
            CBUFFER_END
            TEXTURE2D(_HullWidths);
            SAMPLER(sampler_HullWidths);

            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS);
                o.positionOS = positionOS;
                return o;
            }

            void ClipEntry(float insideDistance, float approach, inout float entry)
            {
                if (insideDistance < 0.0)
                    entry = max(entry, approach > 0.00001 ? -insideDistance / approach : 1e7);
            }

            bool InsideSides(float3 p)
            {
                float2 uv = (saturate(p.zy / float2(_SectionSpan, _HullBounds.y)) * float2(61, 50) + .5) / float2(62, 51);
                float width = SAMPLE_TEXTURE2D_LOD(_HullWidths, sampler_HullWidths, uv, 0).r;
                return abs(p.x) <= max(0.0, width - _SideInset);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 eye = TransformWorldToObject(GetCameraPositionWS());
                float3 toExit = i.positionOS - eye;
                float distanceToExit = max(length(toExit), 0.0001);
                float3 ray = toExit / distanceToExit;
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                float opaqueEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float exitEyeDepth = -TransformWorldToView(TransformObjectToWorld(i.positionOS)).z;
                float end = distanceToExit * saturate(opaqueEyeDepth / max(exitEyeDepth, 0.0001));
                float entry = 0.0;
                ClipEntry(_HoldBounds.y - eye.y, -ray.y, entry);
                ClipEntry(eye.y - _HoldBounds.x, ray.y, entry);
                ClipEntry(eye.z - _HoldBounds.z, ray.z, entry);
                ClipEntry(_HoldBounds.w - eye.z, -ray.z, entry);
                // Bound the search to the hull even for a camera on the other ship.
                ClipEntry(_HullBounds.z + eye.x, ray.x, entry);
                ClipEntry(_HullBounds.z - eye.x, -ray.x, entry);
                if (end <= entry) discard;
                if (!InsideSides(eye + entry * ray))
                {
                    // The drawn back face is the exit. Find its matching entry on the
                    // measured hull, including the narrowing sides above the gun decks.
                    float outside = entry, inside = distanceToExit;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float mid = .5 * (inside + outside);
                        if (InsideSides(eye + mid * ray)) inside = mid;
                        else outside = mid;
                    }
                    entry = inside;
                }
                float thickness = max(0.0, end - entry);
                half opacity = 1.0h - exp2(-0.86 * thickness);
                half fromAbove = saturate(0.42h + 0.74h * ray.y);
                half3 water = lerp(half3(0.012h, 0.064h, 0.075h), half3(0.046h, 0.162h, 0.170h), fromAbove);
                return half4(water, opacity);
            }
            ENDHLSL
        }
    }
}
