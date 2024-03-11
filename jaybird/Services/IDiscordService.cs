namespace jaybird.Services;
using Models;

public interface IDiscordService
{
    void Initialize();
    void UpdatePresence(string details, string state, string largeImageKey, string smallImageKey, string smallImageText, Station currentStation, string[] stationNames);
    void Shutdown();
}