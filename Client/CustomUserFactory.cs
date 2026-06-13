using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using System.Security.Claims;
using System.Text.Json;

namespace Client
{
    public class CustomUserFactory
        : AccountClaimsPrincipalFactory<RemoteUserAccount>
    {
        public CustomUserFactory(IAccessTokenProviderAccessor accessor)
            : base(accessor) { }

        public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
            RemoteUserAccount account,
            RemoteAuthenticationUserOptions options)
        {
            var user = await base.CreateUserAsync(account, options);

            // Controlla che identity non sia null
            if (user.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                return user;

            var rolesKey = "https://heroic853.github.io/roles";
            if (account.AdditionalProperties.TryGetValue(rolesKey, out var rolesObj)
                && rolesObj is JsonElement rolesElement
                && rolesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in rolesElement.EnumerateArray())
                {
                    var roleValue = role.GetString();
                    if (!string.IsNullOrEmpty(roleValue))
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                }
            }

            return user;
        }
    }
}