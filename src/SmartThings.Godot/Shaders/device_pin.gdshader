// =============================================================================
// device_pin.gdshader — Google Maps-style teardrop pin with status ring
// Mobile renderer compatible (GLSL ES 3.0)
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, unshaded;

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
    // UV remapped: center at (0, 0), range -1 to 1
    vec2 uv = (UV - 0.5) * 2.0;

    // Google Maps teardrop pin shape
    // Top circle (head of pin) centered at (0, 0.25), radius 0.32
    vec2 head_center = vec2(0.0, 0.25);
    float head_dist = length(uv - head_center);
    float head = smoothstep(0.34, 0.32, head_dist);

    // Pointed bottom triangle (from circle down to tip)
    // Tip at (0, -0.55)
    float tip_y = -0.55;
    float in_triangle = 0.0;
    if (uv.y < head_center.y && uv.y > tip_y) {
        // Width tapers linearly from circle width to 0
        float t = (uv.y - tip_y) / (head_center.y - tip_y); // 0 at tip, 1 at head
        float half_width = 0.32 * t * t; // Curved taper for teardrop look
        float inside = smoothstep(half_width + 0.02, half_width, abs(uv.x));
        in_triangle = inside;
    }

    // Combine head circle and tail
    float pin_shape = max(head, in_triangle);

    // Status ring around the head circle only
    float ring_outer = smoothstep(0.34 + ring_width, 0.34 + ring_width - 0.025, head_dist);
    float ring_inner = smoothstep(0.34, 0.34 - 0.025, head_dist);
    float ring = ring_outer * (1.0 - ring_inner);

    // Pulse animation for active devices
    float pulse = 1.0;
    if (is_active) {
        pulse = sin(TIME * pulse_speed) * 0.2 + 0.8;
        // Expanding ring effect around head
        float expand = fract(TIME * pulse_speed * 0.25);
        float exp_dist = length(uv - head_center);
        float expand_ring = smoothstep(0.34 + expand * 0.4, 0.34 + expand * 0.4 + 0.02, exp_dist)
                          * smoothstep(0.34 + expand * 0.4 + 0.06, 0.34 + expand * 0.4 + 0.04, exp_dist);
        ring += expand_ring * (1.0 - expand) * 0.4;
    }

    // Combine colors: pin body = icon_color, ring = status_color
    vec3 final_color = icon_color.rgb * pin_shape + status_color.rgb * ring * pulse;
    float final_alpha = max(pin_shape, ring);

    // Subtle inner highlight on head (glossy look)
    float highlight = smoothstep(0.25, 0.1, length(uv - head_center + vec2(-0.08, 0.08)));
    final_color += vec3(0.15) * highlight * pin_shape;

    // Drop shadow (offset down-right)
    vec2 shadow_uv = uv + vec2(0.04, -0.06);
    vec2 sh_center = vec2(0.0, 0.25);
    float sh_head = smoothstep(0.38, 0.34, length(shadow_uv - sh_center));
    float sh_tail = 0.0;
    if (shadow_uv.y < sh_center.y && shadow_uv.y > tip_y) {
        float st = (shadow_uv.y - tip_y) / (sh_center.y - tip_y);
        float shw = 0.34 * st * st;
        sh_tail = smoothstep(shw + 0.02, shw, abs(shadow_uv.x));
    }
    float shadow = max(sh_head, sh_tail) * 0.25;
    // Only show shadow where pin isn't
    shadow *= (1.0 - pin_shape);
    final_alpha = max(final_alpha, shadow);
    final_color = mix(vec3(0.0), final_color, step(0.01, max(pin_shape, ring)));

    ALBEDO = final_color;
    ALPHA = final_alpha;

    if (ALPHA < 0.01) {
        discard;
    }
}
