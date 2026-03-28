// =============================================================================
// room_wall.gdshader — Thick white opaque walls with 3D depth
// Matches SmartThings Map View style: solid white walls with subtle shading
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 wall_color : source_color = vec4(0.95, 0.95, 0.97, 1.0);
uniform float wall_opacity : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    ALBEDO = wall_color.rgb;
    METALLIC = 0.0;
    ROUGHNESS = 0.75;

    // Subtle top-edge highlight for 3D depth
    float top_highlight = smoothstep(0.85, 1.0, UV.y) * 0.15;
    ALBEDO += vec3(top_highlight);

    // Slight darkening at base for grounding
    float base_shade = smoothstep(0.15, 0.0, UV.y) * 0.1;
    ALBEDO -= vec3(base_shade);

    ALPHA = wall_opacity;
}
