
using System.ComponentModel;
using HdbMcp.Server.Data;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public sealed class TransactionTools(HdbRepository repo)
{
    [McpServerTool(Name = "query_transactions")]
    [Description("Returns individual resale transactions matching the " +
        "filters. Results are capped; use offset to page. Every row includes " +
        "date_basis so the approval/registration distinction stays visible.")]
    public async Task<object> QueryTransactions(
        string[]? town = null,
        string? flatType = null,
        [Description("Inclusive, format yyyy-MM")] string? fromMonth = null,
        [Description("Inclusive, format yyyy-MM")] string? toMonth = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new Dictionary<string, object?>();

        if (town is not null)      { where.Add("town = @town");           p["@town"] = town; }
        if (flatType is not null)  { where.Add("flat_type = @flatType");  p["@flatType"] = flatType; }
        if (fromMonth is not null) { where.Add("month_text >= @from");    p["@from"] = fromMonth; }
        if (toMonth is not null)   { where.Add("month_text <= @to");      p["@to"] = toMonth; }

        var clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        p["@offset"] = offset;
        p["@limit"] = Math.Clamp(limit, 1, 200);

        var sql = $"""
            SELECT month_text, date_basis, town, flat_type, block, street_name,
                   storey_range, floor_area_sqm, flat_model,
                   remaining_lease_years, resale_price
            FROM dbo.resale_transactions
            {clause}
            ORDER BY month_text DESC, resale_price DESC
            OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
            """;

        var rows = await repo.QueryAsync(sql, p, maxRows: 200, ct: ct);
        return new { count = rows.Count, offset, rows };
    }
}