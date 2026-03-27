// =============================================================================
// minimap_render.gdshader — 2D minimap overlay with room outlines
// Used on a TextureRect displaying a SubViewport of the home from above
// =============================================================================

shader_type canvas_item;

// --- Minimap parameters ---
uniform vec4 background_color : source_color = vec4(0.15, 0.15, 0.2, 0.85);
uniform vec4 border_color : source_color = vec4(0.4, 0.4, 0.5, 1.0);
uniform float border_width : hint_range(0.0, 0.05) = 0.02;
uniform vec4 viewport_rect_color : source_color = vec4(0.13, 0.59, 0.95, 0.6);
uniform vec4 viewport_rect : hint_color = vec4(0.3, 0.3, 0.4, 0.4); // x, y, w, h in UV space

void fragment() {
    vec4 tex = texture(TEXTURE, UV);

    // Round corners
    vec2 center_uv = (UV - 0.5) * 2.0;
    float corner_dist = length(max(abs(center_uv) - vec2(0.9), 0.0));
    float mask = smoothstep(0.1, 0.08, corner_dist);

    // Border
    float edge_x = min(UV.x, 1.0 - UV.x);
    float edge_y = min(UV.y, 1.0 - UV.y);
    float edge = min(edge_x, edge_y);
    float border = smoothstep(border_width, border_width + 0.005, edge);

    // Camera viewport rectangle indicator
    float vp_left = step(viewport_rect.x, UV.x);
    float vp_right = step(UV.x, viewport_rect.x + viewport_rect.z);
    float vp_top = step(viewport_rect.y, UV.y);
    float vp_bottom = step(UV.y, viewport_rect.y + viewport_rect.w);
    float in_viewport = vp_left * vp_right * vp_top * vp_bottom;

    // Viewport rect border
    float vp_edge_x = min(abs(UV.x - viewport_rect.x), abs(UV.x - (viewport_rect.x + viewport_rect.z)));
    float vp_edge_y = min(abs(UV.y - viewport_rect.y), abs(UV.y - (viewport_rect.y + viewport_rect.w)));
    float vp_edge = min(vp_edge_x, vp_edge_y);
    float vp_border = (1.0 - smoothstep(0.005, 0.01, vp_edge)) * in_viewport;

    // Combine
    vec3 final_color = mix(background_color.rgb, tex.rgb, tex.a * 0.8);
    final_color = mix(final_color, viewport_rect_color.rgb, vp_border * 0.8);
    final_color = mix(final_color, border_color.rgb, 1.0 - border);

    COLOR = vec4(final_color, background_color.a * mask);
}
