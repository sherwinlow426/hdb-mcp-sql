using Microsoft.Data.SqlClient;

public static class GroundTruth
{
    public static async Task<Dictionary<string, decimal?>> ComputeAsync(
        string connectionString, string gtFolder, CancellationToken ct = default)
    {
        var results = new Dictionary<string, decimal?>();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        foreach (var file in Directory.GetFiles(gtFolder, "*.sql").Order())
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var sql = await File.ReadAllTextAsync(file, ct);

            await using var cmd = new SqlCommand(sql, conn);
            var value = await cmd.ExecuteScalarAsync(ct);

            results[id] = value is null or DBNull
                ? null
                : Convert.ToDecimal(value);

            Console.WriteLine($"{id}: {results[id]}");
        }

        return results;
    }
}