// =============================================================================
// MockHomeProvider.cs — Provides a realistic sample home for testing
// 8 rooms, ~20 devices, realistic apartment layout
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Data;

/// <summary>
/// Provides mock SmartHome data for the 3D Home Map View.
/// Creates a realistic 8-room apartment with ~20 IoT devices.
/// </summary>
public static class MockHomeProvider
{
    /// <summary>Create a fully populated sample home.</summary>
    public static SmartHome CreateSampleHome()
    {
        var devices = CreateDevices();
        var rooms = CreateRooms(devices);
        var placements = CreateDevicePlacements();

        return new SmartHome
        {
            Id = "home_001",
            Name = "My Smart Home",
            Rooms = rooms,
            Devices = devices,
            DevicePlacements = placements
        };
    }

    private static List<SmartRoom> CreateRooms(List<SmartDevice> devices)
    {
        // Apartment layout (all coordinates in meters, XZ plane)
        // Roughly 12m x 8m apartment
        //
        //  ┌─────────────┬──────────┐
        //  │  Living Rm  │ Kitchen  │
        //  │  (0,0)-(6,4)│(6,0)-(10,4)│
        //  ├─────┬───────┼──────────┤
        //  │Bath1│Hallway│ Bedroom2 │
        //  │     │       │          │
        //  ├─────┤       ├──────────┤
        //  │Bath2│       │Master BR │
        //  │     │       │          │
        //  └─────┴───────┴──────────┘
        //  + Balcony off living room (top)

        return new List<SmartRoom>
        {
            new("room_living", "Living Room",
                devices.Where(d => d.RoomId == "room_living").ToList(),
                RoomType: RoomType.LivingRoom,
                FloorPolygon: new List<Vector2>
                {
                    new(0, 0), new(6, 0), new(6, 4), new(0, 4)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 0), new(6, 0)),           // North wall
                    new(new(6, 0), new(6, 4)),           // East wall (shared with kitchen)
                    new(new(6, 4), new(0, 4), HasDoor: true, DoorWidth: 1.0f), // South wall with door
                    new(new(0, 4), new(0, 0)),           // West wall
                }),

