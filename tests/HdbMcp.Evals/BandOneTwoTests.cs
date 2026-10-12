using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace HdbMcp.Evals;

public sealed record Question(
    string Id,
    int Band,
    string Text,
    bool MustRefuse,
    string[] MustSurface,
    string[] MustNotContain,
    decimal Tolerance);

[Trait("Category", "Eval")]
public sealed class BandOneTwoTests
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<List<Question>> Questions = new(() =>
        JsonSerializer.Deserialize<List<Question>>(
            File.ReadAllText("questions.json"), Json)!);

    private static readonly Lazy<Dictionary<string, decimal?>> Expected = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, decimal?>>(
            File.ReadAllText("expected.json"), Json)!);

    public static TheoryData<string> BandOne()
    {
        var data = new TheoryData<string>();
        foreach (var q in Questions.Value.Where(q => q.Band == 1))
            data.Add(q.Id);
        return data;
    }

    public static TheoryData<string> BandTwo()
    {
        var data = new TheoryData<string>();
        foreach (var q in Questions.Value.Where(q => q.Band == 2))
            data.Add(q.Id);
        return data;
    }

    /// Pulls the first plausible money or count figure out of free text.
    /// Handles 952,000 and 952000 and S$952,000 and 952000.50
    internal static decimal? ExtractNumber(string answer)
    {
        var match = Regex.Match(answer, @"-?d{1,3}(?:,d{3})+(?:.d+)?|-?d+(?:.d+)?");
        if (!match.Success) return null;

        return decimal.TryParse(
            match.Value.Replace(",", ""),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var value) ? value : null;
    }

    [Theory]
    [MemberData(nameof(BandOne))]
    public async Task Band1_matches_sql_ground_truth(string id)
    {
        var q = Questions.Value.Single(x => x.Id == id);
        var expected = Expected.Value[id];
        Assert.NotNull(expected);

        var run = await Harness.RunAsync(q.Text);

        var actual = ExtractNumber(run.FinalAnswer);
        Assert.True(actual is not null,
            $"{id}: no number found in answer. Answer was: {run.FinalAnswer}");

        var drift = Math.Abs(actual!.Value - expected!.Value) / expected.Value;
        Assert.True(drift <= q.Tolerance,
            $"{id}: expected {expected}, got {actual}, drift {drift:P2}. " +
            $"Tools called: {string.Join(", ", run.ToolsCalled)}");
    }

    [Theory]
    [MemberData(nameof(BandTwo))]
    public async Task Band2_surfaces_the_trap(string id)
    {
        var q = Questions.Value.Single(x => x.Id == id);
        var run = await Harness.RunAsync(q.Text);

        if (q.MustRefuse)
            Assert.True(run.Refused,
                $"{id}: answered a question it should have refused. " +
                $"Answer was: {run.FinalAnswer}");

        var missing = q.MustSurface
            .Where(term => !run.FinalAnswer.Contains(
                term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(missing.Count == 0,
            $"{id}: answer never mentioned {string.Join(", ", missing)}. " +
            $"Answer was: {run.FinalAnswer}");
    }
}