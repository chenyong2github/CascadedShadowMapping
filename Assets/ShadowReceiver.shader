// Upgrade NOTE: replaced '_World2Shadow' with 'unity_WorldToShadow[0]'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'
// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

///////////////////////////////////////////
// author     : chen yong
// create time: 2017/7/5
// modify time: 
// description: 
///////////////////////////////////////////

Shader "Kingsoft/CustomShadow/Receiver" {

	SubShader {
		Tags { "RenderType"="Opaque"  }

		LOD 300 

		Pass {
			Name "FORWARD"
			Tags{ "LightMode" = "ForwardBase" }

			CGPROGRAM
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 shadowCoord : TEXCOORD1;
				float eyeZ : TEXCOORD2;
				float4 worldPos : TEXCOORD3;
			};

			uniform float4x4 _gWorldToShadow;
			uniform sampler2D _gShadowMapTexture;
			uniform float4 _gShadowMapTexture_TexelSize;

			uniform float4 _gLightSplitsNear;
			uniform float4 _gLightSplitsFar;
			uniform float4x4 _gWorld2Shadow[4];
			
			uniform sampler2D _gShadowMapTexture0;
			uniform sampler2D _gShadowMapTexture1;
			uniform sampler2D _gShadowMapTexture2;
			uniform sampler2D _gShadowMapTexture3;

			uniform float _gShadowStrength;

			v2f vert (appdata_full v) 
			{
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.uv = v.texcoord.xy;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.shadowCoord = mul(_gWorldToShadow, o.worldPos);
				o.eyeZ = o.pos.w;
				return o; 
			}

			float PCFSample(float depth, float2 uv)
			{
				float shadow = 0.0;
				for (int x = -1; x <= 1; ++x)
				{
					for (int y = -1; y <= 1; ++y)
					{
						float4 col = tex2D(_gShadowMapTexture, uv + float2(x, y) * _gShadowMapTexture_TexelSize.xy);
						float sampleDepth = DecodeFloatRGBA(col);
						shadow += sampleDepth < depth ? _gShadowStrength : 1;
					}
				}
				return shadow /= 9;
			}

			fixed4 getCascadeWeights(float z)
			{
				fixed4 zNear = float4(z >= _gLightSplitsNear);
				fixed4 zFar = float4(z < _gLightSplitsFar);
				fixed4 weights = zNear * zFar;
				return weights;
			}

			float4 getShadowCoord(float4 wpos, fixed4 cascadeWeights)
			{
				float3 sc0 = mul(_gWorld2Shadow[0], wpos).xyz;
				float3 sc1 = mul(_gWorld2Shadow[1], wpos).xyz;
				float3 sc2 = mul(_gWorld2Shadow[2], wpos).xyz;
				float3 sc3 = mul(_gWorld2Shadow[3], wpos).xyz;
				return float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);
			}

			float4 SampleShadowTexture(float4 wPos, fixed4 cascadeWeights)
			{
				float4 shadowCoord0 = mul(_gWorld2Shadow[0], wPos);
				float4 shadowCoord1 = mul(_gWorld2Shadow[1], wPos);
				float4 shadowCoord2 = mul(_gWorld2Shadow[2], wPos);
				float4 shadowCoord3 = mul(_gWorld2Shadow[3], wPos);

				shadowCoord0.xy /= shadowCoord0.w;
				shadowCoord1.xy /= shadowCoord1.w;
				shadowCoord2.xy /= shadowCoord2.w;
				shadowCoord3.xy /= shadowCoord3.w;

				shadowCoord0.xy = shadowCoord0.xy*0.5 + 0.5;
				shadowCoord1.xy = shadowCoord1.xy*0.5 + 0.5;
				shadowCoord2.xy = shadowCoord2.xy*0.5 + 0.5;
				shadowCoord3.xy = shadowCoord3.xy*0.5 + 0.5;

				float4 sampleDepth0 = tex2D(_gShadowMapTexture0, shadowCoord0.xy);
				float4 sampleDepth1 = tex2D(_gShadowMapTexture1, shadowCoord1.xy);
				float4 sampleDepth2 = tex2D(_gShadowMapTexture2, shadowCoord2.xy);
				float4 sampleDepth3 = tex2D(_gShadowMapTexture3, shadowCoord3.xy);

				float depth0 = shadowCoord0.z / shadowCoord0.w;
				float depth1 = shadowCoord1.z / shadowCoord1.w;
				float depth2 = shadowCoord2.z / shadowCoord2.w;
				float depth3 = shadowCoord3.z / shadowCoord3.w;

				#if defined (SHADER_TARGET_GLSL)
					depth0 = depth0*0.5 + 0.5; //(-1, 1)-->(0, 1)
					depth1 = depth1*0.5 + 0.5;
					depth2 = depth2*0.5 + 0.5;
					depth3 = depth3*0.5 + 0.5;
				#elif defined (UNITY_REVERSED_Z)
					depth0 = 1 - depth0;       //(1, 0)-->(0, 1)
					depth1 = 1 - depth1;
					depth2 = 1 - depth2;
					depth3 = 1 - depth3;
				#endif

				float shadow0 = sampleDepth0 < depth0 ? _gShadowStrength : 1;
				float shadow1 = sampleDepth1 < depth1 ? _gShadowStrength : 1;
				float shadow2 = sampleDepth2 < depth2 ? _gShadowStrength : 1;
				float shadow3 = sampleDepth3 < depth3 ? _gShadowStrength : 1;

				//return col0;
				float shadow = shadow0*cascadeWeights[0] + shadow1*cascadeWeights[1] + shadow2*cascadeWeights[2] + shadow3*cascadeWeights[3];
				return shadow*cascadeWeights;
			}

			fixed4 frag (v2f i) : COLOR0 
			{			
				//return float4(i.eyeZ < _gLightSplitsFar);

				fixed4 weights = getCascadeWeights(i.eyeZ);
				fixed4 coord = getShadowCoord(i.worldPos, weights);

			

				//return i.shadowCoord;
				// shadow
				i.shadowCoord.xy = i.shadowCoord.xy/i.shadowCoord.w;
				float2 uv = i.shadowCoord.xy;
				uv = uv*0.5 + 0.5; //(-1, 1)-->(0, 1)
	
				float depth = i.shadowCoord.z / i.shadowCoord.w;

			#if defined (SHADER_TARGET_GLSL)
				depth = depth*0.5 + 0.5; //(-1, 1)-->(0, 1)
			#elif defined (UNITY_REVERSED_Z)
				depth = 1 - depth;       //(1, 0)-->(0, 1)
			#endif

				// sample depth texture
				float4 col = SampleShadowTexture(i.worldPos, weights);
				return col;

				//return PCFSample(depth, uv);
			}	
		
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest 	
			ENDCG
		}
	}
}
