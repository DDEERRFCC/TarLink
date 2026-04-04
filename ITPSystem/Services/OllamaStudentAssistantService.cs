using System.Text;
using System.Text.Json;
using ITPSystem.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace ITPSystem.Services;

public class OllamaStudentAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly Lazy<IReadOnlyList<KnowledgeChunk>> _knowledgeChunks;

    public OllamaStudentAssistantService(HttpClient httpClient, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _knowledgeChunks = new Lazy<IReadOnlyList<KnowledgeChunk>>(LoadKnowledgeChunks);
    }

    public async Task<string> AskAsync(StudentAssistantContext context, string question, CancellationToken cancellationToken = default)
    {
        var baseUrl = (_configuration["Ollama:BaseUrl"] ?? "http://localhost:11434").Trim().TrimEnd('/');
        var model = (_configuration["Ollama:Model"] ?? "llama3.1:8b").Trim();

        if (string.IsNullOrWhiteSpace(model))
        {
            return "AI assistant is not configured yet. Set `Ollama:Model` in configuration first.";
        }

        var prompt = BuildPrompt(context, question, GetRelevantKnowledge(question));
        var payload = new
        {
            model,
            prompt,
            stream = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/generate");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ExtractApiErrorMessage(rawBody, (int)response.StatusCode)
                    ?? "AI assistant could not answer right now. Please check the Ollama model and local server connection.";
            }

            return ExtractAssistantText(rawBody)
                ?? "AI assistant could not generate a reply right now.";
        }
        catch (HttpRequestException)
        {
            return "AI assistant could not connect to Ollama. Start Ollama locally and check `Ollama:BaseUrl`.";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "AI assistant timed out while waiting for the local model. Try a smaller Ollama model or ask a shorter question.";
        }
    }

    private string BuildPrompt(StudentAssistantContext context, string question, IReadOnlyList<KnowledgeChunk> relevantKnowledge)
    {
        var deadlines = context.Deadlines.Count == 0
            ? "No upcoming deadlines."
            : string.Join("; ", context.Deadlines.Select(d => $"{d.Title}: {d.DateText} ({d.Note})"));
        var roleScope = BuildRoleScope(context.UserRole);
        var knowledgeSection = relevantKnowledge.Count == 0
            ? "No matching local knowledge snippets were found."
            : string.Join(
                Environment.NewLine + Environment.NewLine,
                relevantKnowledge.Select(k => $"- {k.Title}: {k.Content}"));

        return
            "You are an AI assistant inside an Internship Training Programme management system used by students, supervisors, committee members, and guests on login pages. " +
            "Answer only questions related to using this system and the current user's allowed workflow. " +
            "Be practical, concise, and give step-by-step directions using page names when helpful. " +
            "Use the provided local knowledge snippets as your primary source whenever they are relevant. " +
            "If the user asks outside their allowed scope, say that this assistant for their role only supports that role's portal tasks. " +
            "If the local knowledge does not contain the answer, say that clearly and suggest contacting the committee. " +
            $"Role scope: {roleScope}. " +
            $"Current user role: {context.UserRole}. " +
            $"Student name: {context.StudentName}. " +
            $"Student ID: {context.StudentId}. " +
            $"Application status: {context.Status}. " +
            $"Cohort: {context.Cohort}. " +
            $"Intern period: {context.InternPeriod}. " +
            $"Current page: {context.CurrentPage}. " +
            $"Upcoming deadlines: {deadlines}. " +
            "Relevant pages include Index, login pages, Forgot Password, Student Dashboard, My Documents, Upload Resume / CV, My Progress Report, My Profile, Own Company Request, supervisor pages, and committee pages. " +
            Environment.NewLine + Environment.NewLine +
            "Local knowledge snippets:" +
            Environment.NewLine +
            knowledgeSection +
            Environment.NewLine + Environment.NewLine +
            $"Student question: {question}" + Environment.NewLine +
            "Assistant answer:";
    }

    private static string BuildRoleScope(string? userRole)
    {
        return (userRole ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "student" =>
                "Answer only student-related questions such as student login, forgot password, dashboard usage, profile, documents, company details, progress reports, deadlines, and student submission workflow. Do not answer supervisor-only or committee-only workflow questions.",
            "supervisor" =>
                "Answer only supervisor-related questions such as supervisor login, assigned students, reports, marks, summary reports, and supervisor workflow. Do not answer committee-only administration or student-only submission details beyond basic navigation.",
            "committee" =>
                "Answer only committee-related questions such as committee login, company management, student management, imports, cohorts, evaluations, summary reports, and committee workflow.",
            _ =>
                "Answer only general public questions such as login pages, forgot password, navigation, and high-level portal usage. Do not provide role-specific internal workflow steps unless the user is in that role."
        };
    }

    private IReadOnlyList<KnowledgeChunk> GetRelevantKnowledge(string question)
    {
        var normalizedQuestionTerms = ExtractTerms(question);
        if (normalizedQuestionTerms.Count == 0)
        {
            return _knowledgeChunks.Value.Take(3).ToList();
        }

        return _knowledgeChunks.Value
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreChunk(chunk, normalizedQuestionTerms)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.Title)
            .Take(4)
            .Select(x => x.Chunk)
            .ToList();
    }

    private static int ScoreChunk(KnowledgeChunk chunk, HashSet<string> questionTerms)
    {
        var score = 0;
        foreach (var term in questionTerms)
        {
            if (chunk.Tags.Any(tag => string.Equals(tag, term, StringComparison.OrdinalIgnoreCase)))
            {
                score += 5;
            }

            if (chunk.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }

            if (chunk.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        return score;
    }

    private static HashSet<string> ExtractTerms(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "to", "of", "for", "in", "on", "at", "is", "are", "be", "do",
            "how", "what", "where", "when", "can", "i", "my", "me", "you", "your", "it", "this", "that"
        };

        var parts = text
            .Split(new[] { ' ', '\r', '\n', '\t', ',', '.', ':', ';', '?', '!', '/', '\\', '-', '_', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(p => p.Length >= 2 && !stopWords.Contains(p));

        return parts.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<KnowledgeChunk> LoadKnowledgeChunks()
    {
        var path = Path.Combine(_hostEnvironment.ContentRootPath, "Data", "assistant-knowledge.json");
        if (!File.Exists(path))
        {
            return Array.Empty<KnowledgeChunk>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var chunks = JsonSerializer.Deserialize<List<KnowledgeChunk>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (chunks == null)
            {
                return Array.Empty<KnowledgeChunk>();
            }

            return chunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Content))
                .ToList();
        }
        catch
        {
            return Array.Empty<KnowledgeChunk>();
        }
    }

    private static string? ExtractAssistantText(string rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;

        if (root.TryGetProperty("response", out var responseElement) && responseElement.ValueKind == JsonValueKind.String)
        {
            var text = responseElement.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static string? ExtractApiErrorMessage(string rawBody, int statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
            {
                var message = errorElement.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return $"AI assistant request failed: {message}";
                }
            }

            if (statusCode == 404)
            {
                return "AI assistant could not find the configured Ollama model. Pull the model first, then try again.";
            }
        }
        catch
        {
            // Fall back to the generic error message if the response is not valid JSON.
        }

        return null;
    }
}

public class StudentAssistantContext
{
    public string UserRole { get; set; } = "guest";
    public string StudentName { get; set; } = "-";
    public string StudentId { get; set; } = "-";
    public string Status { get; set; } = "-";
    public string Cohort { get; set; } = "-";
    public string InternPeriod { get; set; } = "-";
    public string CurrentPage { get; set; } = "-";
    public List<StudentAssistantDeadlineItem> Deadlines { get; set; } = new();
}

public class StudentAssistantDeadlineItem
{
    public string Title { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public class KnowledgeChunk
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
}
