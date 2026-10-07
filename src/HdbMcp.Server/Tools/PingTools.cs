using System.ComponentModel;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Tools;

[McpServerToolType]
public static class PingTools
{
    [McpServerTool(Name = "ping")]
    [Description("Returns pong. Used to verify the server is reachable.")]
    public static string Ping() => "pong";
}