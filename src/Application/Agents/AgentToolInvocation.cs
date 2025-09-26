namespace RapidCli.Application.Agents;

/// <summary>
/// Captures the execution details of a single agent tool invocation.
/// </summary>
public sealed record AgentToolInvocation(string ToolName, string Arguments, string Output, bool IsError)
{
    /// <summary>
    /// Creates a successful invocation descriptor.
    /// </summary>
    public static AgentToolInvocation Success(string toolName, string arguments, string output)
        => new(toolName, arguments, output, IsError: false);

    /// <summary>
    /// Creates an error descriptor for a failed tool invocation.
    /// </summary>
    public static AgentToolInvocation Failure(string toolName, string arguments, string output)
        => new(toolName, arguments, output, IsError: true);
}
