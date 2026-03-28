using SmartThings.Abstraction;

namespace SmartThings.Tests;

public class MathTypesTests
{
    // --- Vector3 Tests ---

    [Fact]
    public void Vector3_Addition_ReturnsCorrectResult()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, 5, 6);
        var result = a + b;
        Assert.Equal(5, result.X);
        Assert.Equal(7, result.Y);
        Assert.Equal(9, result.Z);
    }

    [Fact]
    public void Vector3_Subtraction_ReturnsCorrectResult()
    {
        var a = new Vector3(5, 7, 9);
        var b = new Vector3(1, 2, 3);
        var result = a - b;
        Assert.Equal(4, result.X);
        Assert.Equal(5, result.Y);
        Assert.Equal(6, result.Z);
    }

    [Fact]
    public void Vector3_ScalarMultiplication_ReturnsCorrectResult()
    {
        var v = new Vector3(2, 3, 4);
        var result = v * 3f;
        Assert.Equal(6, result.X);
        Assert.Equal(9, result.Y);
        Assert.Equal(12, result.Z);
    }

    [Fact]
    public void Vector3_Length_ReturnsCorrectValue()
    {
        var v = new Vector3(3, 4, 0);
        Assert.Equal(5f, v.Length(), 0.001f);
    }

    [Fact]
    public void Vector3_Normalized_ReturnsUnitVector()
    {
        var v = new Vector3(0, 0, 5);
        var n = v.Normalized();
        Assert.Equal(0, n.X, 0.001f);
        Assert.Equal(0, n.Y, 0.001f);
        Assert.Equal(1, n.Z, 0.001f);
    }

    [Fact]
    public void Vector3_DotProduct_ReturnsCorrectValue()
    {
        var a = new Vector3(1, 0, 0);
        var b = new Vector3(0, 1, 0);
        Assert.Equal(0, Vector3.Dot(a, b), 0.001f);

        var c = new Vector3(1, 0, 0);
        Assert.Equal(1, Vector3.Dot(a, c), 0.001f);
    }

    [Fact]
    public void Vector3_CrossProduct_ReturnsCorrectValue()
    {
        var a = new Vector3(1, 0, 0);
        var b = new Vector3(0, 1, 0);
        var cross = Vector3.Cross(a, b);
        Assert.Equal(0, cross.X, 0.001f);
        Assert.Equal(0, cross.Y, 0.001f);
        Assert.Equal(1, cross.Z, 0.001f);
    }

    [Fact]
    public void Vector3_Lerp_InterpolatesCorrectly()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(10, 20, 30);
        var mid = Vector3.Lerp(a, b, 0.5f);
        Assert.Equal(5, mid.X, 0.001f);
        Assert.Equal(10, mid.Y, 0.001f);
        Assert.Equal(15, mid.Z, 0.001f);
    }

    [Fact]
    public void Vector3_StaticProperties_AreCorrect()
    {
        Assert.Equal(new Vector3(0, 0, 0), Vector3.Zero);
        Assert.Equal(new Vector3(1, 1, 1), Vector3.One);
        Assert.Equal(new Vector3(0, 1, 0), Vector3.Up);
        Assert.Equal(new Vector3(0, 0, -1), Vector3.Forward);
    }

    // --- Vector2 Tests ---

    [Fact]
    public void Vector2_Operations_Work()
    {
        var a = new Vector2(3, 4);
        Assert.Equal(5f, a.Length(), 0.001f);

        var b = new Vector2(1, 2);
        var sum = a + b;
        Assert.Equal(4, sum.X);
        Assert.Equal(6, sum.Y);
    }

    // --- Quaternion Tests ---

    [Fact]
    public void Quaternion_Identity_IsCorrect()
    {
        var q = Quaternion.Identity;
        Assert.Equal(0, q.X);
        Assert.Equal(0, q.Y);
        Assert.Equal(0, q.Z);
        Assert.Equal(1, q.W);
    }

    [Fact]
    public void Quaternion_FromEuler_ProducesNonIdentity()
    {
        // FromEuler takes radians
        var euler = new Vector3(0.5f, 0.8f, 1.0f);
        var q = Quaternion.FromEuler(euler);

        // Should not be identity for non-zero euler
        Assert.NotEqual(Quaternion.Identity, q);

        // Should be approximately unit quaternion
        var magnitude = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        Assert.Equal(1f, magnitude, 0.001f);
    }

    [Fact]
    public void Quaternion_FromEuler_ZeroReturnsIdentity()
    {
        var q = Quaternion.FromEuler(Vector3.Zero);
        Assert.Equal(0, q.X, 0.001f);
        Assert.Equal(0, q.Y, 0.001f);
        Assert.Equal(0, q.Z, 0.001f);
        Assert.Equal(1, q.W, 0.001f);
    }

    // --- Transform3D Tests ---

    [Fact]
    public void Transform3D_Identity_HasCorrectDefaults()
    {
        var t = Transform3D.Identity;
        Assert.Equal(Vector3.Zero, t.Position);
        Assert.Equal(Quaternion.Identity, t.Rotation);
        Assert.Equal(Vector3.One, t.Scale);
    }

    // --- Color Tests ---

    [Fact]
    public void Color_FromHex_ParsesCorrectly()
    {
        var color = Color.FromHex("#FF0000");
        Assert.Equal(1f, color.R, 0.01f);
        Assert.Equal(0f, color.G, 0.01f);
        Assert.Equal(0f, color.B, 0.01f);
        Assert.Equal(1f, color.A, 0.01f);
    }

    [Fact]
    public void Color_FromHex_ParsesWithAlpha()
    {
        var color = Color.FromHex("#FF000080");
        Assert.Equal(1f, color.R, 0.01f);
        Assert.Equal(0f, color.G, 0.01f);
        Assert.Equal(0f, color.B, 0.01f);
        Assert.InRange(color.A, 0.49f, 0.52f);
    }

    [Fact]
    public void Color_StaticColors_AreCorrect()
    {
        Assert.Equal(new Color(1, 1, 1, 1), Color.White);
        Assert.Equal(new Color(0, 0, 0, 1), Color.Black);
        Assert.Equal(new Color(1, 0, 0, 1), Color.Red);
    }
}
