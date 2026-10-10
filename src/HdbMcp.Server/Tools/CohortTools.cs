using System.ComponentModel;
using System.Globalization;
using HdbMcp.Server.Data;
using HdbMcp.Server.Domain;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public sealed class CohortTools(HdbRepository repo)
{
    private static DateOnly Parse(string yyyyMM) =>
        DateOnly.ParseExact(yyyyMM + "-01", "yyyy-MM-dd",
                            CultureInfo.InvariantCulture);
    private static object Delta(decimal? a, decimal? b)
        => a is null || b is null || b.Value == 0m
            ? new { absolute = (decimal?)null, pct = (decimal?)null, higher = "unknown" }
            : new
            {
                absolute = (decimal?)Math.Round(a.Value - b.Value, 2),
                pct = (decimal?)Math.Round((a.Value - b.Value) / b.Value * 100m, 1),
                higher = a.Value > b.Value ? "cohortA"
                       : a.Value < b.Value ? "cohortB"
                       : "equal"
            };

    private static decimal? Dec(IReadOnlyDictionary<string, object?>? row, string col)
        => row is not null && row.TryGetValue(col, out var v) && v is not null
            ? Convert.ToDecimal(v)
            : null;

    [McpServerTool(Name = "compare_cohorts")]
    [Description("Median, mean, quartiles and price per square metre for a " +
        "filtered set of transactions. Refuses any date range that spans the " +
        "March 2012 change in how transactions are dated.")]
    public async Task<object> PriceSummary(
        [Description("Inclusive, format yyyy-MM")] string fromMonth,
        [Description("Inclusive, format yyyy-MM")] string toMonth,
        string? townA = null,
        string? townB = null,
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

        var whereA = new List<string>
            { "month_text >= @from", "month_text <= @to" };
        var whereB = new List<string>
            { "month_text >= @from", "month_text <= @to" };
        var paramA = new Dictionary<string, object?>
            { ["@from"] = fromMonth, ["@to"] = toMonth };
        var paramB = new Dictionary<string, object?>
            { ["@from"] = fromMonth, ["@to"] = toMonth };

        if (townA is not null)     { whereA.Add("town = @townA");          paramA["@townA"] = townA; }
        if (flatType is not null) { whereA.Add("flat_type = @flatType"); paramA["@flatType"] = flatType; }

        if (townB is not null)     { whereB.Add("town = @townB");          paramB["@townB"] = townB; }
        if (flatType is not null) { whereB.Add("flat_type = @flatType"); paramB["@flatType"] = flatType; }

        var sqlA = $"""
            SELECT DISTINCT
                town + ' ' + flat_type AS town_flat_type,
                COUNT(*) OVER (
                    PARTITION BY town, flat_type
                ) AS transactions,
                PERCENTILE_CONT(0.50)
                    WITHIN GROUP (ORDER BY resale_price)
                    OVER (PARTITION BY town, flat_type) AS median_resale_price,
                AVG(resale_price / NULLIF(floor_area_sqm, 0))
                    OVER (PARTITION BY town, flat_type) AS mean_price_per_sqm,
                AVG(floor_area_sqm)
                    OVER (PARTITION BY town, flat_type) AS mean_floor_area_sqm
            FROM dbo.resale_transactions
                        WHERE {string.Join(" AND ", whereA)}
            ORDER BY town_flat_type;

            """;
            
        var sqlB = $"""
            SELECT DISTINCT
                town + ' ' + flat_type AS town_flat_type,
                COUNT(*) OVER (
                    PARTITION BY town, flat_type
                ) AS transactions,
                PERCENTILE_CONT(0.50)
                    WITHIN GROUP (ORDER BY resale_price)
                    OVER (PARTITION BY town, flat_type) AS median_resale_price,
                AVG(resale_price / NULLIF(floor_area_sqm, 0))
                    OVER (PARTITION BY town, flat_type) AS mean_price_per_sqm,
                AVG(floor_area_sqm)
                    OVER (PARTITION BY town, flat_type) AS mean_floor_area_sqm
            FROM dbo.resale_transactions
                        WHERE {string.Join(" AND ", whereB)}
            ORDER BY town_flat_type;
            """;
        var rowsA = await repo.QueryAsync(sqlA, paramA, maxRows: 1, ct: ct);
        var rowsB = await repo.QueryAsync(sqlB, paramB, maxRows: 1, ct: ct);


        var rowA = rowsA.FirstOrDefault();
        var rowB = rowsB.FirstOrDefault();

        if (rowA is null || rowB is null)
            return new
            {
                refused = true,
                reason = "empty_cohort",
                explanation = "One or both cohorts matched no transactions. " +
                    "Call list_dimensions to check valid town and flat_type values.",
                cohortAEmpty = rowA is null,
                cohortBEmpty = rowB is null
            };


        string Label(string town) =>
            $"{town}{(flatType is null ? "" : " " + flatType)} {fromMonth} to {toMonth}";



        
        return new
        {
            refused = false,
            dateBasis = DateBasis.For(from),
            baseline = "cohortB",
            cohortA = new
            {
                label = Label(townA),
                transactions = Dec(rowsA.FirstOrDefault(), "transactions"),
                medianPrice = Dec(rowsA.FirstOrDefault(), "median_resale_price"),
                meanPricePerSqm = Dec(rowsA.FirstOrDefault(), "mean_price_per_sqm"),
                meanFloorAreaSqm = Dec(rowsA.FirstOrDefault(), "mean_floor_area_sqm")
            },
            cohortB = new
            {
                label = Label(townB),
                transactions = Dec(rowsB.FirstOrDefault(), "transactions"),
                medianPrice = Dec(rowsB.FirstOrDefault(), "median_resale_price"),
                meanPricePerSqm = Dec(rowsB.FirstOrDefault(), "mean_price_per_sqm"),
                meanFloorAreaSqm = Dec(rowsB.FirstOrDefault(), "mean_floor_area_sqm")
            },
            delta = new
            {
                medianPrice =Delta(
                    Dec(rowsA.FirstOrDefault(), "median_resale_price"),
                    Dec(rowsB.FirstOrDefault(), "median_resale_price")
                ),
                mean_price_per_sqm = Delta(
                    Dec(rowsA.FirstOrDefault(), "mean_price_per_sqm"),
                    Dec(rowsB.FirstOrDefault(), "mean_price_per_sqm")
                ),
            }
        };
    }
}