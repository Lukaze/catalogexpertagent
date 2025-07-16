using CatalogExpertBot.Configuration;
using CatalogExpertBot.Models;
using CatalogExpertBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Options;

namespace CatalogExpertBot.Handlers;

public class HybridMessageHandler
{
    private readonly AIConfiguration _aiConfig;
    private readonly IAIService _aiService;
    private readonly ILogger<HybridMessageHandler> _logger;

    public HybridMessageHandler(
        IOptions<AIConfiguration> aiConfig,
        IAIService aiService,
        ILogger<HybridMessageHandler> logger)
    {
        _aiConfig = aiConfig.Value;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<string> OnMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
    {
        var userMessage = turnContext.Activity.Text?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return "👋 Hello! I'm the Teams App Catalog Expert. Ask me about Microsoft Teams apps, their availability across different audience groups, or say 'help' to see what I can do!";
        }

        _logger.LogInformation("Processing message: {UserMessage}, AIAvailable: {AIAvailable}", 
            userMessage, _aiService.IsAvailable);

        // AI-only mode - fail if AI is not available
        if (!_aiService.IsAvailable)
        {
            return "❌ AI Service Required: This bot requires AI functionality to be enabled. Please configure Azure OpenAI credentials in `appsettings.secrets.json`.";
        }

        try
        {
            _logger.LogInformation("Using AI to process message: {UserMessage}", userMessage);
            var aiResponse = await _aiService.ProcessMessageAsync(userMessage, cancellationToken);
            
            _logger.LogInformation("AI successfully processed message, response length: {Length}", aiResponse?.Length ?? 0);
            return aiResponse ?? "❌ AI Response Error: Received empty response from AI service.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI processing failed");
            return "❌ AI Processing Error: I encountered an error while processing your request with AI. Please check the AI service configuration and try again.";
        }
    }
}
