// =============================================================================
// HomeMapTests.cs — Tests for Phase 3 Home Map models and logic
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;
using Xunit;

namespace SmartThings.Tests;

public class HomeModelsTests
{
    [Fact]
    public void SmartHome_CanBeCreated()
    {
        var home = new SmartHome
        {
            Id = "test_home",
            Name = "Test Home"
        };

        Assert.Equal("test_home", home.Id);
        Assert.Equal("Test Home", home.Name);
        Assert.Empty(home.Rooms);
        Assert.Empty(home.Devices);
        Assert.Empty(home.DevicePlacements);
    }

    [Fact]
    public void SmartHome_WithRoomsAndDevices()
    {
        var device = new SmartDevice
        {
            DeviceId = "dev1",
            Name = "test",
            Label = "Test Light",
            Category = DeviceCategory.Light,
            RoomId = "room1"
        };

        var room = new SmartRoom(
            "room1", "Living Room",
            new[] { device },
            RoomType: RoomType.LivingRoom,
            FloorPolygon: new List<Vector2>
            {
                new(0, 0), new(5, 0), new(5, 4), new(0, 4)
            }
        );

        var home = new SmartHome
        {
            Id = "h1",
            Name = "My Home",
            Rooms = new() { room },
            Devices = new() { device }
        };

        Assert.Single(home.Rooms);
        Assert.Single(home.Devices);
        Assert.Equal(RoomType.LivingRoom, home.Rooms[0].RoomType);
        Assert.Equal(4, home.Rooms[0].FloorPolygon!.Count);
    }

    [Fact]
    public void WallSegment_DefaultValues()
    {
        var wall = new WallSegment(new(0, 0), new(5, 0));

        Assert.Equal(2.8f, wall.Height);
        Assert.False(wall.HasDoor);
        Assert.Equal(0.9f, wall.DoorWidth);
        Assert.Equal(2.1f, wall.DoorHeight);
    }

    [Fact]
    public void WallSegment_WithDoor()
    {
        var wall = new WallSegment(new(0, 0), new(5, 0), HasDoor: true, DoorWidth: 1.2f);

        Assert.True(wall.HasDoor);
        Assert.Equal(1.2f, wall.DoorWidth);
    }

    [Fact]
    public void DevicePlacement_DefaultIconScale()
    {
        var placement = new DevicePlacement("dev1", "room1", new Vector3(1, 0, 2));

        Assert.Equal(1.0f, placement.IconScale);
        Assert.Equal(1.0f, placement.Position.X);
        Assert.Equal(2.0f, placement.Position.Z);
    }

    [Theory]
    [InlineData(RoomType.LivingRoom)]
    [InlineData(RoomType.Bedroom)]
    [InlineData(RoomType.Kitchen)]
    [InlineData(RoomType.Bathroom)]
    [InlineData(RoomType.Hallway)]
    [InlineData(RoomType.Balcony)]
    [InlineData(RoomType.Office)]
    [InlineData(RoomType.Garage)]
    [InlineData(RoomType.Laundry)]
    [InlineData(RoomType.Custom)]
    public void SmartRoom_AllRoomTypes(RoomType roomType)
    {
        var room = new SmartRoom("r1", "Test", Array.Empty<SmartDevice>(), RoomType: roomType);
        Assert.Equal(roomType, room.RoomType);
    }

    [Fact]
    public void SmartRoom_DefaultValues()
    {
        var room = new SmartRoom("r1", "Test", Array.Empty<SmartDevice>());

        Assert.Equal(RoomType.Custom, room.RoomType);
        Assert.Null(room.FloorPolygon);
        Assert.Equal(0f, room.FloorY);
        Assert.Equal(2.8f, room.WallHeight);
        Assert.Null(room.WallSegments);
        Assert.Null(room.DeviceIds);
    }
}

