using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;
using SmartThings.Abstraction.Interfaces;

namespace SmartThings.Tests;

public class DeviceModelsTests
{
    [Fact]
    public void SmartDevice_CanBeCreated_WithRequiredFields()
    {
        var device = new SmartDevice
        {
            DeviceId = "device-001",
            Name = "Living Room Light",
            Label = "Living Room Light",
            Category = DeviceCategory.Light,
            RoomId = "room-001",
            Capabilities = new Dictionary<string, DeviceCapabilityState>
            {
                ["switch"] = new DeviceCapabilityState("switch", "switch", "on"),
                ["brightness"] = new DeviceCapabilityState("brightness", "brightness", 80, "%")
            }
        };

        Assert.Equal("device-001", device.DeviceId);
        Assert.Equal("Living Room Light", device.Name);
        Assert.Equal(DeviceCategory.Light, device.Category);
        Assert.Equal(DeviceStatus.Online, device.Status);
        Assert.Equal(2, device.Capabilities.Count);
    }

    [Fact]
    public void DeviceCommand_CanBeCreated()
    {
        var command = new DeviceCommand(
            DeviceId: "device-001",
            CapabilityId: "switch",
            CommandName: "on");

        Assert.Equal("device-001", command.DeviceId);
        Assert.Equal("switch", command.CapabilityId);
        Assert.Equal("on", command.CommandName);
        Assert.Null(command.Arguments);
    }

    [Fact]
    public void DeviceCommand_CanHaveArguments()
    {
        var command = new DeviceCommand(
            DeviceId: "device-001",
            CapabilityId: "thermostatMode",
            CommandName: "setThermostatMode",
            Arguments: new object[] { "heat" });

        Assert.NotNull(command.Arguments);
        Assert.Single(command.Arguments);
        Assert.Equal("heat", command.Arguments[0]);
    }

    [Fact]
    public void SmartRoom_CanHoldDevices()
    {
        var device = new SmartDevice
        {
            DeviceId = "device-001",
            Name = "Light",
            Label = "Light",
            Category = DeviceCategory.Light,
            RoomId = "room-001"
        };

        var room = new SmartRoom(
            RoomId: "room-001",
            Name: "Living Room",
            Devices: new[] { device },
            Layout: new RoomLayout(
                Center: Vector3.Zero,
                Size: new Vector3(5, 3, 4),
                DevicePositions: new Dictionary<string, Vector3>
                {
                    ["device-001"] = new Vector3(1, 0, 2)
                }
            )
        );

        Assert.Equal("Living Room", room.Name);
        Assert.Single(room.Devices);
        Assert.NotNull(room.Layout);
        Assert.Single(room.Layout.DevicePositions);
    }

    [Fact]
    public void DeviceStateChangedEvent_CanBeCreated()
    {
        var evt = new DeviceStateChangedEvent(
            DeviceId: "device-001",
            CapabilityId: "switch",
            AttributeName: "switch",
            OldValue: "off",
            NewValue: "on",
            Timestamp: DateTimeOffset.UtcNow
        );

        Assert.Equal("off", evt.OldValue);
        Assert.Equal("on", evt.NewValue);
    }

    [Fact]
    public void DeviceCategory_HasExpectedValues()
    {
        Assert.Equal(0, (int)DeviceCategory.Light);
        Assert.True(Enum.IsDefined(typeof(DeviceCategory), DeviceCategory.Thermostat));
        Assert.True(Enum.IsDefined(typeof(DeviceCategory), DeviceCategory.Lock));
        Assert.True(Enum.IsDefined(typeof(DeviceCategory), DeviceCategory.Camera));
        Assert.True(Enum.IsDefined(typeof(DeviceCategory), DeviceCategory.Hub));
    }

    [Fact]
    public void DeviceStatus_HasAllStates()
    {
        var statuses = Enum.GetValues<DeviceStatus>();
        Assert.Contains(DeviceStatus.Online, statuses);
        Assert.Contains(DeviceStatus.Offline, statuses);
        Assert.Contains(DeviceStatus.Updating, statuses);
        Assert.Contains(DeviceStatus.Error, statuses);
    }
}
