namespace jaybird.Models;

public class UserSettings
{
    public Station LastStation { get; set; } = Station.TripleJ;
    public int LastVolume { get; set; } = 100;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}