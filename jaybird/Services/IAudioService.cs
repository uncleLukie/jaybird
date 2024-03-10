namespace jaybird.Services
{
    public interface IAudioService
    {
        Task PlayStream(string streamUrl);
        Task StopStream();
        // TODO: volume controls here pls
    }
}