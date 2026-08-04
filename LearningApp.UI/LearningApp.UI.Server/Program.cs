using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

var azureAdSection = builder.Configuration.GetSection("AzureAd");
var apiScope = azureAdSection["ApiScope"] ?? throw new InvalidOperationException("AzureAd:ApiScope is missing.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "LearningApp.Bff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/bff/login";
        options.LogoutPath = "/bff/logout";
        options.AccessDeniedPath = "/login";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = azureAdSection["Authority"];
        options.ClientId = azureAdSection["ClientId"];
        options.ClientSecret = azureAdSection["ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = azureAdSection["CallbackPath"] ?? "/signin-oidc";
        options.SignedOutCallbackPath = azureAdSection["SignedOutCallbackPath"] ?? "/signout-callback-oidc";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");
        options.Scope.Add(apiScope);
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "roles";
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient("LearningApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl is missing."));
});
builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
