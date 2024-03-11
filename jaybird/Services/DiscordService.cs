using DiscordRPC;
using jaybird.Models;
using System;

namespace jaybird.Services;

public class DiscordService : IDiscordService
{
    private DiscordRpcClient _client;

    public DiscordService(string applicationId)
    {
        _client = new DiscordRpcClient(applicationId);
    }

    public void Initialize()
    {
        _client.Initialize();
    }

    public void UpdatePresence(string details, string state, string largeImageKey, string smallImageKey, string smallImageText, Station currentStation, string[] stationNames)
    {
        var presence = new RichPresence()
        {
            Details = details,
            State = state,
            Assets = new Assets()
            {
                LargeImageKey = largeImageKey,
                SmallImageKey = smallImageKey,
                SmallImageText = $"Tuned into: {stationNames[(int)currentStation]}"
            },
            Buttons = new Button[]
            {
                new Button() { Label = "Get jaybird here <3", Url = "https://github.com/uncleLukie/jaybird" }
            }
        };

        _client.SetPresence(presence);
    }

    public void Shutdown()
    {
        _client.Dispose();
    }
}