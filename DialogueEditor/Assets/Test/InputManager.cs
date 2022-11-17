using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueEditor;

public class InputManager : MonoBehaviour
{
    public NPCConversation m_testConv;

    void Update()
    {
        if (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
                ConversationManager.Instance.SelectNextOption();

            if (Input.GetKeyDown(KeyCode.DownArrow))
                ConversationManager.Instance.SelectPreviousOption();

            if (Input.GetKeyDown(KeyCode.LeftControl))
                ConversationManager.Instance.PressSelectedOption();
        }
    }

    void Test()
    {
        ConversationManager.Instance.SetBool("BoolName", true);
        ConversationManager.Instance.SetInt("IntName", 1);

        bool bVal = ConversationManager.Instance.GetBool("BoolName");
        int iVal = ConversationManager.Instance.GetInt("IntName");
    }
}