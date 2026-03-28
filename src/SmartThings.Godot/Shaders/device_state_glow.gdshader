// =============================================================================
// device_state_glow.gdshader — IoT Device State Visualization
// Reusable shader for SmartThings device objects in 3D scene
// Set parameters from C# via ShaderMaterial.SetShaderParameter()
// =============================================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// --- Device state parameters (set from C#) ---
uniform vec4 device_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 glow_color : source_color = vec4(0.0, 0.5, 1.0, 1.0);
uniform float glow_intensity : hint_range(0.0, 5.0) = 0.0;
uniform float pulse_speed : hint_range(0.0, 5.0) = 1.0;
uniform bool is_active = false;
uniform bool is_error = false;
uniform bool is_selected = false;

// --- Textures ---
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap;
uniform sampler2D normal_texture : hint_normal;

// --- Selection outline ---
uniform float outline_width : hint_range(0.0, 0.05) = 0.02;
uniform vec4 selection_color : source_color = vec4(0.0, 0.8, 1.0, 1.0);

void vertex() {
    // Selection outline: expand vertices along normals
    if (is_selected) {
        VERTEX += NORMAL * outline_width;
    }
}

void fragment() {
    // Base color
    vec4 tex = texture(albedo_texture, UV);
    ALBEDO = tex.rgb * device_color.rgb;
    METALLIC = 0.1;
    ROUGHNESS = 0.7;

    // Normal map
    NORMAL_MAP = texture(normal_texture, UV).rgb;

    // State-dependent emission
    vec3 emission = vec3(0.0);

    if (is_error) {
        // Error state: red pulsing
        float pulse = sin(TIME * 3.0) * 0.5 + 0.5;
        emission = vec3(1.0, 0.1, 0.1) * pulse * 2.0;
    } else if (is_active) {
        // Active state: steady glow with subtle pulse
        float pulse = sin(TIME * pulse_speed) * 0.15 + 0.85;
        emission = glow_color.rgb * glow_intensity * pulse;
    }

    EMISSION = emission;

    // Rim highlight when selected (for accessibility — visual focus indicator)
    if (is_selected) {
        float rim = pow(1.0 - dot(NORMAL, VIEW), 2.0);
        EMISSION += selection_color.rgb * rim * 1.5;
    }
}