public class RoomColorPaletteTests
{
    [Theory]
    [InlineData(RoomType.LivingRoom)]
    [InlineData(RoomType.Bedroom)]
    [InlineData(RoomType.Kitchen)]
    [InlineData(RoomType.Bathroom)]
    [InlineData(RoomType.Hallway)]
    [InlineData(RoomType.Balcony)]
    [InlineData(RoomType.Office)]
    [InlineData(RoomType.Garage)]
    [InlineData(RoomType.Laundry)]
    [InlineData(RoomType.Custom)]
    public void GetColor_ReturnsNonBlackForAllTypes(RoomType roomType)
    {
        var color = RoomColorPalette.GetColor(roomType);

        // All colors should be non-black (visible)
        Assert.True(color.R > 0 || color.G > 0 || color.B > 0,
            $"Color for {roomType} should not be black");
        Assert.Equal(1.0f, color.A); // Fully opaque
    }

    [Fact]
    public void GetColor_LivingRoomIsBrightGreen()
    {
        var color = RoomColorPalette.GetColor(RoomType.LivingRoom);

        // 90EE90 ≈ (0.565, 0.933, 0.565) — bright green
        Assert.InRange(color.R, 0.55f, 0.58f);
        Assert.InRange(color.G, 0.92f, 0.94f);
        Assert.InRange(color.B, 0.55f, 0.58f);
    }

    [Fact]
    public void GetWallColor_IsDarkerThanFloor()
    {
        var floor = RoomColorPalette.GetColor(RoomType.LivingRoom);
        var wall = RoomColorPalette.GetWallColor(RoomType.LivingRoom);

        Assert.True(wall.R < floor.R);
        Assert.True(wall.G < floor.G);
        Assert.True(wall.B < floor.B);
    }

    [Fact]
    public void GetWallColor_Is80PercentBrightness()
    {
        var floor = RoomColorPalette.GetColor(RoomType.Kitchen);
        var wall = RoomColorPalette.GetWallColor(RoomType.Kitchen);

        Assert.Equal((double)(floor.R * 0.8f), (double)wall.R, 3);
        Assert.Equal((double)(floor.G * 0.8f), (double)wall.G, 3);
        Assert.Equal((double)(floor.B * 0.8f), (double)wall.B, 3);
    }
}

public class DeviceStatusColorsTests
{
    [Fact]
    public void OnlineIsGreen()
    {
        var color = DeviceStatusColors.Online;
        Assert.True(color.G > color.R && color.G > color.B, "Online should be green");
    }

    [Fact]
    public void OfflineIsGray()
    {
        var color = DeviceStatusColors.Offline;
        // Gray: R ≈ G ≈ B
        Assert.InRange(Math.Abs(color.R - color.G), 0, 0.05f);
    }

    [Fact]
    public void ErrorIsRed()
    {
        var color = DeviceStatusColors.Error;
        Assert.True(color.R > color.G && color.R > color.B, "Error should be red");
    }

    [Fact]
    public void ActiveIsBlue()
    {
        var color = DeviceStatusColors.Active;
        Assert.True(color.B > color.R && color.B > color.G, "Active should be blue");
    }

    [Fact]
    public void UpdatingIsOrange()
    {
        var color = DeviceStatusColors.Updating;
        Assert.True(color.R > color.B, "Updating should be orange (R > B)");
    }
}

public class HomeViewConfigTests
{
    [Fact]
    public void DefaultValues_AreReasonable()
    {
        var config = new HomeViewConfig();

        Assert.Equal(15.0f, config.DefaultCameraDistance);
        Assert.Equal(45.0f, config.DefaultCameraAngle);
        Assert.True(config.MinZoom < config.MaxZoom);
        Assert.True(config.DefaultCameraDistance >= config.MinZoom);
        Assert.True(config.DefaultCameraDistance <= config.MaxZoom);
        Assert.True(config.WallOpacity > 0 && config.WallOpacity <= 1);
        Assert.True(config.GridOpacity >= 0 && config.GridOpacity <= 1);
    }

