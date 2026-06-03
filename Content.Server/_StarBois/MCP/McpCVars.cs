using Robust.Shared.Configuration;

namespace Content.Server._StarBois.MCP;

[CVarDefs]
public sealed class McpCVars
{
    /// <summary>
    /// Enable the star-bois MCP server. Off by default for dev builds only.
    /// </summary>
    public static readonly CVarDef<bool> McpEnabled =
        CVarDef.Create("mcp.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// Port the MCP server listens on.
    /// </summary>
    public static readonly CVarDef<int> McpPort =
        CVarDef.Create("mcp.port", 9222, CVar.SERVERONLY);

    /// <summary>
    /// URL of the client's internal agent HTTP API.
    /// </summary>
    public static readonly CVarDef<string> McpClientUrl =
        CVarDef.Create("mcp.client_url", "http://localhost:9223", CVar.SERVERONLY);
}
