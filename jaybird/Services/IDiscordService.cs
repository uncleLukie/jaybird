namespace jaybird.Services;

public interface IDiscordService
{
    void Initialize();
    void UpdatePresence(string details, string state, string largeImageKey, string smallImageKey);
    void Shutdown();
}