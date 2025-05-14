# Backend.Fx.Execution

This library is designed to encapsulate an injection container implementing `ICompositionRoot` together with some framework services in an instance of `BackendFxApplication`.

The application instance allows to enrich the injection container by features and modules, ensures a safe boot with container verification (if supported by the implementation) and feature initialization.

When booted, the application is supposed to execute delegates inside an execution pipeline accessible through `async BackendFxApplication.Invoker.InvokeAsync(...)`.

The Backend.Fx.Execution pipeline is executing asynchronous delegates of type `Func<IServiceProvider, CancellationToken, Task>` with infrastructure wrapped around it:

- a separate injection scope, the scoped `IServiceProvider` is passed to the delegate
- a scoped NodaTime `IClock` that can be configured to be halted or not during the invocation
- an operation counter
- an `IOperation` that can be decorated with middlewares and that
  - begins before delegate execution, but after starting the injection scope
  - completes after successful execution
  - or cancels on exception
- an optional executing identity that can be accessed from within the delegate by requesting the injection of the `ICurrentTHolder<IIdentity>` in a constructor, defaulting to a new instance of `AnonymousIdentity` when not provided
- a UUID correlation that is auto generated or can be resumed by calling `ICurrentTHolder<Correlation>.Current.Resume()`
- an entry/exit logging
- exception logging
