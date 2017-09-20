// SpriteParticleShader.shader - READ ME
// Primary Functionality:
//      - To plot the particles to the screen in 3D space
//		- To colour the particles based on their [u] values (i.e number of neighbours)
//
// Assignment Object: NBodyPlotter.cs within "Main Camera"
// 
// Notes: 
//      This is an edited version of the Particle Shader written by "jakedowns" for the GPU GEMS code
//		See here: https://github.com/jakedowns/GPU_GEMS_OVR
//		I have added colour based on [u] values
//		I'm pretty weak on shader language (CG/HLSL) so comments are scarce

Shader "NBodySim/SpriteParticleShader" 
{

	Properties		// editable in the material inspector
	{
		_Sprite("Sprite", 2D) = "white" {}
		_Size("Size", float) = 1.0
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
		#pragma geometry geom
		#pragma fragment frag

		#include "UnityCG.cginc"

		StructuredBuffer<float4> _Positions;		// 4 vector array of postion and mass data
		StructuredBuffer<float4> _Velocities;		// 4 vector array of velocity and [u] data
		float _MinNeighbours;						// smallest [u] value
		float _RangeNeighbours;						// range of [u] values

		// declare variables that were written in Properties{}
		float _Size;								
		sampler2D _Sprite;	

		// matricies for position space conversions
		uniform float4x4 _OldWorldMatrix;			
		uniform float4x4 _WorldMatrix;

		// vertex to geometry struct
		struct v2g 
		{
			float4 pos : SV_POSITION;	
			int id : TEXCOORD0;			
		};

		// vertex to geometry function
		v2g vert(uint id : SV_VertexID)
		{
			v2g OUT;
			float3 worldPos = _Positions[id].xyz;			
			OUT.pos = mul (UNITY_MATRIX_VP, float4(worldPos,1.0f));	// world pos to screen pos conversion
			OUT.id = id;
			return OUT;
		}
		
		// gemoetry to fragment stuct
		struct g2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		int id : TEXCOORD1;
		};

		// gemoetry to fragment function
		[maxvertexcount(4)]
		void geom(point v2g IN[1], inout TriangleStream<g2f> outStream)
		{
			// Size based on mass
			// float dx = _Size * _Positions[IN[0].id][3];
			// float dy = _Size * _Positions[IN[0].id][3] * _ScreenParams.x / _ScreenParams.y;
			
			// Uniform size
			float dx = _Size;
			float dy = _Size * _ScreenParams.x / _ScreenParams.y;
			g2f OUT;
			OUT.id = IN[0].id;
			OUT.pos = IN[0].pos + float4(-dx, dy,0,0); OUT.uv=float2(0,0); outStream.Append(OUT);
			OUT.pos = IN[0].pos + float4( dx, dy,0,0); OUT.uv=float2(1,0); outStream.Append(OUT);
			OUT.pos = IN[0].pos + float4(-dx,-dy,0,0); OUT.uv=float2(0,1); outStream.Append(OUT);
			OUT.pos = IN[0].pos + float4( dx,-dy,0,0); OUT.uv=float2(1,1); outStream.Append(OUT);
			outStream.RestartStrip();
		}

		// fragment fucntion
		float4 frag (g2f IN) : COLOR
		{
			fixed4 col;
			float4 vel = _Velocities[IN.id];
			float ColourScale = (vel.w - _MinNeighbours) / _RangeNeighbours;	
			ColourScale = clamp(ColourScale, 0, 1);
			// functions of ColourScale below are chosen in an attempt to reproduce a nice transition between red (low) and cyan (high).
			// some options are available

			/*
			// smooth									red/yellow/lime/cyan
			col.r = 0.627f - (0.471f * ColourScale);
			col.g = pow(ColourScale, 0.5f);
			col.b = pow(ColourScale, 3);
			*/

			// higher contrast (default)				red/yellow/lime/cyan
			col.r = 0.627f - (0.471f * ColourScale);
			col.g = pow(ColourScale, 0.5f);
			col.b = 0.3*pow(ColourScale, 2) + 0.7*pow(ColourScale, 20);

			/*
			// high contrast							red/orange/white/yellow/green/cyan
			col.r = 0.627f - (0.471f * ColourScale);
			col.g = pow(ColourScale, 0.5f);
			col.b = (-0.5*cos(3 * 3.14159f*ColourScale) + 0.5f) * ColourScale;
			*/

			col.a = 0.8f;								// gamma i.e. transparency
			return tex2D(_Sprite, IN.uv) * col;
		}

		ENDCG

		}
	}

Fallback Off
}















