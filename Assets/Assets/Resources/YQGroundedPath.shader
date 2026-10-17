Shader "YourQuest/Grounded Path"
{
    Properties
    {
        _BaseMap("Approved packed earth", 2D) = "white" {}
        _BumpMap("Approved ground normal", 2D) = "bump" {}
        _BaseColor("Ground tint", Color) = (0.58,0.48,0.36,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; half4 colour:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; half coverage:TEXCOORD4; float fog:TEXCOORD5; };
            Varyings Vert(Attributes input)
            {
                // note: Ground contact is fixed by the terrain-owned mesh; only the verge coverage blends with the approved terrain material beneath it.
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.coverage = input.colour.a;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // note: A continuous world-space variance map blends four translated samples of the approved textures, hiding tile repetition without stretching or replacing the source asset.
                float2 map = input.positionWS.xz * .08;
                float2 cell = floor(map), f = frac(map);
                f = f * f * (3.0 - 2.0 * f);
                float2 jitterA = frac(sin(float2(dot(cell, float2(127.1,311.7)), dot(cell, float2(269.5,183.3)))) * 43758.5453);
                float2 jitterB = frac(sin(float2(dot(cell + float2(1,0), float2(127.1,311.7)), dot(cell + float2(1,0), float2(269.5,183.3)))) * 43758.5453);
                float2 jitterC = frac(sin(float2(dot(cell + float2(0,1), float2(127.1,311.7)), dot(cell + float2(0,1), float2(269.5,183.3)))) * 43758.5453);
                float2 jitterD = frac(sin(float2(dot(cell + 1, float2(127.1,311.7)), dot(cell + 1, float2(269.5,183.3)))) * 43758.5453);
                float2 dx = ddx(input.uv), dy = ddy(input.uv);
                half4 baseA = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, input.uv + jitterA, dx, dy);
                half4 baseB = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, input.uv + jitterB, dx, dy);
                half4 baseC = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, input.uv + jitterC, dx, dy);
                half4 baseD = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, input.uv + jitterD, dx, dy);
                half3 tangentNormal = normalize(lerp(lerp(UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BumpMap, input.uv + jitterA, dx, dy)),
                    UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BumpMap, input.uv + jitterB, dx, dy)), f.x),
                    lerp(UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BumpMap, input.uv + jitterC, dx, dy)),
                    UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BumpMap, input.uv + jitterD, dx, dy)), f.x), f.y));
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normal = normalize(tangentNormal.x * input.tangentWS.xyz + tangentNormal.y * bitangent + tangentNormal.z * input.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = SampleSH(normal) + sun.color * saturate(dot(normal, sun.direction)) * sun.shadowAttenuation;
                half3 colour = lerp(lerp(baseA.rgb, baseB.rgb, f.x), lerp(baseC.rgb, baseD.rgb, f.x), f.y) * _BaseColor.rgb * light;
                half wear = .84 + .16 * sin(dot(input.positionWS.xz, float2(.41,.29))) * sin(dot(input.positionWS.xz, float2(.13,-.37)));
                return half4(MixFog(colour, input.fog), saturate(input.coverage * wear));
            }
            ENDHLSL
        }
    }
}
