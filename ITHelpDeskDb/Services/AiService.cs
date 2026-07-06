using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Models.DTOs.Responses;
using System.Text;
using System.Text.Json;

namespace ITHelpDeskDb.Services;

public class AiService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(ParsedTicketResponse? result, string? error)> ParseTicketAsync(
        ParseTicketRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RawText))
            return (null, "No text provided.");

        var apiKey = _config["Groq:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return (null, "AI service is not configured.");

        var managersList = req.AvailableManagers?.Any() == true
            ? string.Join(", ", req.AvailableManagers)
            : "none available";

        var systemPrompt = $@"You are a helpdesk ticket parser. Given a spoken or typed description
of an IT issue, extract structured fields. Respond ONLY with valid JSON, no markdown, no explanation.

Categories must be exactly one of: Hardware, Software, Network, Email, Access, Other
Priorities must be exactly one of: Low, Medium, High, Critical

Available managers (exact names): {managersList}

If the person mentions a manager by name (even a partial name, nickname, or first name),
match it to the closest name from the available managers list above and put that EXACT
name in ""managerName"". If no manager is mentioned or no confident match exists, set
""managerName"" to null. Do not invent a manager name that isn't in the list.

JSON format:
{{
  ""title"": ""short summary, max 10 words"",
  ""description"": ""cleaned up, full sentence description"",
  ""category"": ""one of the categories above"",
  ""priority"": ""one of the priorities above"",
  ""managerName"": ""exact name from the list above, or null""
}}";

        var body = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = req.RawText },
            },
            temperature = 0.2,
            response_format = new { type = "json_object" },
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await client.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (null, $"AI service error: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var parsed = JsonSerializer.Deserialize<ParsedTicketResponse>(
                content!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return (parsed, null);
        }
        catch (Exception ex)
        {
            return (null, $"Unexpected error: {ex.Message}");
        }
    }
}