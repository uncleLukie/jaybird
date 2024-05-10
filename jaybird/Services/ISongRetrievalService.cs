namespace jaybird.Services;

using Models;

public interface ISongRetrievalService
{
    Task<SongData?> GetCurrentSongAsync(Station currentStation);
}