Shader "Custom/UI/IrisWipe"
{
    Properties
    {
        _Color ("Overlay Color", Color) = (0,0,0,1)
        _Radius ("Hole Radius", Range(0, 1.5)) = 0
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.1
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Radius;
            float _Softness;
            float4 _Center;

            v2f vert (appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // UV 좌표에서 중심점(_Center)까지의 거리 계산
                // 화면 비율(Aspect Ratio) 보정 (원이 찌그러지지 않게)
                float2 uv = i.uv - _Center.xy;
                uv.x *= (_ScreenParams.x / _ScreenParams.y); 
                
                float dist = length(uv);
                
                // 구멍 뚫기 로직 (smoothstep으로 부드러운 경계선)
                float alpha = smoothstep(_Radius, _Radius + _Softness, dist);
                
                return fixed4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
}