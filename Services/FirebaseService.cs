using Firebase.Database;
using Firebase.Database.Query;
using FanShop.Models;

namespace FanShop.Services;

public class FirebaseService
{
    private FirebaseClient _firebaseClient;

    public FirebaseService(string firebaseUrl)
    {
        _firebaseClient = new FirebaseClient(firebaseUrl);
    }

    public async Task<List<Match>> GetMatchesAsync()
    {
        var matches = await _firebaseClient
            .Child("matches")
            .OnceAsync<Match>();
        
        return matches.Select(m => m.Object).ToList();
    }
    
    public async Task SaveMatchAsync(Match match)
    {
        await _firebaseClient
            .Child("matches")
            .PostAsync(match);
    }
    
    public async Task UpdateMatchAsync(string matchId, Match match)
    {
        await _firebaseClient
            .Child("matches")
            .Child(matchId)
            .PutAsync(match);
    }
    
    public async Task DeleteMatchAsync(string matchId)
    {
        await _firebaseClient
            .Child("matches")
            .Child(matchId)
            .DeleteAsync();
    }
}