            new("room_kitchen", "Kitchen",
                devices.Where(d => d.RoomId == "room_kitchen").ToList(),
                RoomType: RoomType.Kitchen,
                FloorPolygon: new List<Vector2>
                {
                    new(6, 0), new(10, 0), new(10, 4), new(6, 4)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(6, 0), new(10, 0)),
                    new(new(10, 0), new(10, 4)),
                    new(new(10, 4), new(6, 4)),
                    new(new(6, 4), new(6, 0), HasDoor: true, DoorWidth: 1.2f),
                }),

            new("room_master", "Master Bedroom",
                devices.Where(d => d.RoomId == "room_master").ToList(),
                RoomType: RoomType.Bedroom,
                FloorPolygon: new List<Vector2>
                {
                    new(6, 6), new(10, 6), new(10, 10), new(6, 10)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(6, 6), new(10, 6)),
                    new(new(10, 6), new(10, 10)),
                    new(new(10, 10), new(6, 10)),
                    new(new(6, 10), new(6, 6), HasDoor: true),
                }),

            new("room_bed2", "Bedroom 2",
                devices.Where(d => d.RoomId == "room_bed2").ToList(),
                RoomType: RoomType.Bedroom,
                FloorPolygon: new List<Vector2>
                {
                    new(6, 4), new(10, 4), new(10, 6), new(6, 6)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(6, 4), new(10, 4)),
                    new(new(10, 4), new(10, 6)),
                    new(new(10, 6), new(6, 6)),
                    new(new(6, 6), new(6, 4), HasDoor: true),
                }),

            new("room_bath1", "Bathroom 1",
                devices.Where(d => d.RoomId == "room_bath1").ToList(),
                RoomType: RoomType.Bathroom,
                FloorPolygon: new List<Vector2>
                {
                    new(0, 4), new(2, 4), new(2, 7), new(0, 7)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 4), new(2, 4)),
                    new(new(2, 4), new(2, 7), HasDoor: true, DoorWidth: 0.8f),
                    new(new(2, 7), new(0, 7)),
                    new(new(0, 7), new(0, 4)),
                }),

            new("room_bath2", "Bathroom 2",
                devices.Where(d => d.RoomId == "room_bath2").ToList(),
                RoomType: RoomType.Bathroom,
                FloorPolygon: new List<Vector2>
                {
                    new(0, 7), new(2, 7), new(2, 10), new(0, 10)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 7), new(2, 7)),
                    new(new(2, 7), new(2, 10), HasDoor: true, DoorWidth: 0.8f),
                    new(new(2, 10), new(0, 10)),
                    new(new(0, 10), new(0, 7)),
                }),

            new("room_hallway", "Hallway",
                devices.Where(d => d.RoomId == "room_hallway").ToList(),
                RoomType: RoomType.Hallway,
                FloorPolygon: new List<Vector2>
                {
                    new(2, 4), new(6, 4), new(6, 10), new(2, 10)
                },
                WallSegments: new List<WallSegment>
                {
                    new(new(2, 4), new(6, 4), HasDoor: true, DoorWidth: 1.0f),
                    new(new(6, 4), new(6, 10)),
                    new(new(6, 10), new(2, 10)),
                    new(new(2, 10), new(2, 4)),
                }),

            new("room_balcony", "Balcony",
                devices.Where(d => d.RoomId == "room_balcony").ToList(),
                RoomType: RoomType.Balcony,
                FloorPolygon: new List<Vector2>
                {
                    new(1, -2), new(5, -2), new(5, 0), new(1, 0)
                },
                WallHeight: 1.2f,
                WallSegments: new List<WallSegment>
                {
                    new(new(1, -2), new(5, -2), Height: 1.2f),
                    new(new(5, -2), new(5, 0), Height: 1.2f),
                    new(new(1, 0), new(1, -2), Height: 1.2f),
                    // No wall on apartment-facing side (opening)
                }),
        };
    }

    private static List<SmartDevice> CreateDevices()
    {
        return new List<SmartDevice>
        {
            // Living Room
            CreateDevice("dev_tv", "Living Room TV", DeviceCategory.Television, "room_living"),
            CreateDevice("dev_light_lr", "Living Room Light", DeviceCategory.Light, "room_living"),
            CreateDevice("dev_speaker", "Smart Speaker", DeviceCategory.Speaker, "room_living"),
            CreateDevice("dev_thermo", "Thermostat", DeviceCategory.Thermostat, "room_living"),

            // Kitchen
            CreateDevice("dev_light_kit", "Kitchen Light", DeviceCategory.Light, "room_kitchen"),
            CreateDevice("dev_fridge", "Smart Fridge", DeviceCategory.Appliance, "room_kitchen"),
            CreateDevice("dev_plug_kit", "Kitchen Plug", DeviceCategory.Switch, "room_kitchen"),

            // Master Bedroom
            CreateDevice("dev_light_mb", "Bedroom Light", DeviceCategory.Light, "room_master"),
            CreateDevice("dev_ac_mb", "Bedroom AC", DeviceCategory.Thermostat, "room_master"),
            CreateDevice("dev_sensor_mb", "Motion Sensor", DeviceCategory.Sensor, "room_master"),

            // Bedroom 2
            CreateDevice("dev_light_b2", "Bedroom 2 Light", DeviceCategory.Light, "room_bed2"),
            CreateDevice("dev_plug_b2", "Desk Plug", DeviceCategory.Switch, "room_bed2"),

            // Bathroom 1
            CreateDevice("dev_light_bt1", "Bath 1 Light", DeviceCategory.Light, "room_bath1"),
            CreateDevice("dev_sensor_bt1", "Leak Sensor", DeviceCategory.Sensor, "room_bath1"),

            // Bathroom 2
            CreateDevice("dev_light_bt2", "Bath 2 Light", DeviceCategory.Light, "room_bath2"),

            // Hallway
            CreateDevice("dev_light_hall", "Hallway Light", DeviceCategory.Light, "room_hallway"),
            CreateDevice("dev_lock", "Front Door Lock", DeviceCategory.Lock, "room_hallway"),
            CreateDevice("dev_cam", "Door Camera", DeviceCategory.Camera, "room_hallway"),
            CreateDevice("dev_hub", "SmartThings Hub", DeviceCategory.Hub, "room_hallway"),

            // Balcony
            CreateDevice("dev_light_bal", "Balcony Light", DeviceCategory.Light, "room_balcony"),
            CreateDevice("dev_sensor_bal", "Weather Sensor", DeviceCategory.Sensor, "room_balcony"),
        };
    }

    private static List<DevicePlacement> CreateDevicePlacements()
    {
        return new List<DevicePlacement>
        {
            // Living Room
            new("dev_tv", "room_living", new Vector3(3, 0, 0.3f)),
            new("dev_light_lr", "room_living", new Vector3(3, 0, 2)),
            new("dev_speaker", "room_living", new Vector3(1, 0, 1)),
            new("dev_thermo", "room_living", new Vector3(5.5f, 0, 2)),

            // Kitchen
            new("dev_light_kit", "room_kitchen", new Vector3(8, 0, 2)),
            new("dev_fridge", "room_kitchen", new Vector3(9.5f, 0, 1)),
            new("dev_plug_kit", "room_kitchen", new Vector3(7, 0, 3)),

            // Master Bedroom
            new("dev_light_mb", "room_master", new Vector3(8, 0, 8)),
            new("dev_ac_mb", "room_master", new Vector3(9.5f, 0, 7)),
            new("dev_sensor_mb", "room_master", new Vector3(7, 0, 9)),

            // Bedroom 2
            new("dev_light_b2", "room_bed2", new Vector3(8, 0, 5)),
            new("dev_plug_b2", "room_bed2", new Vector3(9, 0, 5.5f)),

            // Bathrooms
            new("dev_light_bt1", "room_bath1", new Vector3(1, 0, 5.5f)),
            new("dev_sensor_bt1", "room_bath1", new Vector3(1, 0, 6.5f)),
            new("dev_light_bt2", "room_bath2", new Vector3(1, 0, 8.5f)),

            // Hallway
            new("dev_light_hall", "room_hallway", new Vector3(4, 0, 7)),
            new("dev_lock", "room_hallway", new Vector3(4, 0, 9.5f)),
            new("dev_cam", "room_hallway", new Vector3(4, 0, 9.8f)),
            new("dev_hub", "room_hallway", new Vector3(3, 0, 5)),

            // Balcony
            new("dev_light_bal", "room_balcony", new Vector3(3, 0, -1)),
            new("dev_sensor_bal", "room_balcony", new Vector3(3, 0, -1.5f)),
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
