using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

namespace OpenAILib
{
    public class OpenAIService : IOpenAIService
    {
        private readonly ILogger<OpenAIService> _logger;
        private readonly OpenAIResponseClient _client;

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
        {
            _logger = logger;
            string apiKey = configuration.GetConnectionString("OpenAI") ?? throw new InvalidOperationException("OpenAI connection string is not configured");
            string model = configuration.GetSection("OpenAI").GetValue<string>("Model") ?? "gpt-4o";
            _client = new OpenAIResponseClient(model, apiKey);
        }

        public async Task<OpenAIResponse> CreateResponseAsync(string query, ResponseCreationOptions options)
        {
            _logger.LogInformation("Creating OpenAI response for query: {Query}", query);
            return await _client.CreateResponseAsync(query, options);
        }

        public async Task<string?> GetRecipeAsync(string query)
        {
            _logger.LogInformation("Getting recipe for query: {Query}", query);

            string systemPrompt = @"
                Tu es un assistant de cuisine.
                L'utilisateur peut fournir un lien ou un nom de recette.
                IMPORTANT : Retourne uniquement un JSON, sans texte, sans explications, sans guillemets autour du JSON
                IMPORTANT : N'ajoutes pas d'infos concernant:
                    - la source de la recette
                    - ton raisonement
                IMPORTANT : preparationTime et cookingTime sont au format HH:mm:ss
                IMPORTANT : la recette et les unités de quantité doivent être en français";

            ResponseTextFormat responseFormat = ResponseTextFormat.CreateJsonSchemaFormat("recipe_json_schema", BinaryData.FromString("""
                {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "name": { "type": "string" },
                    "preparationTime": { 
                      "type": "string", 
                      "pattern": "^\\d{2}:\\d{2}:\\d{2}$" 
                    },
                    "cookingTime": { 
                      "type": "string", 
                      "pattern": "^\\d{2}:\\d{2}:\\d{2}$" 
                    },
                    "servings": { 
                      "type": "integer", 
                      "minimum": 1 
                    },
                    "ingredients": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "quantity": { "type": "number" },
                          "quantityUnit": { "type": "string" },
                          "name": { "type": "string" }
                        },
                        "required": ["quantity", "quantityUnit", "name"]
                      }
                    },
                    "instructions": {
                      "type": "array",
                      "items": { "type": "string" }
                    }
                  },
                  "required": [
                    "name",
                    "preparationTime",
                    "cookingTime",
                    "servings",
                    "ingredients",
                    "instructions"
                  ]
                }
                """), jsonSchemaIsStrict: true);

            OpenAIResponse response = await _client.CreateResponseAsync(query, new ResponseCreationOptions()
            {
                Tools = { ResponseTool.CreateWebSearchTool() },
                Instructions = systemPrompt,
                TextOptions = new ResponseTextOptions()
                {
                    TextFormat = responseFormat,
                },
            });

            ResponseItem? responseItem = response.OutputItems.FirstOrDefault(i => i is MessageResponseItem);

            if (responseItem != null)
            {
                string? json = (responseItem as MessageResponseItem)?.Content?.FirstOrDefault()?.Text;
                return json;
            }

            _logger.LogWarning("No response item found for recipe query");
            return null;
        }
    }
}
