// =============================================================================
// energy_flow.gdshader — Animated Energy Flow Visualization
// For SmartThings energy monitoring overlays (heatmaps, flow arrows)
// =============================================================================

shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform sampler2D flow_texture : filter_linear_mipmap, repeat_enable;
uniform vec4 flow_color : source_color = vec4(0.0, 0.8, 1.0, 0.8);
uniform float flow_speed : hint_range(0.0, 5.0) = 1.0;
uniform float energy_level : hint_range(0.0, 1.0) = 0.5;
uniform float opacity : hint_range(0.0, 1.0) = 0.6;

// Heatmap gradient
uniform vec4 color_low : source_color = vec4(0.0, 0.0, 1.0, 1.0);   // Blue (cool)
uniform vec4 color_mid : source_color = vec4(0.0, 1.0, 0.0, 1.0);   // Green (normal)
uniform vec4 color_high : source_color = vec4(1.0, 0.0, 0.0, 1.0);  // Red (hot)

void fragment() {
    // Animated UV scroll for flow effect
    vec2 flow_uv = UV + vec2(TIME * flow_speed, 0.0);
    vec4 flow = texture(flow_texture, flow_uv);

    // Heatmap based on energy level
    vec4 heat_color;
    if (energy_level < 0.5) {
        heat_color = mix(color_low, color_mid, energy_level * 2.0);
    } else {
        heat_color = mix(color_mid, color_high, (energy_level - 0.5) * 2.0);
    }

    // Combine flow animation with heatmap
    ALBEDO = mix(flow_color.rgb, heat_color.rgb, 0.5) * flow.rgb;
    ALPHA = flow.a * opacity * energy_level;
}
