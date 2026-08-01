using Shora.Api;
using Shora.Application.Options;
using Shora.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddShoraCaching(builder.Configuration);
builder.Services.AddIdentityServices();
builder.Services.AddApiAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(CorsOptions.PolicyName);
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

await app.Services.InitializeDatabaseAsync();

app.Run();