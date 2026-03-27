// =============================================================================
// MockHomeProvider.cs — Simplified home layout for testing
// 7 rooms: 3 bedrooms, 1 living room, 1 kitchen, 2 balconies
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Data;

/// <summary>
/// Provides mock SmartHome data with a clean 7-room apartment layout.
/// </summary>
public static class MockHomeProvider
{
    public static SmartHome CreateSampleHome()
    {
        var devices = CreateDevices();
        var rooms = CreateRooms(devices);
        var placements = CreateDevicePlacements();

        return new SmartHome
        {
            Id = "home_001",
            Name = "My home",
            Rooms = rooms,
            Devices = devices,
            DevicePlacements = placements
        };
    }

    private static List<SmartRoom> CreateRooms(List<SmartDevice> devices)
    {
        // Clean apartment layout (12m x 8m):
        //
        // ┌──────────┬──────────┬──────────┐
        // │ Bedroom 1│  Living  │ Kitchen  │
        // │  (pink)  │  room    │ (yellow) │
        // │  4x4     │ (green)  │  4x4     │
        // │          │  4x4     │          │
        // ├──────────┼──────────┼──────────┤
        // │ Bedroom 2│ Bedroom 3│ Balcony 1│
        // │  (pink)  │  (pink)  │ (purple) │
        // │  4x4     │  4x4     │  4x4     │
        // │          │          │          │
        // └──────────┴──────────┴──────────┘
        //                       ┌──────────┐
        //                       │ Balcony 2│
        //                       │ (purple) │
        //                       │  4x2     │
        //                       └──────────┘

        return new List<SmartRoom>
        {
            // Top row
            new("room_bed1", "Bedroom 1",
                devices.Where(d => d.RoomId == "room_bed1").ToList(),
                RoomType: RoomType.Bedroom,
                FloorPolygon: new List<Vector2> { new(0, 0), new(4, 0), new(4, 4), new(0, 4) },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 0), new(4, 0)),
                    new(new(4, 0), new(4, 4), HasDoor: true, DoorWidth: 0.9f),
                    new(new(4, 4), new(0, 4)),
                    new(new(0, 4), new(0, 0)),
                }),

            new("room_living", "Living room",
                devices.Where(d => d.RoomId == "room_living").ToList(),
                RoomType: RoomType.LivingRoom,
                FloorPolygon: new List<Vector2> { new(4, 0), new(8, 0), new(8, 4), new(4, 4) },
                WallSegments: new List<WallSegment>
                {
                    new(new(4, 0), new(8, 0)),
                    new(new(8, 0), new(8, 4), HasDoor: true, DoorWidth: 1.0f),
                    new(new(8, 4), new(4, 4), HasDoor: true, DoorWidth: 1.0f),
                    new(new(4, 4), new(4, 0), HasDoor: true, DoorWidth: 1.0f),
                }),

            new("room_kitchen", "Kitchen",
                devices.Where(d => d.RoomId == "room_kitchen").ToList(),
                RoomType: RoomType.Kitchen,
                FloorPolygon: new List<Vector2> { new(8, 0), new(12, 0), new(12, 4), new(8, 4) },
                WallSegments: new List<WallSegment>
                {
                    new(new(8, 0), new(12, 0)),
                    new(new(12, 0), new(12, 4)),
                    new(new(12, 4), new(8, 4), HasDoor: true, DoorWidth: 1.0f),
                    new(new(8, 4), new(8, 0), HasDoor: true, DoorWidth: 1.0f),
                }),

            // Bottom row
            new("room_bed2", "Bedroom 2",
                devices.Where(d => d.RoomId == "room_bed2").ToList(),
                RoomType: RoomType.Bedroom,
                FloorPolygon: new List<Vector2> { new(0, 4), new(4, 4), new(4, 8), new(0, 8) },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 4), new(4, 4)),
                    new(new(4, 4), new(4, 8), HasDoor: true, DoorWidth: 0.9f),
                    new(new(4, 8), new(0, 8)),
                    new(new(0, 8), new(0, 4)),
                }),

            new("room_bed3", "Bedroom 3",
                devices.Where(d => d.RoomId == "room_bed3").ToList(),
                RoomType: RoomType.Bedroom,
                FloorPolygon: new List<Vector2> { new(4, 4), new(8, 4), new(8, 8), new(4, 8) },
                WallSegments: new List<WallSegment>
                {
                    new(new(4, 4), new(8, 4)),
                    new(new(8, 4), new(8, 8), HasDoor: true, DoorWidth: 0.9f),
                    new(new(8, 8), new(4, 8)),
                    new(new(4, 8), new(4, 4), HasDoor: true, DoorWidth: 0.9f),
                }),

            // Balconies
            new("room_balcony1", "Balcony 1",
                devices.Where(d => d.RoomId == "room_balcony1").ToList(),
                RoomType: RoomType.Balcony,
                FloorPolygon: new List<Vector2> { new(8, 4), new(12, 4), new(12, 8), new(8, 8) },
                WallSegments: new List<WallSegment>
                {
                    new(new(8, 4), new(12, 4)),
                    new(new(12, 4), new(12, 8)),
                    new(new(12, 8), new(8, 8)),
                    new(new(8, 8), new(8, 4), HasDoor: true, DoorWidth: 1.2f),
                }),

            new("room_balcony2", "Balcony 2",
                devices.Where(d => d.RoomId == "room_balcony2").ToList(),
                RoomType: RoomType.Balcony,
                FloorPolygon: new List<Vector2> { new(8, 8), new(12, 8), new(12, 10), new(8, 10) },
                WallSegments: new List<WallSegment>
                {
                    new(new(8, 8), new(12, 8)),
                    new(new(12, 8), new(12, 10)),
                    new(new(12, 10), new(8, 10)),
                    new(new(8, 10), new(8, 8), HasDoor: true, DoorWidth: 1.2f),
                }),
        };
    }

    private static List<SmartDevice> CreateDevices()
    {
        return new List<SmartDevice>
        {
            // Bedroom 1
            CreateDevice("dev_light_b1", "Bedroom Light", DeviceCategory.Light, "room_bed1"),
            CreateDevice("dev_ac_b1", "Air Conditioner", DeviceCategory.Thermostat, "room_bed1"),
            CreateDevice("dev_blinds_b1", "Smart Blinds", DeviceCategory.Switch, "room_bed1"),

            // Living room
            CreateDevice("dev_light_lr", "Living Light", DeviceCategory.Light, "room_living"),
            CreateDevice("dev_tv", "Smart TV", DeviceCategory.Television, "room_living"),
            CreateDevice("dev_speaker", "Smart Speaker", DeviceCategory.Speaker, "room_living"),
            CreateDevice("dev_hub", "SmartThings Hub", DeviceCategory.Hub, "room_living"),

            // Kitchen
            CreateDevice("dev_light_kit", "Kitchen Light", DeviceCategory.Light, "room_kitchen"),
            CreateDevice("dev_fridge", "Smart Fridge", DeviceCategory.Appliance, "room_kitchen"),
            CreateDevice("dev_cam_kit", "Kitchen Camera", DeviceCategory.Camera, "room_kitchen"),

            // Bedroom 2
            CreateDevice("dev_light_b2", "Bedroom Light", DeviceCategory.Light, "room_bed2"),
            CreateDevice("dev_cam_b2", "Camera", DeviceCategory.Camera, "room_bed2"),

            // Bedroom 3
            CreateDevice("dev_light_b3", "Bedroom Light", DeviceCategory.Light, "room_bed3"),
            CreateDevice("dev_blinds_b3", "Blinds", DeviceCategory.Switch, "room_bed3"),

            // Balcony 1
            CreateDevice("dev_cam_bal1", "Balcony Camera", DeviceCategory.Camera, "room_balcony1"),
            CreateDevice("dev_sensor_bal1", "Motion Sensor", DeviceCategory.Sensor, "room_balcony1"),

            // Balcony 2
            CreateDevice("dev_light_bal2", "Balcony Light", DeviceCategory.Light, "room_balcony2"),
        };
    }

    private static List<DevicePlacement> CreateDevicePlacements()
    {
        return new List<DevicePlacement>
        {
            // Bedroom 1 (0-4, 0-4)
            new("dev_light_b1", "room_bed1", new Vector3(2, 0, 2)),
            new("dev_ac_b1", "room_bed1", new Vector3(1, 0, 0.5f)),
            new("dev_blinds_b1", "room_bed1", new Vector3(3.2f, 0, 3)),

            // Living room (4-8, 0-4)
            new("dev_light_lr", "room_living", new Vector3(6, 0, 2)),
            new("dev_tv", "room_living", new Vector3(5, 0, 0.8f)),
            new("dev_speaker", "room_living", new Vector3(7, 0, 1)),
            new("dev_hub", "room_living", new Vector3(6, 0, 3.2f)),

            // Kitchen (8-12, 0-4)
            new("dev_light_kit", "room_kitchen", new Vector3(10, 0, 2)),
            new("dev_fridge", "room_kitchen", new Vector3(11, 0, 1)),
            new("dev_cam_kit", "room_kitchen", new Vector3(9, 0, 3)),

            // Bedroom 2 (0-4, 4-8)
            new("dev_light_b2", "room_bed2", new Vector3(2, 0, 6)),
            new("dev_cam_b2", "room_bed2", new Vector3(1, 0, 5)),

            // Bedroom 3 (4-8, 4-8)
            new("dev_light_b3", "room_bed3", new Vector3(6, 0, 6)),
            new("dev_blinds_b3", "room_bed3", new Vector3(7, 0, 7)),

            // Balcony 1 (8-12, 4-8)
            new("dev_cam_bal1", "room_balcony1", new Vector3(10, 0, 6)),
            new("dev_sensor_bal1", "room_balcony1", new Vector3(11, 0, 7)),

            // Balcony 2 (8-12, 8-10)
            new("dev_light_bal2", "room_balcony2", new Vector3(10, 0, 9)),
        };
    }

    private static SmartDevice CreateDevice(string id, string label, DeviceCategory category, string roomId)
    {
        return new SmartDevice
        {
            DeviceId = id,
            Name = id,
            Label = label,
            Category = category,
            RoomId = roomId,
            Status = DeviceStatus.Online
        };
    }
}
