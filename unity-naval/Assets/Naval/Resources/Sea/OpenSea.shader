Shader "Galleon/Open Sea"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+15" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4x4 _ShipA, _ShipB;
            float4 _DeckFlood;
            float4 _ShipBoundsA, _ShipBoundsB;
            float4 _SectionSpans;
            TEXTURE2D(_ShipWidthsA); SAMPLER(sampler_ShipWidthsA);
            TEXTURE2D(_ShipWidthsB); SAMPLER(sampler_ShipWidthsB);
            struct Varyings { float4 clip : SV_POSITION; float3 world : TEXCOORD0; };
            float wave(float2 p, float t)
            {
                return .12*sin(dot(p,float2(.12,.19))-t*.8) + .048*sin(dot(p,float2(-.34,.17))+t*1.12);
            }
            float hullShelter(float3 p, float4 hull)
            {
                // Include the neighbouring ocean-grid vertices so a wave trough never
                // exposes the highest deck once the hull settles level with the sea.
                float mid = hull.x * .5;
                float2 d = max(abs(p.xz - float2(0, mid)) - float2(hull.z, mid), 0);
                return 1 - smoothstep(6, 14, length(d));
            }
            Varyings vert(float4 p:POSITION)
            {
                Varyings o;
                o.world = TransformObjectToWorld(p.xyz);
                float4 world = float4(o.world, 1);
                float sheltered = max(_DeckFlood.x * hullShelter(mul(_ShipA, world).xyz, _ShipBoundsA),
                                      _DeckFlood.y * hullShelter(mul(_ShipB, world).xyz, _ShipBoundsB));
                o.world.y += (1 - sheltered) * wave(o.world.xz, _Time.y);
                o.clip = TransformWorldToHClip(o.world);
                return o;
            }
            bool insideShip(float3 p, float4 hull, float span, TEXTURE2D_PARAM(widths, widthsSampler))
            {
                if (hull.x <= 0 || abs(p.x) > hull.z || p.z < 0 || p.z > hull.x) return false;
                float2 uv = (saturate(p.zy / max(float2(span, hull.y), .001)) * float2(61, 50) + .5) / float2(62, 51);
                float width = SAMPLE_TEXTURE2D_LOD(widths, widthsSampler, uv, 0).r;
                return abs(p.x) < width - hull.w;
            }
            half4 frag(Varyings i) : SV_Target
            {
                if (insideShip(mul(_ShipA,float4(i.world,1)).xyz, _ShipBoundsA, _SectionSpans.x, TEXTURE2D_ARGS(_ShipWidthsA, sampler_ShipWidthsA)) ||
                    insideShip(mul(_ShipB,float4(i.world,1)).xyz, _ShipBoundsB, _SectionSpans.y, TEXTURE2D_ARGS(_ShipWidthsB, sampler_ShipWidthsB))) discard;
                float2 p=i.world.xz;
                float t=_Time.y;
                float2 d = p - _WorldSpaceCameraPos.xz;
                float detail = 1-smoothstep(45,250,length(d));
                float a=cos(dot(p,float2(.12,.19))-t*.8), b=cos(dot(p,float2(-.34,.17))+t*1.12);
                float c=cos(p.x*1.43+p.y*.54+1.3*sin(p.y*.7+t*.31)-t*1.2);
                float e=cos(p.y*2.3-p.x*.45+t*1.7+.7*sin(p.x*1.4));
                float3 n=normalize(float3(-.09*a+.105*b+detail*(.10*c-.03*e),1,-.17*a-.08*b+detail*(.09*e+.025*c)));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float fres=pow(1-saturate(dot(n,view)),3.2);
                float3 sun=normalize(float3(-.34,.55,.66));
                float shimmer=pow(saturate(dot(reflect(-sun,n),view)),85);
                float bands=.5+.5*sin(p.y*.15+p.x*.09+.8*sin(p.x*.09)+t*.5);
                float3 color=lerp(float3(.019,.165,.19),float3(.028,.235,.245),bands);
                color=lerp(color,float3(.30,.53,.56),fres*.94)+float3(1.1,.85,.53)*shimmer;
                color=lerp(color,float3(.49,.65,.68),smoothstep(260,1700,length(d))*.78);
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}
