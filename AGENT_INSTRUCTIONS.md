# 🤖 Agent Instructions for CatalogExpertBot

> **Important:** This file contains essential guidelines for AI agents working on this codebase. Please read and follow these instructions carefully.

## 🚀 Build & Run Protocol

### ✅ **ALWAYS USE:**
- **`.\run.ps1`** - The official build and run script
- **`.\run.ps1 [port] [configuration]`** - For custom port/config
- **Examples:**
  - `.\run.ps1` (default: port 5000, Debug)
  - `.\run.ps1 5001 Release`

### ❌ **NEVER USE:**
- Individual `dotnet build`, `dotnet run`, or `dotnet restore` commands
- Manual build processes or one-off commands
- Any other build scripts (they should not exist)

### 🔄 **Standard Workflow:**
1. Make code changes
2. Run `.\run.ps1` to test
3. Fix any issues
4. **Ask user to verify changes before committing**
5. Commit changes only when explicitly instructed

---

## ⚡ AI Agent Efficiency Guidelines

### 🎯 **Be Concise and Fast:**
- **No excessive summaries** - Brief confirmations only when needed
- **Skip verification steps** - Don't check if files were committed unless asked
- **Focus on action** - Do the work, don't narrate every step
- **Single-pass execution** - Complete tasks without over-explaining

### ✅ **What to Include:**
- Essential error messages or warnings
- Brief confirmation when major changes complete
- Next steps only if unclear what to do
- **Request for user verification before committing changes**

### ❌ **What to Skip:**
- Detailed "what I just did" summaries
- Verification that files exist or were modified correctly
- Step-by-step narration of routine operations
- Excessive emoji usage and formatting
- Long success confirmation messages
- **Automatic commits without user approval**

### 💡 **Communication Style:**
- Get straight to the point
- Use tools efficiently without over-explaining
- Only ask clarifying questions when truly needed
- Provide brief, actionable responses

---

## 📋 Project Specifications

### 📁 **Specification Sources:**
- **`spec/TEAMS_AI_BOT_SPEC.md`** - Core bot functionality requirements
- **`spec/LLM_INTEGRATION_SPEC.md`** - AI integration specifications

### 🎯 **Implementation Requirements:**
1. **Follow spec documents exactly** - All features must align with specifications
2. **Maintain backward compatibility** - Don't break existing functionality
3. **AI-first approach** - Prioritize LLM integration over rule-based logic
4. **Teams integration** - Ensure proper Microsoft Teams Bot Framework compliance

---

## 🏗️ Architecture & Design Principles

### 📂 **Project Structure:**
```
├── Actions/           # Bot actions and commands
├── Configuration/     # App configuration classes
├── Handlers/          # Message and event handlers
├── Models/           # Data models and DTOs
├── Services/         # Core business logic services
├── prompts/          # AI prompts and templates
├── spec/             # Specification documents
└── wwwroot/          # Static web files
```

### 🎨 **Design Principles:**

#### 1. **Service-Oriented Architecture**
- Keep services focused and single-responsibility
- Use dependency injection properly
- Maintain clear interfaces

#### 2. **AI Integration Standards**
- Use `AIService` for all LLM interactions
- Implement function calling for complex operations
- **AI is required** - no fallback mechanisms for non-AI processing
- The bot will return errors if AI configuration is missing or invalid

#### 3. **Error Handling**
- Always include proper exception handling
- Log errors appropriately with context
- Provide user-friendly error messages

#### 4. **Code Quality**
- **File Size Limit:** Keep files under 1000 lines
- **Method Complexity:** Break down complex methods
- **Documentation:** Include XML docs for public APIs
- **Naming:** Use clear, descriptive names

#### 5. **Testing Strategy**
- Test via the web interface at `http://localhost:5000`
- Verify AI responses are accurate and helpful
- Ensure proper error handling when AI configuration is invalid

---

## 🔧 Configuration Management

### ⚙️ **Configuration Files:**
- **`appsettings.json`** - Production settings (no secrets)
- **`appsettings.Development.json`** - Development overrides
- **`appsettings.secrets.json`** - Sensitive AI config (excluded from git)
- **`prompts/`** - AI prompt templates

