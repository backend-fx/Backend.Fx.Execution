using System;

namespace Backend.Fx.Execution;

public class BackendFxApplicationStateMachine
{
    public BackendFxApplicationState State { get; private set; } = BackendFxApplicationState.Halted;

    public void EnterSingeUserMode()
    {
        if (State != BackendFxApplicationState.Halted)
        {
            throw new InvalidOperationException("Cannot enter single user mode from state " + State);
        }

        State = BackendFxApplicationState.SingleUserMode;
    }

    public void EnterMultiUserMode()
    {
        if (State != BackendFxApplicationState.SingleUserMode)
        {
            throw new InvalidOperationException("Cannot enter multi user mode from state " + State);
        }

        State = BackendFxApplicationState.MultiUserMode;
    }

    public void EnterCrashed()
    {
        State = BackendFxApplicationState.Crashed;
    }

}
