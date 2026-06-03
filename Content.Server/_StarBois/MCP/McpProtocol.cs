using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Content.Server._StarBois.MCP;

// JSON-RPC 2.0 types for the MCP protocol

public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public JsonElement? Id { get; set; }
    [JsonPropertyName("method")] public string Method { get; set; } = "";
    [JsonPropertyName("params")] public JsonElement? Params { get; set; }
}

public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public JsonElement? Id { get; set; }
    [JsonPropertyName("result")] public object? Result { get; set; }
    [JsonPropertyName("error")] public JsonRpcError? Error { get; set; }
}

public sealed class JsonRpcError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

// MCP tool result content block
public sealed class McpContent
{
    [JsonPropertyName("type")] public string Type { get; set; } = "text";
    [JsonPropertyName("text")] public string? Text { get; set; }

    // For image content (screenshots)
    [JsonPropertyName("data")] public string? Data { get; set; }
    [JsonPropertyName("mimeType")] public string? MimeType { get; set; }

    public static McpContent FromText(string text) => new() { Type = "text", Text = text };

    public static McpContent Image(byte[] png) => new()
    {
        Type = "image",
        Data = Convert.ToBase64String(png),
        MimeType = "image/png"
    };
}

public sealed class McpToolResult
{
    [JsonPropertyName("content")] public List<McpContent> Content { get; set; } = new();
    [JsonPropertyName("isError")] public bool IsError { get; set; }

    public static McpToolResult Ok(string text) => new()
    {
        Content = [McpContent.FromText(text)]
    };

    public static McpToolResult Ok(object data) => new()
    {
        Content = [McpContent.FromText(JsonSerializer.Serialize(data, McpJsonOptions.Default))]
    };

    public static McpToolResult Err(string message) => new()
    {
        Content = [McpContent.FromText(message)],
        IsError = true
    };

    public static McpToolResult Screenshot(byte[] png) => new()
    {
        Content = [McpContent.Image(png)]
    };
}

// Tool definition for tools/list response
public sealed class McpToolDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("inputSchema")] public object InputSchema { get; set; } = new();
}

public static class McpJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
