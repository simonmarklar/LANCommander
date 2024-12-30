using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using AuthenticationService = LANCommander.Server.Services.AuthenticationService;

namespace LANCommander.Server.Extensions;

public static class AuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddOpenIdConnect(this AuthenticationBuilder authBuilder, AuthenticationProvider authenticationProvider)
    {
        if (String.IsNullOrWhiteSpace(authenticationProvider.Slug))
            return authBuilder;
        
        return authBuilder.AddOpenIdConnect(authenticationProvider.Slug, authenticationProvider.Name, options =>
        {
            options.ClientId = authenticationProvider.ClientId;
            options.ClientSecret = authenticationProvider.ClientSecret;
            options.Authority = authenticationProvider.Authority;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false
            };

            options.Configuration = new OpenIdConnectConfiguration
            {
                AuthorizationEndpoint = authenticationProvider.AuthorizationEndpoint,
                TokenEndpoint = authenticationProvider.TokenEndpoint,
                UserInfoEndpoint = authenticationProvider.UserInfoEndpoint,
            };

            // Callbacks for middleware to properly correlate
            options.CallbackPath = new PathString($"/SignInOIDC");
            options.SignedOutCallbackPath = new PathString($"/SignOutOIDC");
            
            foreach (var scope in authenticationProvider.Scopes)
            {
                options.Scope.Add(scope);
            }
            
            foreach (var claimMapping in authenticationProvider.ClaimMappings)
            {
                if (!String.IsNullOrWhiteSpace(claimMapping.Name) && !String.IsNullOrWhiteSpace(claimMapping.Value))
                    options.ClaimActions.MapJsonKey(claimMapping.Name, claimMapping.Value);
            }
        });
    }

    public static AuthenticationBuilder AddOAuth(this AuthenticationBuilder authBuilder,
        AuthenticationProvider authenticationProvider)
    {
        if (String.IsNullOrWhiteSpace(authenticationProvider.Slug))
            return authBuilder;
        
        return authBuilder.AddOAuth(authenticationProvider.Slug, authenticationProvider.Name, options =>
        {
            options.ClientId = authenticationProvider.ClientId;
            options.ClientSecret = authenticationProvider.ClientSecret;
            options.CallbackPath = new PathString($"/SignInOAuth");
            
            options.AuthorizationEndpoint = authenticationProvider.AuthorizationEndpoint;
            options.TokenEndpoint = authenticationProvider.TokenEndpoint;
            options.UserInformationEndpoint = authenticationProvider.UserInfoEndpoint;

            foreach (var scope in authenticationProvider.Scopes)
            {
                options.Scope.Add(scope);
            }

            options.SaveTokens = true;

            foreach (var claimMapping in authenticationProvider.ClaimMappings)
            {
                if (!String.IsNullOrWhiteSpace(claimMapping.Name) && !String.IsNullOrWhiteSpace(claimMapping.Value))
                    options.ClaimActions.MapJsonKey(claimMapping.Name, claimMapping.Value);
            }

            options.Events.OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var response = await context.Backchannel.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"An error occurred while retrieving the user profile: {response.StatusCode}");

                var oauthUser = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                
                context.Identity.AddClaim(new Claim("Provider", authenticationProvider.Name));
                
                context.RunClaimActions(oauthUser.RootElement);

                var signInManager = context.HttpContext.RequestServices.GetService<SignInManager<User>>();
                var userService = context.HttpContext.RequestServices.GetService<UserService>();
                var userCustomFieldService = context.HttpContext.RequestServices.GetService<UserCustomFieldService>();
                
                var customField = await userCustomFieldService.FirstOrDefaultAsync(cf => cf.Name == $"ExternalId/{authenticationProvider.Slug}" && cf.Value == context.Identity.Name);
                var user = await userService.GetAsync(customField.UserId.GetValueOrDefault());

                await signInManager.SignInAsync(user, true);

                context.Response.Redirect("/");
                await context.Response.CompleteAsync();
            };
        });
    }
}