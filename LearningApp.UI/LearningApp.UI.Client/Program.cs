using LearningApp.UI.Client;
using LearningApp.UI.Client.Auth;
using LearningApp.UI.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<BffAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<BffAuthenticationStateProvider>());
builder.Services.AddHttpClient("LearningApi", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("LearningApi"));
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<CategoryApiService>();
builder.Services.AddScoped<CourseApiService>();
builder.Services.AddScoped<UserApiService>();
builder.Services.AddScoped<EnrollmentApiService>();
builder.Services.AddScoped<LearningProgressApiService>();

await builder.Build().RunAsync();
