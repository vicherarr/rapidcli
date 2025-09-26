using System.Collections.Generic;

namespace RapidCli.Application.Agents;

/// <summary>
/// Represents the outcome of an agent run.
/// </summary>
public sealed record AgentExecutionResult(string FinalResponse, IReadOnlyList<AgentToolInvocation> ToolInvocations, bool Completed)
{
    /// <summary>
    /// Creates a result indicating that the agent finished successfully.
    /// </summary>
    public static AgentExecutionResult Success(string finalResponse, IReadOnlyList<AgentToolInvocation> toolInvocations)
        => new(finalResponse, toolInvocations, Completed: true);

    /// <summary>
    /// Creates a result indicating that the agent failed to complete its task.
    /// </summary>
    public static AgentExecutionResult Failure(string message, IReadOnlyList<AgentToolInvocation> toolInvocations)
        => new(message, toolInvocations, Completed: false);
}
