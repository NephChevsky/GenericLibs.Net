using OpenAI.Responses;

namespace OpenAILib
{
    public interface IOpenAIService
    {
        Task<OpenAIResponse> CreateResponseAsync(string query, ResponseCreationOptions options);
        Task<string?> GetRecipeAsync(string query);
    }
}
