// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

///////////////////////////////////////////
// author     : chen yong
// create time: 2015/8/5
// modify time: 
// description: Generate a depth texture from the projector
///////////////////////////////////////////

Shader "Kingsoft/CustomShadow/Caster" 
{
	SubShader {
		Tags { 			
		    "RenderType" = "Opaque"
		}

		CGINCLUDE
		#include "UnityCG.cginc"	
		struct v2f {
			float4 pos : SV_POSITION;
			float2 depth:TEXCOORD0;
		};

		uniform float _gShadowBias;
		v2f vert (appdata_full v)
		{
			v2f o;
			o.pos = UnityObjectToClipPos(v.vertex);
			o.pos.z += _gShadowBias;
			o.depth = o.pos.zw;
			return o;
		}
		
		fixed4 frag (v2f i) : COLOR
		{
			float depth = i.depth.x/i.depth.y;

		#if defined (SHADER_TARGET_GLSL) 
			depth = depth*0.5 + 0.5; //(-1, 1)-->(0, 1)
		#elif defined (UNITY_REVERSED_Z)
			depth = 1 - depth;       //(1, 0)-->(0, 1)
		#endif

			//return EncodeFloatRGBA(depth);
			return depth;
		}
		ENDCG 

		Pass {
			Fog { Mode Off }
			Cull front
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag	
			ENDCG
		}	
	}
}
