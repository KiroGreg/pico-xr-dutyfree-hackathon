// Heatmap.shader
// Interactive floor heatmap for Gebr. Heinemann airport retail demo.
// Built-in Render Pipeline (Unity 2022.3 LTS). White glow aesthetic with
// animated flow streams between retail points.

Shader "Custom/Heatmap"
{
    Properties
    {
        // 1D gradient ramp: left = cold (transparent), right = hot (bright white).
        // Create a 256x1 px texture: transparent black -> dim blue-white -> pure white.
        // If left as default "white", the shader uses its own inline white-glow math.
        _GradientRamp  ("Gradient Ramp (1D)", 2D) = "white" {}

        // These are also set per-frame via MaterialPropertyBlock from HeatmapController:
        _NoiseStrength ("Noise Strength",  Range(0, 0.3))  = 0.05
        _NoiseScale    ("Noise Scale",     Float)          = 3.0
        _FlowWidth     ("Flow Width",      Range(0.05, 2)) = 0.4
        _FlowSpeed     ("Flow Speed",      Range(0.1, 5))  = 1.5
        _HeatmapTime   ("Heatmap Time",    Float)          = 0
    }

    SubShader
    {
        // Transparent overlay rendered above the floor geometry.
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            ZWrite Off
            // Standard alpha blend — floor texture shows through where heat = 0.
            Blend SrcAlpha OneMinusSrcAlpha
            // Cull Off so the heatmap is visible looking upward in MR passthrough.
            Cull Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            // Ensure loops are properly compiled on mobile GPUs (no dynamic branching).
            #pragma target 3.0
            #include "UnityCG.cginc"

            // ── Array uniforms ─────────────────────────────────────────────────
            // Array properties cannot be declared in the Properties block for
            // Built-in RP; they are set exclusively via MaterialPropertyBlock.

            uniform float4 _HeatmapPositions[6];   // xyz = world pos, w = radius
            uniform float  _HeatmapIntensities[6]; // per-point intensity [0..1]
            uniform int    _HeatmapCount;

            uniform float4 _FlowStarts[6]; // flow segment start (world pos in xyz)
            uniform float4 _FlowEnds[6];   // flow segment end   (world pos in xyz)
            uniform int    _FlowCount;

            // ── Scalar uniforms (also in Properties block for inspector editing) ──
            sampler2D _GradientRamp;
            float     _NoiseStrength;
            float     _NoiseScale;
            float     _FlowWidth;
            float     _FlowSpeed;
            float     _HeatmapTime;

            // ── Vertex / Fragment structs ──────────────────────────────────────

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            // ── Vertex shader ──────────────────────────────────────────────────

            v2f vert(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // ── Helpers ────────────────────────────────────────────────────────

            // Smooth value noise — avoids a texture lookup for the organic wobble.
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Distance from point p to line segment (a,b); also returns parametric t.
            float segmentDist(float3 p, float3 a, float3 b, out float t)
            {
                float3 ab   = b - a;
                float  len2 = dot(ab, ab);
                // Clamp to segment endpoints; add epsilon to avoid division by zero.
                t = saturate(dot(p - a, ab) / (len2 + 1e-6));
                return length(p - (a + ab * t));
            }

            // ── Fragment shader ────────────────────────────────────────────────

            fixed4 frag(v2f i) : SV_Target
            {
                float3 wp        = i.worldPos;
                float  totalHeat = 0.0;
                float  totalFlow = 0.0;

                // ── 1. Heat zones: two-layer approach ─────────────────────────
                //
                // Layer A — AMBIENT (additive, wide radius):
                //   Creates the warm yellowish "in-between" areas on the floor.
                //   Each department spreads softly at 4× its radius, very low intensity.
                //   Overlap is fine here — that's what makes the floor feel uniformly warm.
                //
                // Layer B — HOTSPOT (max blend, tight radius):
                //   Sharp concentrated glow right around each model.
                //   MAX blend prevents a bright accumulation blob where departments overlap.
                //
                // Final heat = hotspot + ambient * scale  (ambient fills the floor,
                // hotspot adds the sharp department markers on top).

                float hotSpot = 0.0;
                float ambient = 0.0;

                [unroll]
                for (int p = 0; p < 6; p++)
                {
                    float dist      = length(wp - _HeatmapPositions[p].xyz);
                    float radius    = _HeatmapPositions[p].w;
                    float intensity = _HeatmapIntensities[p];

                    // -- Layer A: subtle ambient halo (1.4× radius, ^3 falloff) --
                    // Provides a faint warm glow just outside the model footprint.
                    float ambientRadius  = radius * 1.4;
                    float ambientFalloff = 1.0 - smoothstep(0.0, ambientRadius, dist);
                    ambientFalloff       = ambientFalloff * ambientFalloff * ambientFalloff; // ^3
                    ambient             += ambientFalloff * intensity * 0.15;

                    // -- Layer B: concentrated hotspot (hard cutoff at 75 % of radius, ^5) --
                    // Falls to ~zero at half the radius — glow stays tight around the model.
                    float hotFalloff = 1.0 - smoothstep(0.0, radius * 0.75, dist);
                    hotFalloff       = hotFalloff * hotFalloff * hotFalloff * hotFalloff * hotFalloff; // ^5
                    hotSpot          = max(hotSpot, hotFalloff * intensity);
                }

                // Blend: faint ambient halo around models, hotspot dominates.
                totalHeat = saturate(ambient) * 0.35 + hotSpot;

                // ── 2. Flow streams: animated directional lines between points ──

                [unroll]
                for (int f = 0; f < 6; f++)
                {
                    if (f >= _FlowCount) break;

                    float  t;
                    float  dist = segmentDist(wp, _FlowStarts[f].xyz, _FlowEnds[f].xyz, t);

                    // Cross-line falloff (narrow band along the segment).
                    float crossFade = 1.0 - smoothstep(0.0, _FlowWidth, dist);
                    crossFade       = crossFade * crossFade;

                    // Travelling pulse: bright spots move from start to end over time.
                    // frac(t - speed*time) shifts [0,1] parameter forward continuously.
                    float pulse = frac(t - _HeatmapTime * _FlowSpeed * 0.25);
                    // Triangle wave → two bright peaks per cycle for a dashed-stream look.
                    pulse = abs(pulse * 2.0 - 1.0);
                    pulse = smoothstep(0.55, 1.0, pulse);

                    totalFlow += crossFade * (0.35 + pulse * 0.65);
                }

                // ── 3. Organic noise — makes stationary zones feel alive ───────

                float noise = valueNoise(wp.xz * _NoiseScale + _HeatmapTime * 0.4);
                // Only add noise where there is already heat; avoids noise on empty floor.
                totalHeat  += noise * _NoiseStrength * saturate(totalHeat * 3.0);

                // ── 4. Combine and clamp ───────────────────────────────────────

                float heatClamped = saturate(totalHeat);
                float flowClamped = saturate(totalFlow * 0.6);
                float combined    = saturate(heatClamped + flowClamped);

                // ── 5. Colorisation ───────────────────────────────────────────
                // Floor stays white (transparent overlay = white floor shows through).
                // Heat zones: thermal ramp  blue → cyan → yellow → red/orange.
                // Flow streams: bright cyan/electric-blue animated pulse on top.

                // -- Thermal ramp for heat zones (4-stop gradient via lerp chain) --
                // 0.0 → deep blue  |  0.33 → cyan  |  0.66 → yellow  |  1.0 → red
                float3 col_blue   = float3(0.05, 0.10, 0.80);
                float3 col_cyan   = float3(0.00, 0.85, 0.90);
                float3 col_yellow = float3(1.00, 0.90, 0.05);
                float3 col_red    = float3(1.00, 0.15, 0.05);

                float3 heatColor;
                if (heatClamped < 0.33)
                    heatColor = lerp(col_blue,   col_cyan,   heatClamped / 0.33);
                else if (heatClamped < 0.66)
                    heatColor = lerp(col_cyan,   col_yellow, (heatClamped - 0.33) / 0.33);
                else
                    heatColor = lerp(col_yellow, col_red,    (heatClamped - 0.66) / 0.34);

                // -- Flow stream color: electric cyan-white pulse --
                // Pulse brightness modulates between a dim teal base and bright white-cyan.
                float  pulseBright = saturate(totalFlow * 0.6);           // 0..1
                float3 flowBase    = float3(0.00, 0.70, 1.00);            // electric cyan
                float3 flowHot     = float3(0.80, 1.00, 1.00);            // near-white on peak
                float3 flowColor   = lerp(flowBase, flowHot, pulseBright);

                // -- Blend heat and flow; flow wins in its own lanes --
                float  flowDominance = saturate(flowClamped - heatClamped * 0.4);
                float3 finalColor    = lerp(heatColor, flowColor, flowDominance);

                // -- Alpha: transparent where nothing is active (white floor shows through) --
                float alpha = saturate(combined * combined * 1.8);

                return float4(finalColor, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
