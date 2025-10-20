using jaybird.Models;

namespace jaybird.Services;

public class TimezoneService
{
    private readonly Dictionary<Region, TimeZoneInfo> _timezones;
    private readonly TimeZoneInfo _nswTimezone;
    
    public TimezoneService()
    {
        _timezones = new Dictionary<Region, TimeZoneInfo>
        {
            { Region.NSW, TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time") },
            { Region.ACT, TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time") },
            { Region.VIC, TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time") },
            { Region.TAS, TimeZoneInfo.FindSystemTimeZoneById("Tasmania Standard Time") },
            { Region.QLD, TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time") },
            { Region.WA, TimeZoneInfo.FindSystemTimeZoneById("W. Australia Standard Time") },
            { Region.SA, TimeZoneInfo.FindSystemTimeZoneById("Cen. Australia Standard Time") },
            { Region.NT, TimeZoneInfo.FindSystemTimeZoneById("AUS Central Standard Time") }
        };
        
        _nswTimezone = _timezones[Region.NSW];
    }
    
    public TimeSpan GetDelayForRegion(Region region)
    {
        // NSW/ACT/VIC/TAS are always live (same timezone group with DST)
        if (region == Region.NSW || region == Region.ACT || region == Region.VIC || region == Region.TAS)
            return TimeSpan.Zero;

        // For other regions including QLD, calculate delay based on current timezone
        // QLD doesn't observe DST, so it's LIVE when NSW isn't in DST, but 1hr behind during DST
        var regionTimezone = _timezones[region];
        var now = DateTime.UtcNow;

        var nswTime = TimeZoneInfo.ConvertTimeFromUtc(now, _nswTimezone);
        var regionTime = TimeZoneInfo.ConvertTimeFromUtc(now, regionTimezone);

        var delay = nswTime - regionTime;

        // Ensure we don't return negative delays (shouldn't happen, but just in case)
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }
    
    public bool IsDaylightSavingsTime(Region region)
    {
        var timezone = _timezones[region];
        return timezone.IsDaylightSavingTime(DateTime.UtcNow);
    }
    
    public bool IsQldOnDaylightSavingsTime()
    {
        return IsDaylightSavingsTime(Region.QLD);
    }
    
    public DateTime ConvertToRegionTime(DateTime utcTime, Region region)
    {
        var timezone = _timezones[region];
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, timezone);
    }
    
    public DateTime ConvertFromRegionTime(DateTime regionTime, Region region)
    {
        var timezone = _timezones[region];
        return TimeZoneInfo.ConvertTimeToUtc(regionTime, timezone);
    }
    
    public string GetDelayDisplay(Region region)
    {
        var delay = GetDelayForRegion(region);
        
        if (delay == TimeSpan.Zero)
            return "LIVE";
            
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
    
    public RegionalSongData ApplyDelayToSongData(SongData songData, Region region)
    {
        var delay = GetDelayForRegion(region);
        var isLive = delay == TimeSpan.Zero;
        
        return new RegionalSongData
        {
            Title = songData.Title,
            Artist = songData.Artist,
            Album = songData.Album,
            ArtworkUrl = songData.ArtworkUrl,
            PlayedTime = songData.PlayedTime,
            Region = region,
            Delay = delay,
            IsLive = isLive,
            OriginalAirTime = isLive ? songData.PlayedTime : songData.PlayedTime.Add(delay)
        };
    }
}