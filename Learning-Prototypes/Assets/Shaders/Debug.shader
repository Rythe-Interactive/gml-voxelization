Shader "Custom/Debug"
{
	Properties
	{
		_LineColor("Line color", Color) = (1.0, 1.0, 1.0, 1.0)
		_LineWidth("Line width", Range(0.1, 10.0)) = 1.0
		_DrawFaces("Draw faces", Range(0, 1)) = 1
	}

		SubShader
	{
		Tags
		{
			"RenderPipeline" = "HDRenderPipeline"
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
		}
		//ZTest()

		pass
		{
			Name "Debug"

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom

			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float2 barycentricCoordinates : TEXCOORD9;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				half3 normal : TEXCOORD0;
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _LineColor;
			float _LineWidth;
			int _DrawFaces;
			CBUFFER_END

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				UNITY_TRANSFER_FOG(o, o.vertex);

				o.barycentricCoordinates = float2(0.0, 0.0);
				return o;
			}

			[maxvertexcount(3)]
			void geom(triangle v2f i[3], inout TriangleStream<v2f> stream)
			{
				float3 p0 = i[0].vertex.xyz;
				float3 p1 = i[1].vertex.xyz;
				float3 p2 = i[2].vertex.xyz;

				v2f g0, g1, g2;

				g0 = i[0];
				g1 = i[1];
				g2 = i[2];

				g0.barycentricCoordinates = float2(1, 0);
				g1.barycentricCoordinates = float2(0, 1);
				g2.barycentricCoordinates = float2(0, 0);

				stream.Append(g0);
				stream.Append(g1);
				stream.Append(g2);
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 barys;
				barys.xy = i.barycentricCoordinates;
				barys.z = 1 - barys.x - barys.y;

				float3 deltas = fwidth(barys);
				barys = smoothstep(0, _LineWidth * 100.0 * deltas, barys);
				float minBary = min(barys.x, min(barys.y, barys.z));

				fixed4 col;

				if (minBary / i.vertex.z > 0.1)
				{
					if (!_DrawFaces)
						discard;

					col = fixed4(normalize(i.normal + half3(0.5, 0.5, 0.5)), 1.0);
				}
				else
					col = fixed4(_LineColor.rgb, 1.0);

				UNITY_APPLY_FOG(i.fogCoord, col);

				return col;
			}
			ENDHLSL
		}
	}
}
