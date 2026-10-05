using System.Security.Principal;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

[PublicAPI]
public static class IdentityEx
{
    extension(IIdentity identity)
    {
        public bool IsAnonymous()
        {
            return identity is AnonymousIdentity;
        }

        public bool IsSystem()
        {
            return identity is SystemIdentity;
        }
    }
}
