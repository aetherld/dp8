using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<SummaryModel> _logger;

    public SummaryModel(IConnectionMultiplexer redis, ILogger<SummaryModel> logger)
    {
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    public List<TextEvaluation> Evaluations { get; set; } = new();
    public string CurrentEvaluationId { get; set; }
    public string CurrentAuthorId { get; set; }

    public class TextEvaluation
    {
        public string Id { get; set; }
        public double? Rank { get; set; }
        public double Similarity { get; set; }
        public string AuthorId { get; set; }
    }

    public IActionResult OnGet(string id = null)
    {
        if (!User.Identity.IsAuthenticated)
        {
            _logger.LogWarning("Попытка доступа к Summary без аутентификации");
            return RedirectToPage("/Login");
        }

        try
        {
            CurrentEvaluationId = id;
            CurrentAuthorId = User.Identity.Name;

            if (!string.IsNullOrEmpty(id))
            {
                _logger.LogInformation($"Пользователь {User.Identity.Name} запросил оценку: {id}");
                return HandleSingleEvaluation(id);
            }

            _logger.LogInformation($"Пользователь {User.Identity.Name} запросил список всех оценок");
            LoadUserEvaluations();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при загрузке оценок для пользователя {User.Identity.Name}");
            TempData["ErrorMessage"] = "Произошла ошибка при загрузке данных";
            return RedirectToPage("/Index");
        }
    }

    private IActionResult HandleSingleEvaluation(string id)
    {
        var authorKey = "AUTHOR-" + id;
        var author = _redisDb.StringGet(authorKey);

        if (!author.HasValue)
        {
            _logger.LogWarning($"Оценка {id} не найдена в Redis");
            return NotFound();
        }

        if (author != User.Identity.Name)
        {
            _logger.LogWarning($"Попытка доступа! Пользователь {User.Identity.Name} пытается получить доступ к оценке {id}, принадлежащей {author}");
            return Forbid();
        }

        LoadEvaluation(id);
        return Page();
    }

    private void LoadUserEvaluations()
    {
        try
        {
            var server = _redisDb.Multiplexer.GetServer(_redisDb.Multiplexer.GetEndPoints().First());
            var authorKeys = server.Keys(pattern: "AUTHOR-*");

            foreach (var key in authorKeys)
            {
                var evaluationId = key.ToString().Replace("AUTHOR-", "");
                var author = _redisDb.StringGet(key);

                if (author == User.Identity.Name)
                {
                    LoadEvaluation(evaluationId, author.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при загрузке списка оценок для пользователя {User.Identity.Name}");
            throw;
        }
    }

    private void LoadEvaluation(string id, string authorId = null)
    {
        var eval = new TextEvaluation
        {
            Id = id,
            AuthorId = authorId ?? _redisDb.StringGet("AUTHOR-" + id)
        };

        var rankValue = _redisDb.StringGet("RANK-" + id);
        if (rankValue.HasValue && double.TryParse(rankValue, out double rank))
        {
            eval.Rank = rank;
        }

        var similarityValue = _redisDb.StringGet("SIMILARITY-" + id);
        eval.Similarity = similarityValue.HasValue && double.TryParse(similarityValue, out double s) ? s : 0;

        Evaluations.Add(eval);
    }
}