// =============================================================================
// room_floor.gdshader — Clean colored room floor (SmartThings style)
// Bright saturated color, subtle selection glow
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 room_color : source_color = vec4(0.56, 0.93, 0.56, 1.0);
uniform bool is_selected = false;
uniform vec4 selection_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
    ALBEDO = room_color.rgb;
    METALLIC = 0.0;
    ROUGHNESS = 0.8;

    vec3 emission = vec3(0.0);

    if (is_selected) {
        // Brighten entire floor when selected
        ALBEDO = mix(ALBEDO, vec3(1.0), 0.15);

        // Edge glow
        float edge_x = min(UV.x, 1.0 - UV.x);
        float edge_y = min(UV.y, 1.0 - UV.y);
        float edge = min(edge_x, edge_y);
        float glow = smoothstep(0.06, 0.0, edge);
        float pulse = sin(TIME * 2.5) * 0.15 + 0.85;
        emission = selection_color.rgb * glow * pulse * 0.8;
    }

    EMISSION = emission;
}
