using System;
using Backend.Fx.Logging;
using Microsoft.Extensions.Logging;

namespace Backend.Fx.Execution;

public class BackendFxApplicationStateMachine
{
    private static readonly ILogger Logger = Log.Create<BackendFxApplicationStateMachine>();

    public BackendFxApplicationState State { get; private set; } = BackendFxApplicationState.Halted;

    public void EnterSingeUserMode()
    {
        switch (State)
        {
            case BackendFxApplicationState.Halted:
            case BackendFxApplicationState.MultiUserMode:
                EnterState(BackendFxApplicationState.SingleUserMode);
                break;
            case BackendFxApplicationState.SingleUserMode:
                break;
            case BackendFxApplicationState.Crashed:
            default:
                throw new InvalidOperationException("Cannot enter single user mode from state " + State);
        }
    }

    public void EnterMultiUserMode()
    {
        switch (State)
        {
            case BackendFxApplicationState.SingleUserMode:
                EnterState(BackendFxApplicationState.MultiUserMode);
                break;
            case BackendFxApplicationState.MultiUserMode:
                break;
            case BackendFxApplicationState.Halted:
            case BackendFxApplicationState.Crashed:
            default:
                throw new InvalidOperationException("Cannot enter single user mode from state " + State);
        }
    }

    public void EnterCrashed()
    {
        EnterState(BackendFxApplicationState.Crashed);
    }

    private void EnterState(BackendFxApplicationState newState)
    {
        Logger.LogInformation("Application state switches from {OldState} to {NewState}", State, newState);
        State = newState;
    }
}
