using System.ComponentModel;
using System.Globalization;
using HdbMcp.Server.Data;
using HdbMcp.Server.Domain;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public sealed class LeaseTools(HdbRepository repo)
{
    private static DateOnly Parse(string yyyyMM) =>
        DateOnly.ParseExact(yyyyMM + "-01", "yyyy-MM-dd",
                            CultureInfo.InvariantCulture);
                            
    [McpServerTool(Name = "lease_decay_analysis")]
    [Description("Price per square metre grouped into 5-year remaining-lease " +
        "bands, so the shape of lease decay is visible rather than asserted. " +
        "remaining_lease is absent from the older datasets, so this covers a " +
        "shorter period than the rest of the server.")]
    public async Task<object> RemainingLease(
        [Description("Inclusive, format yyyy-MM")] string fromMonth,
        [Description("Inclusive, format yyyy-MM")] string toMonth,
        CancellationToken ct = default)
    {
        var from = Parse(fromMonth);
        var to = Parse(toMonth);

        var where = new List<string>
            { "month_text >= @from", "month_text <= @to" };
        var p = new Dictionary<string, object?>
            { ["@from"] = fromMonth, ["@to"] = toMonth };

        var sql = $"""
            SELECT
                FLOOR(remaining_lease_years / 5) * 5 AS lease_band_start,
                COUNT(*)                             AS transactions,
                AVG(resale_price / NULLIF(floor_area_sqm, 0)) AS mean_price_per_sqm
            FROM dbo.resale_transactions
            WHERE remaining_lease_years IS NOT NULL
            AND  {string.Join(" AND ", where)}
            GROUP BY FLOOR(remaining_lease_years / 5) * 5
            ORDER BY lease_band_start;

            """;

        var rows = await repo.QueryAsync(sql, p, maxRows: 50, ct: ct);
        return new
        {
            refused = false,
            dateBasis = DateBasis.For(from),
            filters = new { fromMonth, toMonth },
            bands = rows.Select(r => new
            {   
                leaseBandStart = Convert.ToInt32(r["lease_band_start"]),
                leaseBandLabel = $"{Convert.ToInt32(r["lease_band_start"])} to {Convert.ToInt32(r["lease_band_start"]) + 4} years",
                transactions = r["transactions"],
                meanPricePerSqm = r["mean_price_per_sqm"]
            })
        };
    }
}