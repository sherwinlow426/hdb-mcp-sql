using System.ComponentModel;
using System.Globalization;
using HdbMcp.Server.Data;
using HdbMcp.Server.Domain;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public sealed class PriceTools(HdbRepository repo)
{
    private static DateOnly Parse(string yyyyMM) =>
        DateOnly.ParseExact(yyyyMM + "-01", "yyyy-MM-dd",
                            CultureInfo.InvariantCulture);

    [McpServerTool(Name = "price_summary")]
    [Description("Median, mean, quartiles and price per square metre for a " +
        "filtered set of transactions. Refuses any date range that spans the " +
        "March 2012 change in how transactions are dated.")]
    public async Task<object> PriceSummary(
        [Description("Inclusive, format yyyy-MM")] string fromMonth,
        [Description("Inclusive, format yyyy-MM")] string toMonth,
        string? town = null,
        string? flatType = null,
        CancellationToken ct = default)
    {
        var from = Parse(fromMonth);
        var to = Parse(toMonth);

        if (DateBasis.Crosses(from, to))
            return new
            {
                refused = true,
                reason = "date_basis_mixture",
                explanation = DateBasis.Explanation,
                suggestion = new[]
                {
                    new { fromMonth, toMonth = "2012-02", basis = "approval" },
                    new { fromMonth = "2012-03", toMonth, basis = "registration" }
                }
            };

        var where = new List<string>
            { "month_text >= @from", "month_text <= @to" };
        var p = new Dictionary<string, object?>
            { ["@from"] = fromMonth, ["@to"] = toMonth };

        if (town is not null)     { where.Add("town = @town");          p["@town"] = town; }
        if (flatType is not null) { where.Add("flat_type = @flatType"); p["@flatType"] = flatType; }

        var sql = $"""
            SELECT COUNT(*) AS transactions,
                   AVG(resale_price) AS mean_price,
                   MIN(resale_price) AS min_price,
                   MAX(resale_price) AS max_price,
                   AVG(resale_price / NULLIF(floor_area_sqm, 0)) AS mean_price_per_sqm,
                   PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY resale_price)
                       OVER () AS p25_price,
                   PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY resale_price)
                       OVER () AS median_price,
                   PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY resale_price)
                       OVER () AS p75_price
            FROM dbo.resale_transactions
            WHERE {string.Join(" AND ", where)}
            """;

        var rows = await repo.QueryAsync(sql, p, maxRows: 1, ct: ct);
        return new
        {
            refused = false,
            dateBasis = DateBasis.For(from),
            filters = new { fromMonth, toMonth, town, flatType },
            summary = rows.FirstOrDefault()
        };
    }
      [McpServerTool(Name = "price_trend")]
    [Description("Median resale price over time, yearly. Set realTerms to " +
        "true to express every year in constant 2024 dollars using CPI. " +
        "Nominal and real can tell different stories.")]
    public async Task<object> PriceTrend(
        string? town = null,
        string? flatType = null,
        bool realTerms = false,
        CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new Dictionary<string, object?>();
        if (town is not null)     { where.Add("t.town = @town");          p["@town"] = town; }
        if (flatType is not null) { where.Add("t.flat_type = @flatType"); p["@flatType"] = flatType; }
        var clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        var join = realTerms
            ? "JOIN dbo.cpi c ON c.period_start = DATEFROMPARTS(YEAR(t.month_start), 1, 1)"
            : "";
        var priceExpr = realTerms
            ? """
              AVG(t.resale_price)
                * (SELECT cpi_index FROM dbo.cpi WHERE period_start = '2024-01-01')
                / c.cpi_index
              """
            : "AVG(t.resale_price)";
        var group = realTerms
            ? "GROUP BY YEAR(t.month_start), c.cpi_index"
            : "GROUP BY YEAR(t.month_start)";

        var sql = $"""
            SELECT YEAR(t.month_start) AS year,
                   MIN(t.date_basis)   AS date_basis,
                   COUNT(*)            AS transactions,
                   {priceExpr}         AS mean_price
            FROM dbo.resale_transactions t
            {join}
            {clause}
            {group}
            ORDER BY year
            """;

        var rows = await repo.QueryAsync(sql, p, maxRows: 100, ct: ct);

        return new
        {
            basis = realTerms ? "real, constant 2024 dollars" : "nominal",
            note = realTerms
                ? "CPI is annual, All Items, 2024 = 100, from SingStat. " +
                  "The series ends at 2025, so transactions from 2026 are " +
                  "excluded from real-terms output."
                : "Nominal dollars, not adjusted for inflation. Ask for " +
                  "realTerms if comparing across years.",
            years = rows
        };
    }
}