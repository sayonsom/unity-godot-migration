// =============================================================================
// device_pin.gdshader — Billboard device marker with status ring and pulse
// Mobile renderer compatible (GLSL ES 3.0)
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded;

// Billboard: always face camera
render_mode vertex_lighting;

// --- Pin parameters (set from C#) ---
uniform vec4 status_color : source_color = vec4(0.3, 0.69, 0.31, 1.0);
uniform vec4 icon_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float pulse_speed : hint_range(0.0, 5.0) = 2.0;
uniform bool is_active = false;
uniform float ring_width : hint_range(0.02, 0.15) = 0.08;

void vertex() {
    // Billboard transform: make quad always face camera
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        vec4(normalize(cross(vec3(0.0, 1.0, 0.0), MODELVIEW_MATRIX[2].xyz)), 0.0),
        vec4(0.0, 1.0, 0.0, 0.0),
        vec4(normalize(cross(MODELVIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0),
        MODELVIEW_MATRIX[3]
    );
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
        vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
        vec4(0.0, 0.0, 0.0, 1.0)
    );
}

void fragment() {
    // Centered UV (-1 to 1)
    vec2 center_uv = (UV - 0.5) * 2.0;
    float dist = length(center_uv);

    // Circular pin body
    float circle = smoothstep(0.42, 0.4, dist);

    // Status ring around the circle
    float ring_outer = smoothstep(0.5 + ring_width, 0.5 + ring_width - 0.02, dist);
    float ring_inner = smoothstep(0.5, 0.5 - 0.02, dist);
    float ring = ring_outer * (1.0 - ring_inner);

    // Pulse animation for active devices
    float pulse = 1.0;
    if (is_active) {
        pulse = sin(TIME * pulse_speed) * 0.15 + 0.85;
        // Expanding ring effect
        float expand = fract(TIME * pulse_speed * 0.3);
        float expand_ring = smoothstep(0.5 + expand * 0.5, 0.5 + expand * 0.5 + 0.02, dist)
                          * smoothstep(0.5 + expand * 0.5 + 0.06, 0.5 + expand * 0.5 + 0.04, dist);
        ring += expand_ring * (1.0 - expand) * 0.5;
    }

    // Combine colors
    vec3 final_color = icon_color.rgb * circle + status_color.rgb * ring * pulse;
    float final_alpha = max(circle, ring);

    // Drop shadow
    float shadow_dist = length(center_uv + vec2(0.05, -0.05));
    float shadow = smoothstep(0.5, 0.35, shadow_dist) * 0.3;
    final_color = mix(vec3(0.0), final_color, step(0.01, final_alpha));
    final_alpha = max(final_alpha, shadow);

    ALBEDO = final_color;
    ALPHA = final_alpha;

    // Discard fully transparent pixels
    if (ALPHA < 0.01) {
        discard;
    }
}
