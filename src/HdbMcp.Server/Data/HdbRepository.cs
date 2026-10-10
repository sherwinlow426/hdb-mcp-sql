using System.Data;
using Microsoft.Data.SqlClient;

namespace HdbMcp.Server.Data;

public sealed class HdbOptions
{
    public required string ConnectionString { get; init; }
    public int MaxRows { get; init; } = 500;
    public int TimeoutSeconds { get; init; } = 15;
    public bool EnableRawSql { get; init; }
}

public sealed class HdbRepository(HdbOptions options)
{
    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        int? maxRows = null,
        CancellationToken ct = default)
    {
        var cap = Math.Min(maxRows ?? options.MaxRows, options.MaxRows);

        await using var conn = new SqlConnection(options.ConnectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = options.TimeoutSeconds
        };
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<Dictionary<string, object?>>();
        while (rows.Count < cap && await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] =
                    reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}