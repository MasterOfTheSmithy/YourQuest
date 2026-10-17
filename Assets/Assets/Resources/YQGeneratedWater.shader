Shader "YourQuest/Generated Water"
{
    Properties
    {
        _BumpMap("Imported ripple normal", 2D) = "bump" {}
        _ShallowColor("Shallow water", Color) = (0.055,0.28,0.62,1)
        _DeepColor("Deep water", Color) = (0.008,0.045,0.16,1)
        _FlowSpeed("Flow speed", Float) = 0.075
        _RippleStrength("Ripple strength", Float) = 0.22
        _WorldUV("Use lake world coordinates", Float) = 0
        _FoamOnly("Render bank foam", Float) = 0
        _FoamOpacity("Bank foam opacity", Range(0,1)) = 0.72
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            // note: Generated river ribbons can be viewed from either bank and may contain tight bends; disabling backface culling keeps the accepted water visible when a resampled segment reverses its local winding.
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST;
                half4 _ShallowColor, _DeepColor;
                float _FlowSpeed, _RippleStrength, _WorldUV, _FoamOnly, _FoamOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 colour:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; half turbulence:TEXCOORD4; half coverage:TEXCOORD5; half bankAcross:TEXCOORD6; half flowFade:TEXCOORD7; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // note: Keep the accepted water elevation unchanged; only the shading moves.
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = lerp(input.uv * 2, output.positionWS.xz * 0.11, _WorldUV);
                output.fog = ComputeFogFactor(output.positionCS.z);
                // note: World-space UVs remove texture seams, while vertex colour still carries river-mouth foam and fade coverage.
                output.turbulence = input.colour.r;
                // note: Green carries the physical left-to-right ribbon coordinate because world UVs cannot describe shoreline distance.
                output.bankAcross = saturate(input.colour.g);
                // note: River vertices carry the receiving-body fade so a shared mouth eases toward lake motion instead of exposing an abrupt material-speed seam.
                output.flowFade = saturate(input.colour.b);
                // note: Area meshes default to full coverage; river meshes retain their authored receiving-body fade.
                // note: Foam meshes use zero alpha to suppress ordinary banks, so do not convert that sentinel back to full coverage.
                output.coverage = _FoamOnly > 0.5
                    ? saturate(input.colour.a)
                    : (input.colour.a > 0.0001 ? saturate(input.colour.a) : 1);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // note: Two moving samples retain the imported water normal while avoiding a sliding flat colour texture.
                // note: Local surface grade accelerates GPU-only flow on descents, bounded to prevent extreme rates and without per-frame mesh uploads.
                float grade = sqrt(saturate(1-input.normalWS.y*input.normalWS.y)) / max(.15, abs(input.normalWS.y));
                // note: Preserve slope acceleration while reducing only the inlet portion of a river as it merges into calmer receiving water.
                float speed = _FlowSpeed * lerp(.55, 1, input.flowFade) * lerp(1, 3.5, saturate(grade * 4)) ;
                // note: Sloping channel normals point downhill in XZ; advect ripples with that drainage direction instead of sending every river south.
                float2 downhill = input.normalWS.xz;
                float2 flowDirection = dot(downhill, downhill) > 0.000001 ? normalize(downhill) : float2(-0.22, 0.98);
                float2 drift = -flowDirection * speed * _Time.y;
                half3 a = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv + drift));
                half3 b = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv * 1.71 + drift * float2(-0.7,0.83)));
                // note: Both water and foam use actual opaque-surface depth; an open-water ribbon edge is not a bank collision.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float bedEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEyeDepth = -TransformWorldToView(input.positionWS).z;
                float thickness = max(0, bedEyeDepth - surfaceEyeDepth);
                if (_FoamOnly > 0.5)
                {
                    // note: Bank strips use the same moving normal samples as the water body, while vertex turbulence concentrates whitewash on bends and receiving-mouth impacts.
                    float foamNoise = saturate((a.x+b.y)*.5+.5);
                    half3 foamColour = lerp(half3(.68,.76,.73), half3(1,1,1), foamNoise);
                    float impact = saturate(input.turbulence);
                    // note: Break up the strip and require shallow contact, so banks receive irregular wash without white rails across deep water.
                    float contact = 1-smoothstep(.08,.65,thickness);
                    float patch = smoothstep(.38,.68,foamNoise);
                    float bankSide = min(input.bankAcross, 1-input.bankAcross) * 2;
                    float feather = smoothstep(0,.20,bankSide);
                    float foamAlpha = _FoamOpacity * contact * patch * feather * (.35 + impact*.65);
                    return half4(MixFog(foamColour,input.fog), foamAlpha * input.coverage);
                }
                float3 n = normalize(input.normalWS + float3(a.x+b.x,0,a.y+b.y) * _RippleStrength);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float absorption = 1 - exp(-thickness * 0.42);
                float fresnel = 0.025 + 0.975 * pow(1 - saturate(dot(n, view)), 5);
                half3 reflection = GlossyEnvironmentReflection(reflect(-view,n),0.13h,1.0h);
                Light sun = GetMainLight();
                float glint = pow(saturate(dot(n,normalize(sun.direction+view))),180) * 1.4;
                half3 colour = lerp(_ShallowColor.rgb,_DeepColor.rgb,absorption);
                colour = lerp(colour, reflection, fresnel * .85) + sun.color * glint;
                // note: A narrow depth-based wet edge replaces opaque bank strips and disappears at actual terrain contact.
                float edge = smoothstep(0.015,0.30,thickness);
                // note: Restore irregular foam at real shallow terrain contact, alongside whitewater on descents.
                float contactFoam = (1-smoothstep(.06,.75,thickness)) *
                    smoothstep(.25,.68,saturate((a.x+b.y)*.5+.5)) * _FoamOpacity;
                float flowImpact = saturate(input.turbulence) * saturate((1-input.normalWS.y)*3);
                float flowFoam = flowImpact * smoothstep(-.15,.45,a.x+b.y) * .45;
                colour = lerp(colour,half3(.88,.93,.91),max(flowFoam,contactFoam));
                // note: Keep accepted river and lake surfaces visible when the depth texture has no usable bed sample; depth controls absorption without adding a bright shoreline rail.
                float baseAlpha = saturate(.22 + absorption*.66 + fresnel*.35);
                // note: Feather only the last fraction of the accepted ribbon width so jagged terrain cells cannot form a hard transparent wedge at the bank.
                // note: Steep waterfall ribbons need an opaque edge floor so the carved bank cannot show through as pale triangular wedges; calm rivers and lakes still feather at their shoreline.
                float shoreFade = _FlowSpeed > .12
                    ? 1
                    : smoothstep(.02, .12, input.bankAcross) * smoothstep(.02, .12, 1-input.bankAcross);
                // note: Waterfalls need a stronger edge floor so their steep ribbon does not reveal carved bank triangles; river and lake surfaces retain the shallow transparency floor.
                // note: Keep the waterfall bank effectively opaque so the carved sidewall cannot show through the shallow edge blend.
                float edgeFloor = _FlowSpeed > .12 ? .94 : .24;
                float alpha = max(edge * baseAlpha, edgeFloor) * shoreFade;
                return half4(MixFog(colour,input.fog),alpha * input.coverage);
            }
            ENDHLSL
        }
    }
}
