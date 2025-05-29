using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using NATS.Client;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _redisDb;
    private readonly IConnection _natsConnection;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis, IConnection natsConnection)
    {
        _logger = logger;
        _redisDb = redis.GetDatabase();
        _natsConnection = natsConnection;
    }

    public void OnGet() { }

    public IActionResult OnPost(string text)
    {

        _logger.LogDebug(text);

        if (string.IsNullOrWhiteSpace(text))
            return Page();

        if (!User.Identity.IsAuthenticated)
            return RedirectToPage("/Login");

        string id = Guid.NewGuid().ToString();

        _redisDb.StringSet($"AUTHOR-{id}", User.Identity.Name);

        string similarityKey = "SIMILARITY-" + id;
        double similarity = CalculateSimilarity(text);
        _redisDb.StringSet(similarityKey, similarity);

        // Публикация события SimilarityCalculated
        var similarityEvent = new
        {
            EventType = "SimilarityCalculated",
            TextId = id,
            Similarity = similarity
        };
        _natsConnection.Publish("events.similarity", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(similarityEvent)));

        string textKey = "TEXT-" + id;
        _redisDb.StringSet(textKey, text);

        string textHash = ComputeHash(text);
        _redisDb.StringSet("HASH-" + textHash, text);

        var message = new
        {
            Id = id,
            TextKey = textKey
        };

        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        _natsConnection.Publish("valuator.processing.rank", data);

        return Redirect($"summary?id={id}");
    }

    private double CalculateSimilarity(string text)
    {
        string textHash = ComputeHash(text);
        return _redisDb.KeyExists("HASH-" + textHash) ? 1 : 0;
    }

    private string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes);
    }
}