### 🔐 **Security Notes:**
- Never commit API keys or secrets to git
- Use `appsettings.secrets.json` for sensitive AI configuration
- **AI configuration is required** - the bot will not function without proper Azure OpenAI setup
- Secrets file is already in .gitignore - keep it that way

### 🤖 **AI Configuration Setup:**
Create `appsettings.secrets.json` with your AI credentials and Bot Framework settings:
```json
{
  "MicrosoftAppId": "your-bot-app-id",
  "MicrosoftAppPassword": "your-bot-app-password", 
  "MicrosoftAppTenantId": "your-tenant-id",
  "AI": {
    "ApiKey": "your-azure-openai-api-key-here",
    "Endpoint": "https://your-openai-endpoint.openai.azure.com/"
  }
}
```

**Important Notes:**
- This file is excluded from git via `.gitignore`
- The Bot Framework credentials are required for Teams integration
- **AI configuration is mandatory** - the bot will not function without valid Azure OpenAI credentials
- **Azure OpenAI credentials are required** - the bot will not function without them
- Other AI settings (Provider, Model, etc.) are configured in `appsettings.json`

---

## 📝 Development Guidelines

### ✅ **Before Making Changes:**
1. Read relevant specification documents
2. Understand the current architecture
3. Plan changes to minimize impact
4. Ensure AI service integration is maintained

### ✅ **While Coding:**
1. Follow existing code patterns
2. Use proper async/await patterns
3. Include appropriate logging
4. Handle edge cases gracefully

### ✅ **Before Committing:**
1. Test with `.\run.ps1`
2. Verify all features work as expected
3. Check for compilation errors/warnings
4. Ensure AI service is properly configured
5. **WAIT for user verification and explicit commit instruction**

### ✅ **Git Practices:**
1. Write clear, descriptive commit messages
2. Group related changes in single commits
3. Don't commit build artifacts or temp files
4. Keep commits focused and atomic
5. **For AI agents:** Only commit when explicitly instructed by the user after verification

---

## 🚫 Common Pitfalls to Avoid

### ❌ **Build & Run:**
- Using manual dotnet commands instead of `run.ps1`
- Creating additional build scripts
- Not testing changes before committing

### ❌ **Architecture:**
- Creating files over 1000 lines
- Mixing concerns in single classes
- Ignoring existing patterns
- Breaking the service layer structure

### ❌ **AI Integration:**
- Making AI service optional (it should be required)
- Not properly configuring Azure OpenAI credentials
- Ignoring function calling patterns
- Hardcoding AI responses

### ❌ **Configuration:**
- Committing secrets or API keys
- Breaking existing configuration structure
- Not configuring required AI credentials

---

## 🎯 Key Success Metrics

### ✅ **Project Health Indicators:**
- [ ] All files under 1000 lines
- [ ] `.\run.ps1` builds and runs successfully
- [ ] AI service is properly configured and functional
- [ ] All specifications are implemented
- [ ] No build warnings or errors
- [ ] Web interface at `http://localhost:5000` responds correctly

### ✅ **Feature Completeness:**
- [ ] Teams app catalog search functionality
- [ ] AI-powered natural language processing
- [ ] Function calling for complex queries
- [ ] Proper error handling and user feedback
- [ ] Audience group filtering and management

---

## 📞 Emergency Procedures

### 🚨 **If Build Fails:**
1. Check for compilation errors
2. Ensure all NuGet packages are restored
3. Verify configuration files are valid
4. Try clean build: delete `bin/` and `obj/` folders, then `.\run.ps1`

### 🚨 **If AI Service Fails:**
1. Verify AI configuration in `appsettings.json` and `appsettings.secrets.json`
2. Check API keys and endpoints are correct
3. Ensure Azure OpenAI service is accessible
4. Check logs for specific AI service errors

---

## 📚 Additional Resources

- **Microsoft Teams Bot Framework**: [Official Documentation](https://docs.microsoft.com/en-us/microsoftteams/platform/bots/)
- **Azure OpenAI**: [API Documentation](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/)
- **ASP.NET Core**: [Best Practices](https://docs.microsoft.com/en-us/aspnet/core/)

---

*Last Updated: July 16, 2025*  
*Follow these guidelines to maintain code quality and project consistency.*
