using LearningApp.UI.Shared.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace LearningApp.UI.Client.Auth;

public sealed class BffAuthenticationStateProvider(HttpClient httpClient) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var user = await httpClient.GetFromJsonAsync<BffUserInfo>("bff/user");
            if (user is not { IsAuthenticated: true })
            {
                return new AuthenticationState(Anonymous);
            }

            var claims = new List<Claim>();
            if (!string.IsNullOrWhiteSpace(user.Name))
            {
                claims.Add(new Claim(ClaimTypes.Name, user.Name));
            }

            if (user.UserId is { } userId)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "bff")));
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public void Refresh() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
