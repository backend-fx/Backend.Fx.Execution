using System;
using System.Security.Principal;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline
{
    [PublicAPI]
    public readonly struct AnonymousIdentity : IIdentity, IEquatable<IIdentity>
    {
        public string Name => "ANONYMOUS";

        public string AuthenticationType => null;

        public bool IsAuthenticated => false;
        
        public override bool Equals(object other)
        {
            return other is AnonymousIdentity;
        }

        public override int GetHashCode()
        {
            return 1564925492;
        }

        public bool Equals(IIdentity other)
        {
            return other is AnonymousIdentity;
        }
    }
}