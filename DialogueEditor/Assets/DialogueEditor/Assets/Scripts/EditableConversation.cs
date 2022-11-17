using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DialogueEditor
{
    [DataContract]
    [KnownType(typeof(EditableBoolParameter))]
    [KnownType(typeof(EditableIntParameter))]
    public class EditableConversation
    {
        public const int INVALID_UID = -1;

        public EditableConversation()
        {
            SpeechNodes = new List<EditableSpeechNode>();
            Options = new List<EditableOptionNode>();
        }

        [DataMember] public List<EditableSpeechNode> SpeechNodes;
        [DataMember] public List<EditableOptionNode> Options;
        [DataMember] public List<EditableParameter> Parameters;

        public int SaveVersion;

        // ----

        public EditableSpeechNode GetRootNode()
        {
            for (int i = 0; i < SpeechNodes.Count; i++)
            {
                if (SpeechNodes[i].EditorInfo.isRoot)
                    return SpeechNodes[i];
            }
            return null;
        }

        public EditableConversationNode GetNodeByUID(int uid)
        {
            for (int i = 0; i < SpeechNodes.Count; i++)
                if (SpeechNodes[i].ID == uid)
                    return SpeechNodes[i];

            for (int i = 0; i < Options.Count; i++)
                if (Options[i].ID == uid)
                    return Options[i];

            return null;
        }

        public EditableSpeechNode GetSpeechByUID(int uid)
        {
            for (int i = 0; i < SpeechNodes.Count; i++)
                if (SpeechNodes[i].ID == uid)
                    return SpeechNodes[i];

            return null;
        }

        public EditableOptionNode GetOptionByUID(int uid)
        {
            for (int i = 0; i < Options.Count; i++)
                if (Options[i].ID == uid)
                    return Options[i];

            return null;
        }
    }
}