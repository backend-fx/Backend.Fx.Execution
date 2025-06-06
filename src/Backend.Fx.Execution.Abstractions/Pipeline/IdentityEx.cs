using System.Security.Principal;
using JetBrains.Annotations;

namespace Backend.Fx.Execution.Pipeline;

[PublicAPI]
public static class IdentityEx
{
    public static bool IsAnonymous(this IIdentity identity)
    {
        return identity is AnonymousIdentity;
    }
        
    public static bool IsSystem(this IIdentity identity)
    {
        return identity is SystemIdentity;
    }
}