Shader "UI/SDFRoundedBox"
{
    Properties
    {
        _FillColor ("Fill Color", Color) = (1, 1, 1, 1)
        _StrokeColor ("Stroke Color", Color) = (0, 0, 0, 1)
        _StrokeWidth ("Stroke Width", Float) = 0
        _CornerRadius ("Corner Radius (TL, TR, BR, BL)", Vector) = (0, 0, 0, 0)
        _Size ("Size (Width, Height)", Vector) = (100, 100, 0, 0)
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.25)
        _ShadowOffset ("Shadow Offset (X, Y)", Vector) = (0, -4, 0, 0)
        _ShadowRadius ("Shadow Blur Radius", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _FillColor;
            float4 _StrokeColor;
            float _StrokeWidth;
            float4 _CornerRadius; // TL, TR, BR, BL
            float4 _Size;         // width, height
            float4 _ShadowColor;
            float4 _ShadowOffset; // x, y
            float _ShadowRadius;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // SDF for a rounded box with per-corner radii
            float sdRoundedBox(float2 p, float2 b, float4 r)
            {
                // Select corner radius based on quadrant
                r.xy = (p.x > 0.0) ? r.yz : r.xw; // right : left
                r.x  = (p.y > 0.0) ? r.x  : r.y;  // top : bottom
                
                float2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _Size.xy;
                
                // Map UV to centered coordinates in pixel space
                float2 p = (i.uv - 0.5) * size;
                float2 halfSize = size * 0.5;

                // Clamp corner radii to half of smallest dimension
                float maxR = min(halfSize.x, halfSize.y);
                float4 cr = min(_CornerRadius, maxR);

                // AA pixel width
                float aa = 1.0;

                // ===== SHADOW =====
                float4 color = float4(0, 0, 0, 0);

                if (_ShadowRadius > 0 && _ShadowColor.a > 0)
                {
                    float2 shadowP = p - _ShadowOffset.xy;
                    float shadowDist = sdRoundedBox(shadowP, halfSize, cr);
                    float shadowAlpha = 1.0 - smoothstep(-_ShadowRadius, _ShadowRadius, shadowDist);
                    color = float4(_ShadowColor.rgb, _ShadowColor.a * shadowAlpha);
                }

                // ===== FILL + STROKE =====
                float dist = sdRoundedBox(p, halfSize, cr);

                if (_StrokeWidth > 0)
                {
                    // Stroke region: between (edge - strokeWidth) and edge
                    float strokeOuter = smoothstep(aa, -aa, dist);
                    float strokeInner = smoothstep(aa, -aa, dist + _StrokeWidth);

                    // Fill is inside the stroke
                    float fillAlpha = strokeInner * _FillColor.a;
                    float strokeAlpha = (strokeOuter - strokeInner) * _StrokeColor.a;

                    // Composite: shadow → fill → stroke
                    float3 fillResult = lerp(color.rgb, _FillColor.rgb, fillAlpha);
                    float fillResultA = color.a * (1.0 - fillAlpha) + fillAlpha;

                    float3 finalRGB = lerp(fillResult, _StrokeColor.rgb, strokeAlpha);
                    float finalA = fillResultA * (1.0 - strokeAlpha) + strokeAlpha;

                    color = float4(finalRGB, finalA);
                }
                else
                {
                    // No stroke, just fill
                    float fillAlpha = smoothstep(aa, -aa, dist) * _FillColor.a;

                    float3 finalRGB = lerp(color.rgb, _FillColor.rgb, fillAlpha);
                    float finalA = color.a * (1.0 - fillAlpha) + fillAlpha;

                    color = float4(finalRGB, finalA);
                }

                return color;
            }
            ENDCG
        }
    }
}
