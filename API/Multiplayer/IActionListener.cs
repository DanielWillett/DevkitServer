using DevkitServer.Multiplayer.Actions;

namespace DevkitServer.API.Multiplayer;
public interface IActionListener
{
    ActionSettings Settings { get; }
    int QueueSize { get; }
}

public delegate void AppliedAction(IActionListener caller, IAction action);
public delegate void ApplyingAction(IActionListener caller, IAction action, ref bool execute);