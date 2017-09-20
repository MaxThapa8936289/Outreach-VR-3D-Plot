
Shader "NBodySim/ParticleShader" 
{

	Properties
	{
		_Color("Color", Color) = (0.38,0.26,0.98,1.0)
	}
	SubShader 
	{
		//uses DrawProcedural so tag doesnt really matter
		Tags { "Queue" = "Transparent" }
	
		Pass 
		{
		
		ZWrite Off 
		ZTest Always 
		Cull Off 
		Fog { Mode Off }
    	Blend one one

		CGPROGRAM
		#pragma target 5.0

		#pragma vertex vert
		#pragma fragment frag

		#include "UnityCG.cginc"

		StructuredBuffer<float4> _Positions;
		StructuredBuffer<float4> _Velocities;
		float _MaxSpeed;
		fixed4 _Color;
		fixed Red;
		fixed Green;
		fixed Blue;

		struct v2f 
		{
			float4 pos : SV_POSITION;
			int id : TEXCOORD0;
		};

		v2f vert(uint id : SV_VertexID)
		{
			v2f OUT;
			float3 worldPos = _Positions[id].xyz;
			OUT.pos = mul (UNITY_MATRIX_VP, float4(worldPos,1.0f));
			OUT.id = id;
			return OUT;
		}

		float4 frag (v2f IN) : COLOR
		{
			fixed4 col; 
			float3 vel = _Velocities[IN.id];
			float speedScale = dot(vel, vel) / _MaxSpeed;
			speedScale = clamp(speedScale, 0, 1);
			col.r = 0.627f - (0.471f * speedScale);
			col.g = pow(speedScale, 0.5f);
			col.b = pow(speedScale, 4);
			col.a = 1.0f;
			return col;
			//return _Color;
		}

		ENDCG

		}
	}

Fallback Off
}
