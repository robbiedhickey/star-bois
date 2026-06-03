using System.Text.Json.Serialization;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._StarBois.MCP;

/// <summary>
/// Registry of agent-addressable UI controls.
/// Each entry stores a weak reference to the control (for visibility/value queries)
/// and an action to invoke when clicked.
/// Thread-safe: read from HTTP thread, written from UI thread.
/// </summary>
public static class ControlRegistry
{
    private sealed record Entry(WeakReference<Control> ControlRef, Action? OnClick);

    public sealed record ControlInfo(
        [property: JsonPropertyName("agentId")]
        string AgentId,
        [property: JsonPropertyName("type")]
        string Type,
        [property: JsonPropertyName("visible")]
        bool Visible,
        [property: JsonPropertyName("disabled")]
        bool Disabled,
        [property: JsonPropertyName("hasClickAction")]
        bool HasClickAction,
        [property: JsonPropertyName("text")]
        string? Text,
        [property: JsonPropertyName("value")]
        string? Value);

    private static readonly Dictionary<string, Entry> _entries = new();
    private static readonly object _lock = new();

    public static void Register(string agentId, Control control, Action? onClick)
    {
        lock (_lock)
            _entries[agentId] = new Entry(new WeakReference<Control>(control), onClick);
    }

    public static void Unregister(string agentId)
    {
        lock (_lock)
            _entries.Remove(agentId);
    }

    /// <summary>
    /// Resolve a control by AgentId. Returns null if not registered or GC'd.
    /// </summary>
    public static (Control? Control, Action? OnClick) Resolve(string agentId)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(agentId, out var entry))
                return (null, null);

            if (entry.ControlRef.TryGetTarget(out var control))
                return (control, entry.OnClick);

            _entries.Remove(agentId);
            return (null, null);
        }
    }

    /// <summary>
    /// Get all registered AgentIds and their current metadata.
    /// </summary>
    public static List<ControlInfo> GetAll()
    {
        lock (_lock)
        {
            var result = new List<ControlInfo>();
            var dead = new List<string>();

            foreach (var (id, entry) in _entries)
            {
                if (entry.ControlRef.TryGetTarget(out var control))
                    result.Add(ToInfo(id, control, entry.OnClick));
                else
                    dead.Add(id);
            }

            foreach (var id in dead)
                _entries.Remove(id);

            return result;
        }
    }

    private static ControlInfo ToInfo(string agentId, Control control, Action? onClick)
    {
        var disabled = control switch
        {
            BaseButton button => button.Disabled,
            Slider slider => slider.Disabled,
            _ => false
        };

        var text = control switch
        {
            Button button => button.Text,
            CheckBox checkBox => checkBox.Text,
            Label label => label.Text,
            LineEdit lineEdit => lineEdit.Text,
            _ => null
        };

        var value = control switch
        {
            SpinBox spinBox => spinBox.Value.ToString(),
            LineEdit lineEdit => lineEdit.Text,
            BaseButton button when button.ToggleMode => button.Pressed.ToString(),
            _ => null
        };

        return new ControlInfo(
            agentId,
            control.GetType().Name,
            control.Visible,
            disabled,
            onClick != null,
            text,
            value);
    }
}
