using FanShop.Models;
using System.Net.Http;
using System.Text.Json;

namespace FanShop.Services;

public class FirebaseService
{
    private readonly string _firebaseUrl;
    private readonly HttpClient _httpClient;

    public FirebaseService(string firebaseUrl)
    {
        _firebaseUrl = firebaseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
    }

    public async Task<List<Match>> GetMatchesAsync()
    {
        var json = await _httpClient.GetStringAsync($"{_firebaseUrl}/matches/.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var matches = JsonSerializer.Deserialize<Dictionary<string, Match>>(json, options);

        return matches?
            .Values
            .Where(m => m != null)
            .OrderBy(m => m.Time)
            .ToList() ?? new List<Match>();
    }
    
    public async Task SaveMatchAsync(Match match)
    {
        var json = JsonSerializer.Serialize(match);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        await _httpClient.PostAsync($"{_firebaseUrl}/matches/.json", content);
    }
    
    public async Task UpdateMatchAsync(string matchId, Match match)
    {
        var json = JsonSerializer.Serialize(match);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        await _httpClient.PutAsync($"{_firebaseUrl}/matches/{matchId}.json", content);
    }
    
    public async Task DeleteMatchAsync(string matchId)
    {
        await _httpClient.DeleteAsync($"{_firebaseUrl}/matches/{matchId}.json");
    }
}
