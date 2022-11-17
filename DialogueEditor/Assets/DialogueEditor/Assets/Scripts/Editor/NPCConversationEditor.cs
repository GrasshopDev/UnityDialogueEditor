using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DialogueEditor
{
    [CustomEditor(typeof(NPCConversation))]
    public class NPCConversationEditor : Editor
    {
        private static GUIStyle boldStyle;
        private static GUIStyle regularStyle;

        void OnEnable()
        {
            boldStyle = new GUIStyle();
            boldStyle.alignment = TextAnchor.MiddleLeft;
            boldStyle.fontStyle = FontStyle.Bold;
            boldStyle.wordWrap = true;

            regularStyle = new GUIStyle();
            regularStyle.alignment = TextAnchor.MiddleLeft;
            regularStyle.wordWrap = true;


            if (EditorGUIUtility.isProSkin)
            {
                boldStyle.normal.textColor = DialogueEditorUtil.ProSkinTextColour;
                regularStyle.normal.textColor = DialogueEditorUtil.ProSkinTextColour;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Conversation: ", boldStyle);
            EditorGUILayout.TextField(serializedObject.targetObject.name, regularStyle);
            EditorGUILayout.EndHorizontal();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(NodeEventHolder))]
    public class NodeEventHolderEditor : Editor
    {
        private NodeEventHolder n;

        void OnEnable()
        {
            n = (base.target as NodeEventHolder);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Node " + n.NodeID + " event and data information holder.");
            EditorGUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
        }
    }
}