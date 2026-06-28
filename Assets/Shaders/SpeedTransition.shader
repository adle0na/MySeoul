Shader "Custom/SpeedTransition"
{
    Properties
    {
        _MainTex     ("MainTex",      2D)            = "white" {}
        _Progress    ("Progress",    Range(0,1))    = 0
        _Intensity   ("Intensity",   Range(0,5))    = 1.5
        _Speed       ("Speed",       Range(0,20))   = 8.0
        _Color       ("Color",       Color)         = (0.05, 0.05, 0.08, 1)
        _Direction   ("Direction",   Float)         = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalRenderPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float  _Progress;
            float  _Intensity;
            float  _Speed;
            float4 _Color;
            float  _Direction;

            // ── 유사난수 ────────────────────────────────────────
            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(127.1, 311.7))) * 43758.5453);
            }

            // ── 블록 글리치 노이즈 ──────────────────────────────
            float blockNoise(float2 uv, float t)
            {
                float2 block = floor(uv * float2(40.0, 20.0) + t * float2(3.7, 1.9));
                return rand(block + t * 0.1);
            }

            // ── 수평 속도선 ─────────────────────────────────────
            float speedLine(float2 uv, float t)
            {
                float y     = frac(uv.y * 60.0 + rand(float2(floor(uv.y * 60.0), 0.0)) * 10.0);
                float thick = rand(float2(floor(uv.y * 60.0), t)) * 0.55 + 0.05;
                return step(y, thick);
            }

            // ── 대각선 스크래치 ─────────────────────────────────
            float scratch(float2 uv, float t)
            {
                float s = rand(float2(floor(t * 7.3), 0.0));
                float l = uv.x + uv.y * 0.4 + s;
                float w = 0.003 + rand(float2(s, 1.0)) * 0.012;
                return smoothstep(w, 0.0, abs(frac(l * 8.0) - 0.5));
            }

            // ── 스캔라인 ─────────────────────────────────────────
            float scanline(float2 uv)
            {
                return 0.85 + 0.15 * sin(uv.y * 400.0);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Progress=0이면 즉시 discard (완전 투명)
                if (_Progress <= 0.0) discard;

                float2 uv = IN.uv;
                float  t  = _Speed * 0.1;   // 타임 오프셋 (Progress 기반이라 _Time 불필요)

                // ── 1) 와이프 경계 계산 ─────────────────────────
                // 우→좌 스윕. _Direction=1 이면 우→좌
                float xCoord = (_Direction > 0.0) ? uv.x : (1.0 - uv.x);

                // 속도선 노이즈로 경계선을 들쭉날쭉하게
                float noise  = blockNoise(uv, t)  * 0.06
                             + speedLine(uv, t)   * 0.04
                             + scratch(uv, t)     * 0.03;

                // Progress가 진행될수록 덮이는 영역 확장
                // edge = 0이면 완전 노출, 1이면 완전 덮음
                float edge   = _Progress + noise * _Intensity * 0.2;
                // Progress 0→1: 우→좌 덮기 / Progress 1→0: 역재생(걷히기)
                float mask   = smoothstep(edge - 0.08, edge + 0.04, xCoord);

                if (mask < 0.001) discard;

                // ── 2) 내부 이펙트 ──────────────────────────────
                float bn  = blockNoise(uv, t + _Progress * 3.0);
                float sp  = speedLine(uv, t + _Progress * 2.0);
                float sc  = scratch(uv, t);
                float sl  = scanline(uv);

                // 경계 근처 에너지 플래시
                float edgeDist  = saturate(1.0 - abs(xCoord - _Progress) * 15.0);
                float flash     = edgeDist * _Intensity;

                // 기본 색상
                float3 col = _Color.rgb;

                // 속도선 밝은 줄기
                col += sp  * float3(0.4, 0.5, 0.7) * _Intensity * 0.6;
                // 블록 노이즈 변조
                col += (bn - 0.5) * float3(0.15, 0.1, 0.25) * _Intensity * 0.4;
                // 스크래치 흰 선
                col += sc  * float3(0.9, 0.95, 1.0) * _Intensity * 0.8;
                // 에너지 플래시 (경계 빛)
                col += flash * float3(0.6, 0.8, 1.0);
                // 스캔라인 미세 명암
                col *= sl;

                // 알파: 경계 근처 반투명, 내부 불투명
                float alpha = mask * saturate(_Color.a + flash * 0.5);

                return half4(saturate(col), alpha);
            }
            ENDHLSL
        }
    }
}
