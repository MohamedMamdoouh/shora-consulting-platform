using Shora.Api;
using Shora.Api.Middleware;
using Shora.Application.Options;
using Shora.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddShoraCaching(builder.Configuration);
builder.Services.AddIdentityServices();
builder.Services.AddApiAuthentication(builder.Configuration);

if (builder.Environment.IsProduction())
{
    builder.Services.AddProductionOptionsValidation();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(CorsOptions.PolicyName);

var staticIndexPath = Path.Combine(app.Environment.WebRootPath ?? app.Environment.ContentRootPath, "index.html");

if (!app.Environment.IsDevelopment() && File.Exists(staticIndexPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();
app.MapControllers();

if (!app.Environment.IsDevelopment() && File.Exists(staticIndexPath))
{
    app.MapFallbackToFile("index.html");
}

await app.Services.InitializeDatabaseAsync();

app.Run();