using CatalogExpertBot.Actions;
using CatalogExpertBot.Configuration;
using CatalogExpertBot.Handlers;
using CatalogExpertBot.Models;
using CatalogExpertBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Teams.AI;
using Microsoft.Teams.AI.AI.Models;
using Microsoft.Teams.AI.AI.Planners;
using Microsoft.Teams.AI.AI.Prompts;
using Azure.AI.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AI configuration
builder.Services.Configure<AIConfiguration>(builder.Configuration.GetSection("AI"));

// Add services
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Add CORS for local testing
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Bot Framework services
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(serviceProvider =>
{
    var botFrameworkAuth = serviceProvider.GetRequiredService<BotFrameworkAuthentication>();
    var logger = serviceProvider.GetRequiredService<ILogger<CloudAdapter>>();
    return new CloudAdapter(botFrameworkAuth, logger);
});

// Add application services
builder.Services.AddSingleton<ICatalogConfigurationService, CatalogConfigurationService>();
builder.Services.AddSingleton<IDataLoaderService, DataLoaderService>();
builder.Services.AddSingleton<ISearchService, SearchService>();
builder.Services.AddSingleton<IResponseFormatterService, ResponseFormatterService>();
builder.Services.AddSingleton<CatalogActions>();
builder.Services.AddSingleton<HybridMessageHandler>();

// Add AI service
builder.Services.AddSingleton<IAIService, AIService>();

// Add background service for data loading
builder.Services.AddHostedService<DataLoadingBackgroundService>();

// Configure Teams AI
builder.Services.AddSingleton<Application<AppTurnState>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Application<AppTurnState>>>();
    var hybridHandler = sp.GetRequiredService<HybridMessageHandler>();
    var catalogActions = sp.GetRequiredService<CatalogActions>();
    var aiConfig = sp.GetRequiredService<IOptions<AIConfiguration>>().Value;
    
    var options = new ApplicationOptions<AppTurnState>
    {
        BotAppId = config["MicrosoftAppId"] ?? string.Empty,
        RemoveRecipientMention = true,
        StartTypingTimer = true,
        LoggerFactory = sp.GetRequiredService<ILoggerFactory>()
    };
    
    // Create the application
    Application<AppTurnState> app;
    
    if (!string.IsNullOrEmpty(aiConfig.ApiKey))
    {
        try
        {
            // Create AI-enabled application
            logger.LogInformation("Initializing AI-enabled Teams application");
            
            // For now, create a basic app and add AI capabilities later
            // This will be enhanced in the next phase with proper AI model integration
            app = new Application<AppTurnState>(options);
            
            // Import actions for AI to use
            // TODO: When Teams AI library API is confirmed, uncomment and fix:
            // app.AI.ImportActions(catalogActions);
            
            logger.LogInformation("AI-enabled Teams application initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize AI-enabled application, falling back to basic app");
            app = new Application<AppTurnState>(options);
        }
    }
    else
    {
        logger.LogWarning("AI configuration is incomplete - API key is missing. This bot requires AI to function properly.");
        app = new Application<AppTurnState>(options);
    }

    // Handle reset command
    app.OnMessage("/reset", async (turnContext, turnState, cancellationToken) =>
    {
        await turnContext.SendActivityAsync("🔄 I've reset our conversation. How can I help you with the Teams app catalog?", cancellationToken: cancellationToken);
    });

    // Handle all other messages using hybrid handler
    app.OnMessage(".*", async (turnContext, turnState, cancellationToken) =>
    {
        try
        {
            var response = await hybridHandler.OnMessageAsync(turnContext, cancellationToken);
            if (!string.IsNullOrEmpty(response))
            {
                await turnContext.SendActivityAsync(response, cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message");
            await turnContext.SendActivityAsync("❌ I encountered an error. Please try again or contact support.", cancellationToken: cancellationToken);
        }
    });

    return app;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Enable CORS
app.UseCors();

// Enable static files
app.UseStaticFiles();

app.UseRouting();
app.MapControllers();

// Add Teams bot endpoint
app.MapPost("/api/messages", async (HttpContext context, IBotFrameworkHttpAdapter adapter, Application<AppTurnState> botApp) =>
{
    await adapter.ProcessAsync(context.Request, context.Response, botApp);
});

// Add simple test endpoint for the test chat page
app.MapPost("/api/test-chat", async (HttpContext context, ILogger<Program> logger, IAIService aiService) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        
        var request = System.Text.Json.JsonSerializer.Deserialize<dynamic>(body);
        var message = request?.GetProperty("text").GetString() ?? "No message";
        
        logger.LogInformation($"Received test message: {message}");
        
        // Check if AI service is available
        if (!aiService.IsAvailable)
        {
            var aiErrorResponse = new
            {
                text = "❌ **AI Service Required**: This bot requires AI functionality to be enabled. Please configure Azure OpenAI credentials.",
                timestamp = DateTime.UtcNow
            };
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(aiErrorResponse));
            return;
        }
        
        // Use the AI service to process the message
        var response = await aiService.ProcessMessageAsync(message);
        
        var result = new
        {
            text = response,
            timestamp = DateTime.UtcNow
        };
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in test chat endpoint");
        context.Response.StatusCode = 500;
        
        var errorResponse = new
        {
            text = $"❌ **AI Processing Error**: {ex.Message}",
            timestamp = DateTime.UtcNow
        };
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
    }
});

// Add health check endpoint
app.MapGet("/health", async (IDataLoaderService dataLoader) =>
{
    var status = await dataLoader.GetLoadingStatusAsync();
    
    return new
    {
        Status = status.IsComplete ? "Healthy" : status.IsLoading ? "Loading" : "Unhealthy",
        LastLoadTime = status.LastLoadTime,
        AppCount = status.AppDefinitionsLoaded,
        EntitlementCount = status.EntitlementsLoaded,
        CacheEfficiency = status.CacheEfficiency
    };
});

app.Run();
