using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

using Content.Server.Light.Components; // RAYTEN

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    /// <inheritdoc />
    public override void DispatchGlobalAnnouncement(
        string message,
        string? sender = null,
        bool playSound = true,
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null
        )
    {
        sender ??= Loc.GetString("chat-manager-sender-announcement");

        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));
        _chatManager.ChatMessageToAll(ChatChannel.Radio, message, wrappedMessage, default, false, true, colorOverride);

        // RAYTEN STARTS
        if (playSound)
        {
            var soundToPlay = announcementSound ?? DefaultAnnouncementSound;
            if (soundToPlay != null)
            {
                var query = EntityManager.EntityQueryEnumerator<EmergencyLightComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out _))
                {
                    _audio.PlayPvs(soundToPlay, uid, AudioParams.Default.WithVolume(-10f));
                }
            }
        }
        // RAYTEN ENDS

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Global station announcement from {sender}: {message}");
    }

    /// <inheritdoc />
    public override void DispatchFilteredAnnouncement(
        Filter filter,
        string message,
        EntityUid? source = null,
        string? sender = null,
        bool playSound = true,
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null)
    {
        sender ??= Loc.GetString("chat-manager-sender-announcement");

        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, source ?? default, false, true, colorOverride);
        // RAYTEN STARTS
        if (playSound)
        {
            var soundToPlay = announcementSound ?? DefaultAnnouncementSound;
            if (soundToPlay != null)
            {
                var query = EntityManager.EntityQueryEnumerator<EmergencyLightComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out _))
                {
                    _audio.PlayPvs(soundToPlay, uid, AudioParams.Default.WithVolume(-10f));
                }
            }
        }
        // RAYTEN ENDS
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Station Announcement from {sender}: {message}");
    }

    /// <inheritdoc />
    public override void DispatchStationAnnouncement(
        EntityUid source,
        string message,
        string? sender = null,
        bool playDefaultSound = true,
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null)
    {
        sender ??= Loc.GetString("chat-manager-sender-announcement");

        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));
        var station = _stationSystem.GetOwningStation(source);

        if (station == null)
        {
            // you can't make a station announcement without a station
            return;
        }

        if (!TryComp<StationDataComponent>(station, out var stationDataComp)) return;

        var filter = _stationSystem.GetInStation(stationDataComp);

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, source, false, true, colorOverride);

        // RAYTEN STARTS
        if (playDefaultSound)
        {
            var soundToPlay = announcementSound ?? DefaultAnnouncementSound;
            if (soundToPlay != null)
            {
                var query = EntityManager.EntityQueryEnumerator<EmergencyLightComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out _))
                {
                    _audio.PlayPvs(soundToPlay, uid, AudioParams.Default.WithVolume(-15f));
                }
            }
        }
        // RAYTEN ENDS

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Station Announcement on {station} from {sender}: {message}");
    }
}
