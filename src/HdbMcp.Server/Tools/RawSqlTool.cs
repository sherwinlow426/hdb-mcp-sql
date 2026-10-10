using System.ComponentModel;
using HdbMcp.Server.Data;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public sealed class RawSqlTool(HdbRepository repo, HdbOptions options)
{
    private static readonly string[] Banned =
    {
        "insert", "update", "delete", "drop", "alter", "create", "truncate",
        "merge", "grant", "revoke", "exec", "execute", "xp_", "sp_",
        "openrowset", "opendatasource", "bulk", "waitfor", "shutdown"
    };

    [McpServerTool(Name = "run_readonly_query")]
    [Description("Escape hatch: runs an arbitrary read-only SELECT. " +
        "Disabled unless explicitly enabled. Exists for benchmarking " +
        "against the intent-level tools, not for normal use.")]
    public async Task<object> RunReadonlyQuery(
        string sql, CancellationToken ct = default)
    {
        if (!options.EnableRawSql)
            return new { refused = true,
                reason = "Raw SQL is disabled. Use the intent-level tools." };

        var normalised = sql.Trim().ToLowerInvariant();

        if (!normalised.StartsWith("select") && !normalised.StartsWith("with"))
            return new { refused = true, reason = "Only SELECT is permitted." };

        if (normalised.Contains(';') && !normalised.TrimEnd(';').Contains(';'))
        { /* a single trailing semicolon is fine */ }
        else if (normalised.Contains(';'))
            return new { refused = true, reason = "Multiple statements." };

        foreach (var word in Banned)
            if (normalised.Contains(word))
                return new { refused = true, reason = $"Keyword '{word}' is not permitted." };

        var rows = await repo.QueryAsync(sql, new Dictionary<string, object?>(),
                                         maxRows: 200, ct: ct);
        return new { refused = false, rowCount = rows.Count, rows,
                     warning = "Raw SQL bypasses every semantic guard. " +
                               "Results may be confidently wrong." };
    }
}