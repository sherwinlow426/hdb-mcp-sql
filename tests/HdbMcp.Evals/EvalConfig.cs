using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;

namespace HdbMcp.Evals;

public static class EvalConfig
{
    private static readonly IConfiguration Config =
        new ConfigurationBuilder()
            .AddUserSecrets<BandOneTwoTests>()
            .AddEnvironmentVariables()
            .Build();

    public static string ModelName =>
        Config["EVAL_MODEL_NAME"] ?? "gpt-4o-mini";

    public static string GraderModelName =>
        Config["EVAL_GRADER_MODEL_NAME"] ?? ModelName;

    public static IChatClient CreateChatClient(string modelName)
    {
        var apiKey = Config["EVAL_API_KEY"]
            ?? throw new InvalidOperationException("EVAL_API_KEY is not set");

        return new OpenAIClient(new ApiKeyCredential(apiKey))
            .GetChatClient(modelName)
            .AsIChatClient();
    }
}