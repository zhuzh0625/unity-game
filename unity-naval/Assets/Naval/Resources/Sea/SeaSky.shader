Shader "Galleon/Trade wind sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Varyings { float4 clip:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings vert(float4 p:POSITION) { Varyings o; o.clip=TransformObjectToHClip(p.xyz); o.direction=p.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.13,311.71)))*43758.53); }
            float noise(float2 p)
            {
                float2 a=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 v=normalize(i.direction);
                float h=saturate(v.y);
                float3 color=lerp(float3(.65,.77,.79),float3(.13,.38,.60),pow(h,.55));
                float2 uv=v.xz/max(.09,v.y+.08)*2.4;
                float cloud=noise(uv)*.56+noise(uv*2+.23)*.27+noise(uv*4+.61)*.12+noise(uv*8)*.05;
                float cover=smoothstep(.53,.75,cloud)*smoothstep(-.01,.15,v.y);
                color=lerp(color,float3(.96,.92,.83),cover*.75);
                float sun=saturate(dot(v,normalize(float3(-.34,.55,.66))));
                color+=float3(.48,.33,.16)*pow(sun,25)+float3(2.0,1.55,.83)*smoothstep(.9992,.9998,sun);
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}
