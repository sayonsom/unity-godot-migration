// =============================================================================
// HLSL to GDShader Migration Cheatsheet
// Samsung SmartThings — Unity to Godot Shader Conversion Reference
//
// GDShader uses GLSL ES 3.0 syntax. No automated HLSL->GDShader converter exists.
// Each shader must be manually rewritten using this conceptual mapping.
// =============================================================================

// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  1. SHADER DECLARATION                                                   ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Unity:  Shader "Custom/MyShader" { Properties { } SubShader { } }     ║
// ║  Godot:  shader_type spatial;                                           ║
// ╚══════════════════════════════════════════════════════════════════════════╝

shader_type spatial;

// Render modes replace Unity's Tags and render queue settings
// Unity: Tags { "RenderType"="Transparent" "Queue"="Transparent" }
// Godot:
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// Other common render modes:
//   unshaded              — No lighting (Unity: Unlit shader)
//   blend_add             — Additive blending
//   blend_sub             — Subtractive blending
//   depth_draw_always     — Always write depth
//   depth_draw_never      — Never write depth (for transparent)
//   cull_disabled         — No backface culling (Unity: Cull Off)
//   skip_vertex_transform — Manual vertex transform (advanced)
//   world_vertex_coords   — Vertex positions in world space


// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  2. PROPERTIES / UNIFORMS                                               ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Unity:  [SerializeField] float _Speed;  // In Properties block         ║
// ║          or: float _Speed;  // In HLSL                                  ║
// ║  Godot:  uniform float speed : hint_range(0.0, 10.0) = 1.0;           ║
// ╚══════════════════════════════════════════════════════════════════════════╝

// Scalar types
uniform float speed : hint_range(0.0, 10.0, 0.1) = 1.0;
uniform int iterations : hint_range(1, 100) = 10;
uniform bool use_effect = false;

// Colors
// Unity: Color _Color = (1,1,1,1)
uniform vec4 albedo_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 emission_color : source_color = vec4(0.0);

// Textures
// Unity: sampler2D _MainTex
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_map : hint_normal;
uniform sampler2D roughness_map : hint_roughness_r;  // Channel hint

// Hint reference (common ones):
//   source_color        — sRGB color texture (Unity: [HDR])
//   hint_normal         — Normal map
//   hint_roughness_r    — Roughness in R channel
//   hint_roughness_g    — Roughness in G channel
//   hint_default_white  — Default white texture
//   hint_default_black  — Default black texture
//   hint_anisotropy     — Anisotropy map
//   hint_range(min,max) — Slider in inspector

// Set from C#: ShaderMaterial.SetShaderParameter("albedo_color", new Color(1,0,0,1));


// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  3. TYPE MAPPING                                                        ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Unity HLSL          →  Godot GDShader (GLSL)                          ║
// ║  float               →  float                                          ║
// ║  float2 / half2      →  vec2                                           ║
// ║  float3 / half3      →  vec3                                           ║
// ║  float4 / half4      →  vec4                                           ║
// ║  fixed4              →  vec4 (lowp qualifier optional)                  ║
// ║  float4x4            →  mat4                                           ║
// ║  float3x3            →  mat3                                           ║
// ║  sampler2D           →  sampler2D                                      ║
// ║  samplerCUBE         →  samplerCube                                    ║
// ║  int                 →  int                                            ║
// ║  bool                →  bool                                           ║
// ╚══════════════════════════════════════════════════════════════════════════╝


// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  4. VERTEX SHADER                                                       ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Unity:  v2f vert(appdata v) { ... return o; }                         ║
// ║  Godot:  void vertex() { ... }                                         ║
// ╚══════════════════════════════════════════════════════════════════════════╝

// Built-in vertex inputs (no struct needed, unlike Unity's appdata):
//   VERTEX        — vec3 local position   (Unity: v.vertex.xyz)
//   NORMAL        — vec3 local normal     (Unity: v.normal)
//   TANGENT       — vec3 local tangent    (Unity: v.tangent.xyz)
//   UV            — vec2 texcoord 0       (Unity: v.texcoord)
//   UV2           — vec2 texcoord 1       (Unity: v.texcoord1)
//   COLOR         — vec4 vertex color     (Unity: v.color)
//   INSTANCE_ID   — int instance id

// Built-in vertex outputs (write to these, no struct needed):
//   VERTEX        — transformed position
//   NORMAL        — transformed normal
//   POSITION      — clip space position (advanced, usually automatic)

// Built-in matrices (Unity equivalents):
//   MODEL_MATRIX          — unity_ObjectToWorld
//   VIEW_MATRIX           — UNITY_MATRIX_V
//   PROJECTION_MATRIX     — UNITY_MATRIX_P
//   MODELVIEW_MATRIX      — UNITY_MATRIX_MV
//   INV_VIEW_MATRIX       — UNITY_MATRIX_I_V

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
    // Example: Wave animation (common IoT energy visualization)
    // Unity:  v.vertex.y += sin(v.vertex.x * 2.0 + _Time.y * _Speed) * 0.1;
    VERTEX.y += sin(VERTEX.x * 2.0 + TIME * speed) * 0.1;

    // Pass world-space data to fragment (Unity: o.worldPos = mul(unity_ObjectToWorld, v.vertex))
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = (MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz;
}


// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  5. FRAGMENT SHADER                                                     ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Unity:  fixed4 frag(v2f i) : SV_Target { return color; }             ║
// ║  Godot:  void fragment() { ALBEDO = color.rgb; }                       ║
// ╚══════════════════════════════════════════════════════════════════════════╝

// Built-in fragment inputs:
//   UV              — interpolated texcoord
//   VERTEX          — view-space position
//   FRAGCOORD       — screen pixel coords (Unity: i.screenPos)
//   NORMAL          — interpolated normal (view space)
//   TANGENT         — interpolated tangent
//   BINORMAL        — interpolated binormal
//   VIEW            — view direction

// Built-in PBR outputs (replace return value):
//   ALBEDO          — vec3, base color         (Unity: o.Albedo)
//   ALPHA           — float, transparency      (Unity: o.Alpha)
//   METALLIC        — float, 0.0-1.0           (Unity: o.Metallic)
//   ROUGHNESS       — float, 0.0-1.0           (Unity: 1.0 - o.Smoothness)
//   SPECULAR        — float, specular strength (Unity: o.Specular)
//   NORMAL_MAP      — vec3, tangent normal     (Unity: o.Normal)
//   EMISSION        — vec3, emission color     (Unity: o.Emission)
//   AO              — float, ambient occlusion (Unity: o.Occlusion)
//   RIM             — float, rim lighting
//   RIM_TINT        — float, rim tint

void fragment() {
    // === SmartThings device state visualization example ===
    // Change color based on IoT device state (set from C#)

    // Sample base texture
    vec4 tex = texture(albedo_texture, UV);
    ALBEDO = tex.rgb * albedo_color.rgb;
    ALPHA = tex.a * albedo_color.a;

    // Normal mapping
    // Unity: o.Normal = UnpackNormal(tex2D(_BumpMap, i.uv));
    NORMAL_MAP = texture(normal_map, UV).rgb;

    // Roughness (NOTE: Unity uses Smoothness = 1.0 - Roughness)
    ROUGHNESS = texture(roughness_map, UV).r;

    // Emission for device state glow
    // Unity: o.Emission = _EmissionColor.rgb * _EmissionStrength;
    EMISSION = emission_color.rgb;

    // Metallic
    METALLIC = 0.0;

    // Fresnel / rim effect (common for device highlighting)
    // Unity: float rim = 1.0 - saturate(dot(normalize(i.viewDir), i.normal));
    float fresnel = pow(1.0 - dot(NORMAL, VIEW), 3.0);
    RIM = fresnel * 0.5;
    RIM_TINT = 0.5;
}


// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  6. COMMON FUNCTION MAPPING                                             ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Unity HLSL            →  Godot GDShader (GLSL)                        ║
// ║  saturate(x)           →  clamp(x, 0.0, 1.0)                          ║
// ║  lerp(a, b, t)         →  mix(a, b, t)                                ║
// ║  tex2D(tex, uv)        →  texture(tex, uv)                            ║
// ║  mul(M, v)             →  M * v                                        ║
// ║  frac(x)               →  fract(x)                                    ║
// ║  clip(x)               →  discard (in if statement)                    ║
// ║  _Time.y               →  TIME                                        ║
// ║  _SinTime              →  sin(TIME)                                    ║
// ║  _ScreenParams         →  1.0/VIEWPORT_SIZE (or VIEWPORT_SIZE)        ║
// ║  UNITY_MATRIX_MVP      →  PROJECTION_MATRIX * MODELVIEW_MATRIX        ║
// ║  UnpackNormal()        →  direct sample (Godot auto-unpacks)          ║
// ║  SAMPLE_TEXTURE2D      →  texture()                                   ║
// ║  ddx() / ddy()         →  dFdx() / dFdy()                            ║
// ║  atan2(y, x)           →  atan(y, x)                                  ║
// ╚══════════════════════════════════════════════════════════════════════════╝


// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  7. IoT-SPECIFIC SHADER PATTERNS                                        ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Common SmartThings visualization shaders to migrate:                   ║
// ║  - Device state glow (on/off/error states via emission)                ║
// ║  - Temperature heatmap overlay                                         ║
// ║  - Energy flow arrows (animated UV scroll)                             ║
// ║  - Connection status pulse (ping animation)                            ║
// ╚══════════════════════════════════════════════════════════════════════════╝

// C# usage example for device state:
// -----------------------------------
// var material = (ShaderMaterial)meshInstance.GetActiveMaterial(0);
// material.SetShaderParameter("albedo_color", deviceState switch {
//     "on"    => new Color(0.2f, 0.8f, 0.2f),  // Green
//     "off"   => new Color(0.5f, 0.5f, 0.5f),  // Gray
//     "error" => new Color(0.9f, 0.1f, 0.1f),  // Red
//     _       => Colors.White
// });
// material.SetShaderParameter("emission_color", isActive ? new Color(0, 0.5f, 1, 1) : Colors.Black);
