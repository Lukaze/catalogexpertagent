using Microsoft.Teams.AI.State;

namespace CatalogExpertBot.Models;

public class AppTurnState : TurnState
{
    public AppTurnState()
    {
        ScopeDefaults[TEMP_SCOPE] = new Record();
        ScopeDefaults[USER_SCOPE] = new Record();
        ScopeDefaults[CONVERSATION_SCOPE] = new Record();
    }
}
