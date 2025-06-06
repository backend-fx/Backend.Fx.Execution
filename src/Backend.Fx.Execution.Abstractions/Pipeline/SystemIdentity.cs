﻿using System;
using System.Security.Principal;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

[PublicAPI]
public readonly struct SystemIdentity : IIdentity, IEquatable<SystemIdentity>
{
    public string Name => "SYSTEM";

    public string AuthenticationType => "Internal";

    public bool IsAuthenticated => true;

    public override bool Equals(object? other)
    {
        return other is SystemIdentity;
    }

    public override int GetHashCode()
    {
        return 542451621;
    }

    public bool Equals(SystemIdentity other)
    {
        return true;
    }
}