namespace jaybird.Services;

public interface IAudioService
{
    bool IsInitialized { get; }
    Task<bool> InitializeAsync(IProgress<double>? progress = null);
    Task PlayStream(string streamUrl);
    Task StopStream();
}