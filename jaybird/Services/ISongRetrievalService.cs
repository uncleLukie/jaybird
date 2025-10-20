namespace jaybird.Services;

using Models;

public interface ISongRetrievalService
{
    Task<SongData?> GetCurrentSongAsync(Station currentStation);
}

public interface IRegionalSongRetrievalService : ISongRetrievalService
{
    Task<RegionalSongData?> GetCurrentSongAsync(Station currentStation, Region region);
    Task<Dictionary<Region, RegionalSongData?>> GetAllRegionalSongsAsync(Station currentStation);
    void ClearEtags();
    void ClearEtagForRegion(Station station, Region region);
}