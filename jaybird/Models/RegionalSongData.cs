namespace jaybird.Models;

public class RegionalSongData : SongData
{
    public Region Region { get; set; }
    public TimeSpan? Delay { get; set; }
    public DateTime OriginalAirTime { get; set; }
    public bool IsLive { get; set; }
    public string RegionDisplayName => Region.GetDisplayName();
    
    public string GetDelayDisplay()
    {
        if (IsLive)
            return "LIVE";
            
        if (!Delay.HasValue || Delay.Value == TimeSpan.Zero)
            return "";
            
        var delay = Delay.Value;
        if (delay.TotalHours >= 1)
        {
            var hours = (int)delay.TotalHours;
            var minutes = delay.Minutes;
            return minutes > 0 ? $"-{hours}h {minutes}m" : $"-{hours}h";
        }
        else if (delay.TotalMinutes >= 1)
        {
            return $"-{delay.Minutes}m";
        }
        else
        {
            return $"-{delay.Seconds}s";
        }
    }
    
    public DateTime GetLocalAirTime()
    {
        if (IsLive || !Delay.HasValue)
            return PlayedTime;
            
        return PlayedTime.Add(Delay.Value);
    }
}