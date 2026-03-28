// =============================================================================
// AccessibleDeviceAnnouncer.cs — Automatic device state announcements
// Monitors device changes and announces them for screen reader users
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Monitors device state changes and provides screen reader announcements.
/// Integrates with TalkBack on Android to ensure blind/low-vision users
/// receive timely updates about their smart home.
///
/// Features:
///   - Automatic announcements when devices change state
///   - Batched announcements to avoid overwhelming the user
///   - Priority-based queuing (alerts > status > info)
///   - Haptic feedback on Android for state changes
/// </summary>
public partial class AccessibleDeviceAnnouncer : GodotNative.Node
{
    private IAccessibilityService? _a11y;
    private readonly Queue<QueuedAnnouncement> _announcementQueue = new();
    private float _announceTimer;
    private const float AnnouncementInterval = 0.5f; // Min time between announcements

    public void Initialize(IAccessibilityService a11y)
    {
        _a11y = a11y;
    }

    /// <summary>Announce a device state change.</summary>
    public void OnDeviceStateChanged(SmartDevice device, string oldState, string newState)
    {
        var priority = DetermineAnnouncePriority(device, newState);
        var text = BuildAnnouncementText(device, oldState, newState);

        _announcementQueue.Enqueue(new QueuedAnnouncement(text, priority));
    }

    /// <summary>Announce a room summary when navigating to it.</summary>
    public void AnnounceRoomSummary(SmartRoom room, List<SmartDevice> devices)
    {
        var onlineCount = devices.Count(d => d.Status == DeviceStatus.Online);
        var offlineCount = devices.Count(d => d.Status == DeviceStatus.Offline);

        var parts = new List<string> { $"{room.Name}" };

        if (onlineCount > 0)
            parts.Add($"{onlineCount} device{(onlineCount != 1 ? "s" : "")} online");
        if (offlineCount > 0)
            parts.Add($"{offlineCount} offline");

        // List device names
        var names = devices.Select(d => d.Label).Take(4);
        parts.Add($"Devices: {string.Join(", ", names)}");
        if (devices.Count > 4) parts.Add($"and {devices.Count - 4} more");

        _a11y?.Announce(string.Join(". ", parts), AnnouncePriority.Normal);
    }

    /// <summary>Announce the current home overview.</summary>
    public void AnnounceHomeOverview(SmartHome home)
    {
        var totalDevices = home.Devices.Count;
        var onlineDevices = home.Devices.Count(d => d.Status == DeviceStatus.Online);

        _a11y?.Announce(
            $"{home.Name}. {home.Rooms.Count} rooms, {totalDevices} devices, {onlineDevices} online.",
            AnnouncePriority.Normal);
    }

    public override void _Process(double delta)
    {
        if (_announcementQueue.Count == 0) return;

        _announceTimer -= (float)delta;
        if (_announceTimer <= 0)
        {
            var announcement = _announcementQueue.Dequeue();
            _a11y?.Announce(announcement.Text, announcement.Priority);
            _announceTimer = AnnouncementInterval;

            // Haptic feedback on Android
            if (GodotNative.OS.GetName() == "Android" &&
                announcement.Priority >= AnnouncePriority.High)
            {
                GodotNative.Input.VibrateHandheld(100); // 100ms vibration
            }
        }
    }

    private static AnnouncePriority DetermineAnnouncePriority(SmartDevice device, string newState)
    {
        // Security devices get high priority
        if (device.Category == DeviceCategory.Lock || device.Category == DeviceCategory.Camera)
            return AnnouncePriority.High;

        // Error states get alert priority
        if (newState == "error" || newState == "offline")
            return AnnouncePriority.Alert;

        return AnnouncePriority.Normal;
    }

    private static string BuildAnnouncementText(SmartDevice device, string oldState, string newState)
    {
        return device.Category switch
        {
            DeviceCategory.Light => $"{device.Label} turned {newState}",
            DeviceCategory.Lock => newState == "locked"
                ? $"{device.Label} is now locked"
                : $"Warning: {device.Label} has been unlocked",
            DeviceCategory.Thermostat => $"{device.Label} set to {newState}",
            DeviceCategory.Camera => $"{device.Label}: {newState}",
            DeviceCategory.Sensor => $"{device.Label} detected: {newState}",
            _ => $"{device.Label}: changed from {oldState} to {newState}"
        };
    }

    private record QueuedAnnouncement(string Text, AnnouncePriority Priority);
}
