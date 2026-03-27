// =============================================================================
// MockHomeProvider.cs — SmartThings-style home layout matching reference images
// 10 rooms with bright colors, realistic layout, ~22 devices
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Data;

/// <summary>
/// Provides mock SmartHome data matching the SmartThings Map View reference.
/// Layout inspired by the reference screenshots: top-down apartment view.
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
        // Layout matching SmartThings reference (12x10 meters):
        //
        // ┌──────────┬──────────┬──────────┬───────────┐
        // │ Media    │  Deck    │ Kitchen  │ Primary   │
        // │ room     │          │          │ suite     │
        // │ (blue)   │(purple)  │(yellow)  │ (teal)    │
        // ├──────────┼──────────┤          ├───────────┤
        // │          │ Living   │          │           │
        // │          │ room     ├──────────┤           │
        // │          │ (green)  │          │           │
        // ├──────────┼──────────┼──────────┼─────┬─────┤
        // │ Bedroom  │  Porch   │ Dining   │Laun-│Bath-│
        // │ (pink)   │(lt green)│ (blue)   │dry  │room │
        // └──────────┴──────────┴──────────┴─────┴─────┘

        return new List<SmartRoom>
        {
            // Top row
            new("room_media", "Media room",
                devices.Where(d => d.RoomId == "room_media").ToList(),
                RoomType: RoomType.Bedroom,  // Blue color via Bedroom→ we override
                FloorPolygon: new List<Vector2> { new(0, 0), new(4, 0), new(4, 5), new(0, 5) },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 0), new(4, 0)),
                    new(new(4, 0), new(4, 5), HasDoor: true, DoorWidth: 1.0f),
                    new(new(4, 5), new(0, 5)),
                    new(new(0, 5), new(0, 0)),
                }),

            new("room_deck", "Deck",
                devices.Where(d => d.RoomId == "room_deck").ToList(),
                RoomType: RoomType.Balcony,  // Purple
                FloorPolygon: new List<Vector2> { new(4, 0), new(7, 0), new(7, 3), new(4, 3) },
                WallSegments: new List<WallSegment>
                {
                    new(new(4, 0), new(7, 0)),
                    new(new(7, 0), new(7, 3)),
                    new(new(7, 3), new(4, 3), HasDoor: true, DoorWidth: 1.2f),
                    new(new(4, 3), new(4, 0)),
                }),

            new("room_kitchen", "Kitchen",
                devices.Where(d => d.RoomId == "room_kitchen").ToList(),
                RoomType: RoomType.Kitchen,  // Yellow
                FloorPolygon: new List<Vector2> { new(7, 0), new(10, 0), new(10, 5), new(7, 5) },
                WallSegments: new List<WallSegment>
                {
                    new(new(7, 0), new(10, 0)),
                    new(new(10, 0), new(10, 5), HasDoor: true, DoorWidth: 1.0f),
                    new(new(10, 5), new(7, 5)),
                    new(new(7, 5), new(7, 0)),
                }),

            new("room_primary", "Primary suite",
                devices.Where(d => d.RoomId == "room_primary").ToList(),
                RoomType: RoomType.Office,  // Teal
                FloorPolygon: new List<Vector2> { new(10, 0), new(14, 0), new(14, 5), new(10, 5) },
                WallSegments: new List<WallSegment>
                {
                    new(new(10, 0), new(14, 0)),
                    new(new(14, 0), new(14, 5)),
                    new(new(14, 5), new(10, 5), HasDoor: true, DoorWidth: 0.9f),
                    new(new(10, 5), new(10, 0)),
                }),

            // Middle
            new("room_living", "Living room",
                devices.Where(d => d.RoomId == "room_living").ToList(),
                RoomType: RoomType.LivingRoom,  // Green
                FloorPolygon: new List<Vector2> { new(4, 3), new(10, 3), new(10, 7), new(4, 7) },
                WallSegments: new List<WallSegment>
                {
                    new(new(4, 3), new(10, 3)),
                    new(new(10, 3), new(10, 7), HasDoor: true, DoorWidth: 1.2f),
                    new(new(10, 7), new(4, 7), HasDoor: true, DoorWidth: 1.0f),
                    new(new(4, 7), new(4, 3), HasDoor: true, DoorWidth: 1.0f),
                }),

            // Bottom row
            new("room_bedroom", "Bedroom",
                devices.Where(d => d.RoomId == "room_bedroom").ToList(),
                RoomType: RoomType.Bedroom,  // Pink
                FloorPolygon: new List<Vector2> { new(0, 5), new(4, 5), new(4, 10), new(0, 10) },
                WallSegments: new List<WallSegment>
                {
                    new(new(0, 5), new(4, 5)),
                    new(new(4, 5), new(4, 10), HasDoor: true, DoorWidth: 0.9f),
                    new(new(4, 10), new(0, 10)),
                    new(new(0, 10), new(0, 5)),
                }),

            new("room_porch", "Porch",
                devices.Where(d => d.RoomId == "room_porch").ToList(),
                RoomType: RoomType.Hallway,  // Light green
                FloorPolygon: new List<Vector2> { new(4, 7), new(7, 7), new(7, 10), new(4, 10) },
                WallSegments: new List<WallSegment>
                {
                    new(new(4, 7), new(7, 7)),
                    new(new(7, 7), new(7, 10), HasDoor: true, DoorWidth: 1.0f),
                    new(new(7, 10), new(4, 10)),
                    new(new(4, 10), new(4, 7)),
                }),

            new("room_dining", "Dining room",
                devices.Where(d => d.RoomId == "room_dining").ToList(),
                RoomType: RoomType.Bathroom,  // Blue
                FloorPolygon: new List<Vector2> { new(7, 5), new(11, 5), new(11, 10), new(7, 10) },
                WallSegments: new List<WallSegment>
                {
                    new(new(7, 5), new(11, 5)),
                    new(new(11, 5), new(11, 10)),
                    new(new(11, 10), new(7, 10)),
                    new(new(7, 10), new(7, 5), HasDoor: true, DoorWidth: 1.0f),
                }),

            new("room_laundry", "Laundry room",
                devices.Where(d => d.RoomId == "room_laundry").ToList(),
                RoomType: RoomType.Laundry,  // Blue
                FloorPolygon: new List<Vector2> { new(11, 5), new(14, 5), new(14, 7.5f), new(11, 7.5f) },
                WallSegments: new List<WallSegment>
                {
                    new(new(11, 5), new(14, 5)),
                    new(new(14, 5), new(14, 7.5f)),
                    new(new(14, 7.5f), new(11, 7.5f)),
                    new(new(11, 7.5f), new(11, 5), HasDoor: true, DoorWidth: 0.8f),
                }),

            new("room_bathroom", "Bathroom",
                devices.Where(d => d.RoomId == "room_bathroom").ToList(),
                RoomType: RoomType.Bathroom,  // Sky blue (lighter)
                FloorPolygon: new List<Vector2> { new(11, 7.5f), new(14, 7.5f), new(14, 10), new(11, 10) },
                WallSegments: new List<WallSegment>
                {
                    new(new(11, 7.5f), new(14, 7.5f)),
                    new(new(14, 7.5f), new(14, 10)),
                    new(new(14, 10), new(11, 10)),
                    new(new(11, 10), new(11, 7.5f), HasDoor: true, DoorWidth: 0.8f),
                }),
        };
    }

    private static List<SmartDevice> CreateDevices()
    {
        return new List<SmartDevice>
        {
            // Media room
            CreateDevice("dev_tv", "Smart TV", DeviceCategory.Television, "room_media"),
            CreateDevice("dev_lamp_media", "Floor Lamp", DeviceCategory.Light, "room_media"),
            CreateDevice("dev_speaker", "Smart Speaker", DeviceCategory.Speaker, "room_media"),
            CreateDevice("dev_blinds_media", "Smart Blinds", DeviceCategory.Switch, "room_media"),

            // Deck
            CreateDevice("dev_cam_deck", "Deck Camera", DeviceCategory.Camera, "room_deck"),

            // Kitchen
            CreateDevice("dev_light_kit", "Kitchen Light", DeviceCategory.Light, "room_kitchen"),
            CreateDevice("dev_fridge", "Smart Fridge", DeviceCategory.Appliance, "room_kitchen"),
            CreateDevice("dev_cam_kit", "Kitchen Camera", DeviceCategory.Camera, "room_kitchen"),

            // Primary suite
            CreateDevice("dev_light_ps", "Bedroom Light", DeviceCategory.Light, "room_primary"),
            CreateDevice("dev_ac", "Air Conditioner", DeviceCategory.Thermostat, "room_primary"),
            CreateDevice("dev_blinds_ps", "Smart Blinds", DeviceCategory.Switch, "room_primary"),

            // Living room
            CreateDevice("dev_light_lr", "Living Light", DeviceCategory.Light, "room_living"),
            CreateDevice("dev_speaker_lr", "Speaker", DeviceCategory.Speaker, "room_living"),
            CreateDevice("dev_cam_lr", "Living Camera", DeviceCategory.Camera, "room_living"),
            CreateDevice("dev_hub", "SmartThings Hub", DeviceCategory.Hub, "room_living"),

            // Bedroom
            CreateDevice("dev_light_bed", "Bedroom Light", DeviceCategory.Light, "room_bedroom"),
            CreateDevice("dev_cam_bed", "Camera", DeviceCategory.Camera, "room_bedroom"),
            CreateDevice("dev_blinds_bed", "Blinds", DeviceCategory.Switch, "room_bedroom"),

            // Porch
            CreateDevice("dev_sensor_porch", "Motion Sensor", DeviceCategory.Sensor, "room_porch"),
            CreateDevice("dev_cam_porch", "Porch Camera", DeviceCategory.Camera, "room_porch"),

            // Dining room
            CreateDevice("dev_light_din", "Dining Light", DeviceCategory.Light, "room_dining"),
            CreateDevice("dev_cam_din", "Camera", DeviceCategory.Camera, "room_dining"),

            // Laundry
            CreateDevice("dev_washer", "Smart Washer", DeviceCategory.Appliance, "room_laundry"),
            CreateDevice("dev_thermo", "Thermostat", DeviceCategory.Thermostat, "room_laundry"),

            // Bathroom
            CreateDevice("dev_light_bath", "Bath Light", DeviceCategory.Light, "room_bathroom"),
            CreateDevice("dev_leak", "Leak Sensor", DeviceCategory.Sensor, "room_bathroom"),
        };
    }

    private static List<DevicePlacement> CreateDevicePlacements()
    {
        return new List<DevicePlacement>
        {
            // Media room (0-4, 0-5)
            new("dev_tv", "room_media", new Vector3(1.5f, 0, 1)),
            new("dev_lamp_media", "room_media", new Vector3(3.2f, 0, 1)),
            new("dev_speaker", "room_media", new Vector3(1, 0, 3)),
            new("dev_blinds_media", "room_media", new Vector3(1, 0, 4.2f)),

            // Deck (4-7, 0-3)
            new("dev_cam_deck", "room_deck", new Vector3(5.8f, 0, 1.5f)),

            // Kitchen (7-10, 0-5)
            new("dev_light_kit", "room_kitchen", new Vector3(8.5f, 0, 1)),
            new("dev_fridge", "room_kitchen", new Vector3(9, 0, 3)),
            new("dev_cam_kit", "room_kitchen", new Vector3(8, 0, 2.5f)),

            // Primary suite (10-14, 0-5)
            new("dev_light_ps", "room_primary", new Vector3(12, 0, 2.5f)),
            new("dev_ac", "room_primary", new Vector3(11, 0, 1)),
            new("dev_blinds_ps", "room_primary", new Vector3(13.2f, 0, 4)),

            // Living room (4-10, 3-7)
            new("dev_light_lr", "room_living", new Vector3(6, 0, 5.5f)),
            new("dev_speaker_lr", "room_living", new Vector3(5, 0, 4)),
            new("dev_cam_lr", "room_living", new Vector3(8, 0, 4)),
            new("dev_hub", "room_living", new Vector3(7, 0, 6)),

            // Bedroom (0-4, 5-10)
            new("dev_light_bed", "room_bedroom", new Vector3(2, 0, 7)),
            new("dev_cam_bed", "room_bedroom", new Vector3(1, 0, 6)),
            new("dev_blinds_bed", "room_bedroom", new Vector3(2.5f, 0, 9)),

            // Porch (4-7, 7-10)
            new("dev_sensor_porch", "room_porch", new Vector3(5.5f, 0, 8)),
            new("dev_cam_porch", "room_porch", new Vector3(5.5f, 0, 9.3f)),

            // Dining room (7-11, 5-10)
            new("dev_light_din", "room_dining", new Vector3(9, 0, 7.5f)),
            new("dev_cam_din", "room_dining", new Vector3(8, 0, 6.5f)),

            // Laundry (11-14, 5-7.5)
            new("dev_washer", "room_laundry", new Vector3(12.5f, 0, 6)),
            new("dev_thermo", "room_laundry", new Vector3(13, 0, 7)),

            // Bathroom (11-14, 7.5-10)
            new("dev_light_bath", "room_bathroom", new Vector3(12.5f, 0, 8.5f)),
            new("dev_leak", "room_bathroom", new Vector3(13, 0, 9.3f)),
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
