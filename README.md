# hdb-mcp-sql

An MCP server over Singapore HDB resale flat data, in C# and SQL Server.

Most MCP-over-SQL servers expose one tool: run_query(sql). That is a toy
and a security hole. This one exposes intent-level tools over parameterised,
hand-reviewed SQL, plus a schema-semantics resource — so the model chooses
which question to ask, not which SQL to write.

Status: in progress. Block A of F.