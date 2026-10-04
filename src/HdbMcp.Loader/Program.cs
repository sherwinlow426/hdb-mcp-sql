using System.Data;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;

var snapshot = args.Length > 0
    ? args[0]
    : Path.Combine("..", "..", "data", "snapshot-2026-10-05");

var cs = Environment.GetEnvironmentVariable("HDB_CS")
    ?? "Server=localhost,1433;Database=hdb;User Id=sa;" +
       "Password=Dev_Passw0rd!;TrustServerCertificate=True";

static decimal? ParseRemainingLease(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return null;
    raw = raw.Trim();

    // plain number, e.g. "61" or "61.5"
    if (decimal.TryParse(raw, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var plain))
        return plain;

    // "61 years 04 months" or "61 years"
    var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    decimal years = 0, months = 0;
    for (int i = 0; i < parts.Length - 1; i++)
    {
        if (!decimal.TryParse(parts[i], out var n)) continue;
        var unit = parts[i + 1].ToLowerInvariant();
        if (unit.StartsWith("year")) years = n;
        if (unit.StartsWith("month")) months = n;
    }
    return years + months / 12m;
}

static (short? min, short? max) ParseStorey(string raw)
{
    var parts = raw.Split(" TO ", StringSplitOptions.TrimEntries);
    if (parts.Length == 2
        && short.TryParse(parts[0], out var lo)
        && short.TryParse(parts[1], out var hi))
        return (lo, hi);
    return (null, null);
}

var table = new DataTable();
foreach (var c in new[] { "source_dataset","month_text","town","flat_type",
                          "block","street_name","storey_range","flat_model",
                          "remaining_lease_raw","date_basis" })
    table.Columns.Add(c, typeof(string));
table.Columns.Add("month_start", typeof(DateTime));
table.Columns.Add("storey_min", typeof(short));
table.Columns.Add("storey_max", typeof(short));
table.Columns.Add("lease_commence_year", typeof(short));
table.Columns.Add("floor_area_sqm", typeof(decimal));
table.Columns.Add("remaining_lease_years", typeof(decimal));
table.Columns.Add("resale_price", typeof(decimal));

var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
    { HeaderValidated = null, MissingFieldFound = null };

var cutover = new DateTime(2012, 3, 1);
var files = Directory.GetFiles(snapshot, "*.csv");

foreach (var file in files)
{
    var name = Path.GetFileNameWithoutExtension(file);
    using var reader = new StreamReader(file);
    using var csv = new CsvReader(reader, cfg);
    csv.Read(); csv.ReadHeader();

    int rows = 0;
    while (csv.Read())
    {
        var monthText = csv.GetField("month")!.Trim();
        var monthStart = DateTime.ParseExact(monthText, "yyyy-MM",
            CultureInfo.InvariantCulture);

        var storeyRaw = csv.GetField("storey_range")!.Trim().ToUpperInvariant();
        var (smin, smax) = ParseStorey(storeyRaw);
        var leaseRaw = csv.GetField("remaining_lease");

        var r = table.NewRow();
        r["source_dataset"] = name;
        r["month_text"] = monthText;
        r["month_start"] = monthStart;
        r["date_basis"] = monthStart < cutover ? "approval" : "registration";
        r["town"] = csv.GetField("town")!.Trim();
        r["flat_type"] = csv.GetField("flat_type")!.Trim();
        r["block"] = (object?)csv.GetField("block")?.Trim() ?? DBNull.Value;
        r["street_name"] = (object?)csv.GetField("street_name")?.Trim() ?? DBNull.Value;
        r["storey_range"] = storeyRaw;
        r["storey_min"] = (object?)smin ?? DBNull.Value;
        r["storey_max"] = (object?)smax ?? DBNull.Value;
        r["floor_area_sqm"] = decimal.Parse(csv.GetField("floor_area_sqm")!,
            CultureInfo.InvariantCulture);
        r["flat_model"] = (object?)csv.GetField("flat_model")?.Trim() ?? DBNull.Value;
        r["lease_commence_year"] = short.TryParse(
            csv.GetField("lease_commence_date"), out var lcy)
            ? lcy : (object)DBNull.Value;
        r["remaining_lease_raw"] = (object?)leaseRaw ?? DBNull.Value;
        r["remaining_lease_years"] =
            (object?)ParseRemainingLease(leaseRaw) ?? DBNull.Value;
        r["resale_price"] = decimal.Parse(csv.GetField("resale_price")!,
            CultureInfo.InvariantCulture);
        table.Rows.Add(r);
        rows++;
    }
    Console.WriteLine(name + ": " + rows + " rows");
}

Console.WriteLine("Total staged: " + table.Rows.Count);

using var conn = new SqlConnection(cs);
conn.Open();
using (var cmd = new SqlCommand("TRUNCATE TABLE dbo.resale_transactions", conn))
    cmd.ExecuteNonQuery();

using var bulk = new SqlBulkCopy(conn)
    { DestinationTableName = "dbo.resale_transactions", BatchSize = 10000 };
foreach (DataColumn c in table.Columns)
    bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
bulk.WriteToServer(table);

Console.WriteLine("Loaded.");