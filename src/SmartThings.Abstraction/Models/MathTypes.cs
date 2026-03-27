// =============================================================================
// MathTypes.cs — Engine-agnostic math types
// These map to both Unity (UnityEngine.Vector3) and Godot (Godot.Vector3)
// without referencing either engine's assemblies.
// =============================================================================

namespace SmartThings.Abstraction;

/// <summary>3D vector. Maps to Godot.Vector3 and UnityEngine.Vector3.</summary>
public readonly record struct Vector3(float X, float Y, float Z)
{
    public static readonly Vector3 Zero = new(0, 0, 0);
    public static readonly Vector3 One = new(1, 1, 1);
    public static readonly Vector3 Up = new(0, 1, 0);
    public static readonly Vector3 Forward = new(0, 0, -1); // Godot uses -Z forward like Unity

    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public Vector3 Normalized()
    {
        var len = Length();
        return len > 0 ? new Vector3(X / len, Y / len, Z / len) : Zero;
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3 Cross(Vector3 a, Vector3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}

/// <summary>2D vector for screen coordinates.</summary>
public readonly record struct Vector2(float X, float Y)
{
    public static readonly Vector2 Zero = new(0, 0);
    public static readonly Vector2 One = new(1, 1);

    public float Length() => MathF.Sqrt(X * X + Y * Y);
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, float s) => new(v.X * s, v.Y * s);
}

/// <summary>Quaternion rotation.</summary>
public readonly record struct Quaternion(float X, float Y, float Z, float W)
{
    public static readonly Quaternion Identity = new(0, 0, 0, 1);

    public static Quaternion FromEuler(Vector3 euler)
    {
        float cx = MathF.Cos(euler.X * 0.5f), sx = MathF.Sin(euler.X * 0.5f);
        float cy = MathF.Cos(euler.Y * 0.5f), sy = MathF.Sin(euler.Y * 0.5f);
        float cz = MathF.Cos(euler.Z * 0.5f), sz = MathF.Sin(euler.Z * 0.5f);
        return new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz
        );
    }
}

/// <summary>3D transform (position + rotation + scale).</summary>
public record Transform3D(
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Scale
)
{
    public static readonly Transform3D Identity = new(Vector3.Zero, Quaternion.Identity, Vector3.One);
}

/// <summary>RGBA color (0.0 to 1.0 per channel).</summary>
public readonly record struct Color(float R, float G, float B, float A = 1.0f)
{
    public static readonly Color White = new(1, 1, 1, 1);
    public static readonly Color Black = new(0, 0, 0, 1);
    public static readonly Color Red = new(1, 0, 0, 1);
    public static readonly Color Green = new(0, 1, 0, 1);
    public static readonly Color Blue = new(0, 0, 1, 1);
    public static readonly Color Transparent = new(0, 0, 0, 0);

    /// <summary>Create from hex string (e.g., "#FF5733" or "FF5733").</summary>
    public static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return new Color(
            Convert.ToInt32(hex[..2], 16) / 255f,
            Convert.ToInt32(hex[2..4], 16) / 255f,
            Convert.ToInt32(hex[4..6], 16) / 255f,
            hex.Length >= 8 ? Convert.ToInt32(hex[6..8], 16) / 255f : 1f
        );
    }
}
