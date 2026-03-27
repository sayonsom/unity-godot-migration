// =============================================================================
// room_wall.gdshader — Semi-transparent walls with height-based fade
// Mobile renderer compatible (GLSL ES 3.0)
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// --- Wall parameters (set from C#) ---
uniform vec4 wall_color : source_color = vec4(0.75, 0.7, 0.63, 1.0);
uniform float wall_opacity : hint_range(0.0, 1.0) = 0.6;
uniform float fade_height : hint_range(0.0, 1.0) = 0.7;

void fragment() {
    ALBEDO = wall_color.rgb;
    METALLIC = 0.0;
    ROUGHNESS = 0.9;

    // Height-based fade: solid at base, transparent at top
    // UV.y = 0 at bottom, 1 at top of wall
    float fade = smoothstep(fade_height, 1.0, UV.y);
    ALPHA = wall_opacity * (1.0 - fade);
}
