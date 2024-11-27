using Robust.Shared.Configuration;
using Robust.Shared;

namespace Content.Shared.Vanilla.CCVars;

[CVarDefs]
public sealed class CCVarsVanilla
{

    /// <summary>
    /// URL вебхука, используемого для отправки сообщений о банах на Discord сервер.
    /// </summary>
    public static readonly CVarDef<string> DiscordServerBansWebhook =
        CVarDef.Create("discord.server_bans_webhook", "", CVar.SERVERONLY);


    /*
     * Queue
     */
    /// <summary>
    ///     Controls if the connections queue is enabled. If enabled stop kicking new players after `SoftMaxPlayers` cap and instead add them to queue.
    /// </summary>
    public static readonly CVarDef<bool>
        QueueEnabled = CVarDef.Create("queue.enabled", false, CVar.SERVERONLY);
     /*
     * Discord Auth
     */

    /// <summary>
    ///     Enabled Discord linking, show linking button and modal window
    /// </summary>
    public static readonly CVarDef<bool> DiscordAuthEnabled =
        CVarDef.Create("discord_auth.enabled", false, CVar.SERVERONLY);

    /// <summary>
    ///     URL of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiUrl =
        CVarDef.Create("discord_auth.api_url", "", CVar.SERVERONLY);

    /// <summary>
    ///     Secret key of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiKey =
        CVarDef.Create("discord_auth.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);    
        CVarDef.Create("discord.server_bans_webhook", string.Empty, CVar.SERVERONLY);
        
    /// <summary>
    /// задержка между вызовами обр
    /// </summary>
    public static readonly CVarDef<int> SpecForceDelay =
        CVarDef.Create("specforce.delay", 2, CVar.SERVERONLY);
}