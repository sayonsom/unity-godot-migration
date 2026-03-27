// =============================================================================
// room_floor.gdshader — Room floor with color tint, grid, and selection glow
// Mobile renderer compatible (GLSL ES 3.0)
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// --- Room parameters (set from C#) ---
uniform vec4 room_color : source_color = vec4(0.9, 0.85, 0.75, 1.0);
uniform bool is_selected = false;
uniform float grid_opacity : hint_range(0.0, 0.5) = 0.08;
uniform float edge_glow_width : hint_range(0.0, 0.2) = 0.05;
uniform vec4 selection_color : source_color = vec4(0.13, 0.59, 0.95, 1.0);

void fragment() {
    // Base room color
    ALBEDO = room_color.rgb;
    METALLIC = 0.0;
    ROUGHNESS = 0.85;

    // Subtle grid pattern (1m spacing)
    vec2 grid_uv = fract(UV * 10.0);
    float grid_line = step(0.97, grid_uv.x) + step(0.97, grid_uv.y);
    grid_line = clamp(grid_line, 0.0, 1.0);
    ALBEDO = mix(ALBEDO, ALBEDO * 0.7, grid_line * grid_opacity);

    // Edge glow when selected
    vec3 emission = vec3(0.0);
    if (is_selected) {
        // Edge detection using UV distance from border
        float edge_x = min(UV.x, 1.0 - UV.x);
        float edge_y = min(UV.y, 1.0 - UV.y);
        float edge = min(edge_x, edge_y);
        float glow = smoothstep(edge_glow_width, 0.0, edge);

        // Pulsing selection glow
        float pulse = sin(TIME * 2.0) * 0.2 + 0.8;
        emission = selection_color.rgb * glow * pulse * 1.5;

        // Slight tint on entire floor when selected
        ALBEDO = mix(ALBEDO, selection_color.rgb, 0.1);
    }

    EMISSION = emission;
}
