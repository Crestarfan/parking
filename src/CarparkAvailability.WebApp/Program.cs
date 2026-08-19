using CarparkAvailability.WebApp.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient("ApiApp", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["services:apiapp:https:0"]
        ?? builder.Configuration["services:apiapp:http:0"]
        ?? "http://localhost:5000");
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();
