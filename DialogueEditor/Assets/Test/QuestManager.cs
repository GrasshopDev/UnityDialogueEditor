using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueEditor;

public class QuestManager : MonoBehaviour
{
    public enum Quests
    {
        Tutorial        = 0,
        DragonSlayer    = 1,
    }

    public void Awake()
    {
        DialogueEditor.ConversationManager.OnConversationStarted += ConversationStart;
        DialogueEditor.ConversationManager.OnConversationEnded += ConversationEnd;
    }

    public void OnDestroy()
    {
        DialogueEditor.ConversationManager.OnConversationStarted -= ConversationStart;
        DialogueEditor.ConversationManager.OnConversationEnded -= ConversationEnd;
    }

    private void ConversationStart()
    {
        Debug.Log("[QuestManager]: Conversation has started. Closing Quest UI.");
    }

    private void ConversationEnd()
    {
        Debug.Log("[QuestManager]: Conversation has ended.");
    }

    public void BeginQuest(Quest i)
    {
        Debug.Log("Quest started: " + i);
    }
}