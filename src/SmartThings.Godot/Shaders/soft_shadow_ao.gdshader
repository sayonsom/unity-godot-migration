// =============================================================================
// soft_shadow_ao.gdshader — Vertex-color based ambient occlusion
// Lightweight fake AO for room corners and edges (Mobile renderer)
// AO values are baked into vertex colors at mesh generation time
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// --- AO parameters (set from C#) ---
uniform vec4 base_color : source_color = vec4(0.9, 0.85, 0.75, 1.0);
uniform float ao_intensity : hint_range(0.0, 1.0) = 0.3;

void fragment() {
    // Vertex COLOR.r channel carries the AO value (0 = fully occluded, 1 = no occlusion)
    float ao = mix(1.0, COLOR.r, ao_intensity);

    ALBEDO = base_color.rgb * ao;
    METALLIC = 0.0;
    ROUGHNESS = 0.85;
}
