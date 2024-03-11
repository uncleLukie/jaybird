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

    public void UpdatePresence(string details, string state, string largeImageKey, string smallImageKey)
    {
        var presence = new RichPresence()
        {
            Details = details,
            State = state,
            Assets = new Assets()
            {
                LargeImageKey = largeImageKey,
                SmallImageKey = smallImageKey
            }
        };

        _client.SetPresence(presence);
    }

    public void Shutdown()
    {
        _client.Dispose();
    }
}