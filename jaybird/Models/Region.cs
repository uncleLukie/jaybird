namespace jaybird.Models;

public enum Region
{
    NSW,    // LIVE (AEDT/AEST)
    ACT,    // LIVE (AEDT/AEST)
    VIC,    // LIVE (AEDT/AEST)
    TAS,    // LIVE (AEDT/AEST)
    QLD,    // CONDITIONAL: LIVE during non-DST, 1hr behind during DST (AEST year-round)
    WA,     // DELAY (AWST)
    SA,     // DELAY (ACDT/ACST)
    NT      // DELAY (ACST year-round)
}

public static class RegionExtensions
{
    public static string GetDisplayName(this Region region)
    {
        return region switch
        {
            Region.NSW => "NSW",
            Region.ACT => "ACT", 
            Region.VIC => "VIC",
            Region.TAS => "TAS",
            Region.QLD => "QLD",
            Region.WA => "WA",
            Region.SA => "SA",
            Region.NT => "NT",
            _ => region.ToString()
        };
    }
    
    public static string GetFullName(this Region region)
    {
        return region switch
        {
            Region.NSW => "New South Wales",
            Region.ACT => "Australian Capital Territory",
            Region.VIC => "Victoria", 
            Region.TAS => "Tasmania",
            Region.QLD => "Queensland",
            Region.WA => "Western Australia",
            Region.SA => "South Australia",
            Region.NT => "Northern Territory",
            _ => region.ToString()
        };
    }
    
    public static string GetTimezone(this Region region)
    {
        return region switch
        {
            Region.NSW => "Australia/Sydney",
            Region.ACT => "Australia/Canberra",
            Region.VIC => "Australia/Melbourne",
            Region.TAS => "Australia/Hobart",
            Region.QLD => "Australia/Brisbane",
            Region.WA => "Australia/Perth",
            Region.SA => "Australia/Adelaide",
            Region.NT => "Australia/Darwin",
            _ => "Australia/Sydney"
        };
    }
    
    // Returns true for regions that are always live (NSW/ACT/VIC/TAS)
    // Note: QLD is conditionally live but returns false here - delay is calculated dynamically in TimezoneService
    public static bool IsLiveRegion(this Region region)
    {
        return region switch
        {
            Region.NSW => true,
            Region.ACT => true,
            Region.VIC => true,
            Region.TAS => true,
            _ => false  // QLD, WA, SA, NT are handled dynamically
        };
    }
}