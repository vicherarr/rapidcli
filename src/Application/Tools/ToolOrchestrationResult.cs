using RapidCli.Domain.Models;

namespace RapidCli.Application.Tools;

/// <summary>
/// Represents the result of attempting to orchestrate an MCP tool before delegating to the agent.
/// </summary>
public sealed class ToolOrchestrationResult
{
    private ToolOrchestrationResult(
        string originalObjective,
        string agentObjective,
        bool toolExecuted,
        bool bypassAgent,
        string? responseText,
        McpToolDescriptor? descriptor,
        ToolExecutionResult? executionResult,
        string? message)
    {
        OriginalObjective = originalObjective;
        AgentObjective = agentObjective;
        ToolExecuted = toolExecuted;
        BypassAgent = bypassAgent;
        ResponseText = responseText;
        Descriptor = descriptor;
        ExecutionResult = executionResult;
        Message = message;
    }

    /// <summary>
    /// Gets the original message provided by the user.
    /// </summary>
    public string OriginalObjective { get; }

    /// <summary>
    /// Gets the objective that should be forwarded to the autonomous agent.
    /// </summary>
    public string AgentObjective { get; }

    /// <summary>
    /// Gets a value indicating whether a tool was executed.
    /// </summary>
    public bool ToolExecuted { get; }

    /// <summary>
    /// Gets a value indicating whether the agent should be bypassed because the tool already produced a final answer.
    /// </summary>
    public bool BypassAgent { get; }

    /// <summary>
    /// Gets the response text to return directly to the user when <see cref="BypassAgent"/> is true.
    /// </summary>
    public string? ResponseText { get; }

    /// <summary>
    /// Gets the descriptor of the tool that was chosen.
    /// </summary>
    public McpToolDescriptor? Descriptor { get; }

    /// <summary>
    /// Gets the execution result returned by the provider.
    /// </summary>
    public ToolExecutionResult? ExecutionResult { get; }

    /// <summary>
    /// Gets an optional informational message that should be rendered to the user before the agent response.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Creates a result indicating that no tool was executed and the agent should receive the original objective.
    /// </summary>
    public static ToolOrchestrationResult Skip(string objective, string? message = null)
        => new(objective, objective, false, false, null, null, null, message);

    /// <summary>
    /// Creates a result that forwards the provided objective to the agent after executing a tool.
    /// </summary>
    public static ToolOrchestrationResult Forward(
        string originalObjective,
        string agentObjective,
        McpToolDescriptor descriptor,
        ToolExecutionResult executionResult,
        string? message)
        => new(originalObjective, agentObjective, true, false, null, descriptor, executionResult, message);

    /// <summary>
    /// Creates a result that bypasses the agent because the tool produced the final answer.
    /// </summary>
    public static ToolOrchestrationResult Complete(
        string originalObjective,
        McpToolDescriptor descriptor,
        ToolExecutionResult executionResult,
        string responseText,
        string? message)
        => new(originalObjective, originalObjective, true, true, responseText, descriptor, executionResult, message);
}
