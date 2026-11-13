using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace foll_notifications.Shared.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["InternalApi:ApiKey"];
        var actual = context.HttpContext.Request.Headers["X-Foll-Service-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual) ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(actual))))
            context.Result = new UnauthorizedResult();
    }
}
