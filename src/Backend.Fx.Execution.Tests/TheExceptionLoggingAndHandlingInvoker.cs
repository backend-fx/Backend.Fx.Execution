using System;
using System.Threading.Tasks;
using Backend.Fx.Execution.DependencyInjection;
using Backend.Fx.Execution.Pipeline;
using Backend.Fx.Logging;
using FakeItEasy;
using Xunit;

namespace Backend.Fx.Execution.Tests;

public class TheExceptionLoggingAndHandlingInvoker
{
    private readonly IBackendFxApplicationInvoker _sut;
    private readonly IExceptionLogger _exceptionLogger = A.Fake<IExceptionLogger>();

    public TheExceptionLoggingAndHandlingInvoker()
    {
        var application = new BackendFxApplication(
            A.Fake<ICompositionRoot>(),
            _exceptionLogger,
            GetType().Assembly);
        _sut = new ExceptionLoggingAndHandlingInvoker(_exceptionLogger, application.Invoker);
    }


    [Fact]
    public void SwallowsExceptions()
    {
        _sut.InvokeAsync(_ => Task.CompletedTask);
        _sut.InvokeAsync(_ => throw new DivideByZeroException());
    }
}