    [Fact]
    public void CanOverrideValues()
    {
        var config = new HomeViewConfig
        {
            MinZoom = 3.0f,
            MaxZoom = 50.0f,
            PanSpeed = 0.05f
        };

        Assert.Equal(3.0f, config.MinZoom);
        Assert.Equal(50.0f, config.MaxZoom);
        Assert.Equal(0.05f, config.PanSpeed);
    }
}

public class EarClipTriangulationTests
{
    // We test the ear-clipping algorithm logic directly since it's pure math
    // These use the same types as RoomMeshGenerator.EarClipTriangulate

    [Fact]
    public void Triangle_ProducesThreeVertices()
    {
        var polygon = new List<Vector2>
        {
            new(0, 0), new(1, 0), new(0, 1)
        };

        var result = Triangulate(polygon);

        Assert.Equal(3, result.Count); // 1 triangle = 3 vertices
    }

    [Fact]
    public void Square_ProducesSixVertices()
    {
        var polygon = new List<Vector2>
        {
            new(0, 0), new(4, 0), new(4, 4), new(0, 4)
        };

        var result = Triangulate(polygon);

        Assert.Equal(6, result.Count); // 2 triangles = 6 vertices
    }

    [Fact]
    public void Pentagon_ProducesNineVertices()
    {
        var polygon = new List<Vector2>
        {
            new(0, 0), new(2, 0), new(3, 1.5f), new(1.5f, 3), new(-0.5f, 1.5f)
        };

        var result = Triangulate(polygon);

        Assert.Equal(9, result.Count); // 3 triangles = 9 vertices
    }

    [Fact]
    public void TooFewPoints_ReturnsEmpty()
    {
        Assert.Empty(Triangulate(new List<Vector2> { new(0, 0), new(1, 1) }));
        Assert.Empty(Triangulate(new List<Vector2>()));
    }

    [Fact]
    public void RealisticRoomPolygon_Triangulates()
    {
        // L-shaped room
        var polygon = new List<Vector2>
        {
            new(0, 0), new(6, 0), new(6, 4), new(3, 4), new(3, 6), new(0, 6)
        };

        var result = Triangulate(polygon);

        // 6-vertex polygon = 4 triangles = 12 vertices
        Assert.Equal(12, result.Count);
    }

    /// <summary>
    /// Standalone ear-clipping implementation matching RoomMeshGenerator
    /// (duplicated here to avoid Godot dependency in tests)
    /// </summary>
    private static List<Vector2> Triangulate(IReadOnlyList<Vector2> polygon)
    {
        var result = new List<Vector2>();
        if (polygon.Count < 3) return result;

        var indices = new List<int>();
        for (int i = 0; i < polygon.Count; i++) indices.Add(i);

        if (CalculateSignedArea(polygon) > 0)
            indices.Reverse();

        int safety = polygon.Count * 3;
        while (indices.Count > 2 && safety-- > 0)
        {
            bool earFound = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];

                var a = polygon[prev];
                var b = polygon[curr];
                var c = polygon[next];

                if (Cross2D(b - a, c - a) <= 0) continue;

                bool containsOther = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    if (j == (i - 1 + indices.Count) % indices.Count || j == i || j == (i + 1) % indices.Count)
                        continue;
                    if (PointInTriangle(polygon[indices[j]], a, b, c))
                    {
                        containsOther = true;
                        break;
                    }
                }

                if (!containsOther)
                {
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            if (!earFound) break;
        }
        return result;
    }

    private static float CalculateSignedArea(IReadOnlyList<Vector2> polygon)
    {
        float area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            area += (b.X - a.X) * (b.Y + a.Y);
        }
        return area * 0.5f;
    }

    private static float Cross2D(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross2D(b - a, p - a);
        float d2 = Cross2D(c - b, p - b);
        float d3 = Cross2D(a - c, p - c);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }
}
