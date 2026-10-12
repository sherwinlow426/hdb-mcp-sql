namespace HdbMcp.Evals;

public sealed record EvalRun(
    string Question,
    string FinalAnswer,
    IReadOnlyList<string> ToolsCalled,
    IReadOnlyList<string> ToolResults,
    int PromptTokens,
    int CompletionTokens,
    TimeSpan Latency,
    bool Refused);

public static class Harness
{
    public static async Task<EvalRun> RunAsync(
        string question, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var toolsCalled = new List<string>();
        var toolResults = new List<string>();
        var finalAnswer = string.Empty;
        var promptTokens = 0;
        var completionTokens = 0;

        // 1. Start the MCP server as a child process over stdio and list
        //    its tools.
        // 2. Hand those tools to a chat client from EvalConfig.
        // 3. Loop: send the conversation, if the model asks for a tool,
        //    call it, append the result, repeat until it answers.
        // 4. Fill finalAnswer, and add to toolsCalled, toolResults and the
        //    token counters as you go.

        await Task.CompletedTask;
        sw.Stop();

        return new EvalRun(
            Question: question,
            FinalAnswer: finalAnswer,
            ToolsCalled: toolsCalled,
            ToolResults: toolResults,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            Latency: sw.Elapsed,
            Refused: DetectRefusal(toolResults));
    }

    /// A refusal is a structural fact, not a phrase. Your tools return
    /// refused: true in the JSON, so read that rather than searching the
    /// prose for the word "cannot". Prose matching would score a model
    /// that says "I cannot be certain" as a refusal.
    private static bool DetectRefusal(IEnumerable<string> toolResults)
    {
        const string compact = "\"refused\":true";
        const string spaced = "\"refused\": true";

        return toolResults.Any(r =>
            r.Contains(compact, StringComparison.OrdinalIgnoreCase) ||
            r.Contains(spaced, StringComparison.OrdinalIgnoreCase));
    }
}