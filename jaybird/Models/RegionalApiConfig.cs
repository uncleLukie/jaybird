namespace jaybird.Models;

public class RegionalApiConfig
{
    // Regional API endpoints now use timezone parameters instead of separate URLs
    // This class is kept for backward compatibility and potential future regional stream URLs
    public required Dictionary<Station, Dictionary<Region, string>> StreamUrls { get; set; }
    
    public string GetStreamUrl(Station station, Region region)
    {
        if (StreamUrls.TryGetValue(station, out var stationStreams) &&
            stationStreams.TryGetValue(region, out var streamUrl))
        {
            return streamUrl;
        }
        
        // Fallback to NSW if specific region not found
        if (StreamUrls.TryGetValue(station, out var nswStreams) &&
            nswStreams.TryGetValue(Region.NSW, out var nswStreamUrl))
        {
            return nswStreamUrl;
        }
        
        throw new ArgumentException($"No stream URL found for station {station} in region {region}");
    }
}