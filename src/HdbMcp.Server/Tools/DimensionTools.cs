using System.ComponentModel;
using HdbMcp.Server.Data;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public sealed class DimensionTools(HdbRepository repo)
{
    [McpServerTool(Name = "list_dimensions")]
    [Description("Lists the valid values for a categorical column, with " +
        "transaction counts and the period each value appears in. Call this " +
        "before filtering, rather than guessing values.")]
    public async Task<object> ListDimensions(
        [Description("One of: town, flat_type, flat_model, storey_range")]
        string dimension,
        CancellationToken ct = default)
    {
        var allowed = new[] { "town", "flat_type", "flat_model", "storey_range" };
        if (!allowed.Contains(dimension))
            return new { error = $"Unknown dimension '{dimension}'.",
                         allowed };

        // dimension is validated against a fixed list, never interpolated
        // from free text, so this is safe to compose.
        var sql = $"""
            SELECT {dimension} AS value,
                   COUNT(*)    AS transactions,
                   MIN(month_text) AS first_month,
                   MAX(month_text) AS last_month
            FROM dbo.resale_transactions
            GROUP BY {dimension}
            ORDER BY COUNT(*) DESC
            """;

        var rows = await repo.QueryAsync(sql, new Dictionary<string, object?>(),
                                         maxRows: 200, ct: ct);
        return new { dimension, values = rows };
    }
}