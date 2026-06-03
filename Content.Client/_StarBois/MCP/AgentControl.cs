using Robust.Client.UserInterface;

namespace Content.Client._StarBois.MCP;

/// <summary>
/// Marks a UI control as addressable by the agent via a stable identifier.
///
/// Usage:
///   AgentControl.Register("starmap-warp-button", warpButton, () => OnWarpPressed());
///
/// The click action lets each control define what "clicking" means for it,
/// rather than us trying to invoke framework events from outside.
/// The AgentId should be stable across refactors; it is part of the agent interface.
/// </summary>
public static class AgentControl
{
    /// <summary>
    /// Register a control with an agent-addressable ID and an action to invoke when clicked.
    /// Call this in the control's constructor or initialization.
    /// </summary>
    public static void Register(string agentId, Control control, Action? onClick = null)
    {
        ControlRegistry.Register(agentId, control, onClick);
    }

    /// <summary>
    /// Unregister when a control is disposed.
    /// </summary>
    public static void Unregister(string agentId)
    {
        ControlRegistry.Unregister(agentId);
    }
}
