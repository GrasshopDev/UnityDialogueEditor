using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DialogueEditor
{
    [DataContract]
    [KnownType(typeof(EditableSpeechConnection))]
    [KnownType(typeof(EditableOptionConnection))]
    [KnownType(typeof(EditableSetIntParamAction))]
    [KnownType(typeof(EditableSetBoolParamAction))]
    public abstract class EditableConversationNode
    {
        public enum eNodeType
        {
            Speech,
            Option
        }

        /// <summary> Info used internally by the editor window. </summary>
        [DataContract]
        public class EditorArgs
        {
            [DataMember] public float xPos;
            [DataMember] public float yPos;
            [DataMember] public bool isRoot;
        }

        public EditableConversationNode()
        {
            parents = new List<EditableConversationNode>();
            Connections = new List<EditableConnection>();
            ParamActions = new List<EditableSetParamAction>();
            parentUIDs = new List<int>();
            EditorInfo = new EditorArgs { xPos = 0, yPos = 0, isRoot = false };
        }

        public abstract eNodeType NodeType { get; }

        // ----
        // Serialized Editor vars
        [DataMember] public EditorArgs EditorInfo;
        [DataMember] public int ID;

        // ----
        // Serialized Node data
        [DataMember] public string Text;
        [DataMember] public List<EditableConnection> Connections;
        [DataMember] public List<int> parentUIDs;
        [DataMember] public List<EditableSetParamAction> ParamActions;
        /// <summary> Deprecated as of V1.03 </summary>
        [DataMember] public string TMPFontGUID;

        // ----
        // Volatile
        public TMPro.TMP_FontAsset TMPFont;
        public List<EditableConversationNode> parents;


        // ------------------------

        public void RegisterUIDs()
        {
            if (parentUIDs != null)
                parentUIDs.Clear();

            parentUIDs = new List<int>();

            for (int i = 0; i < parents.Count; i++)
            {
                parentUIDs.Add(parents[i].ID);
            }
        }

        public void RemoveSelfFromTree()
        {
            // This speech is no longer the parent of any children
            for (int i = 0; i < Connections.Count; i++)
            {
                if (Connections[i] is EditableSpeechConnection)
                {
                    EditableSpeechConnection speechCon = Connections[i] as EditableSpeechConnection;
                    speechCon.Speech.parents.Remove(this);
                }
                else if (Connections[i] is EditableOptionConnection)
                {
                    EditableOptionConnection optionCon = Connections[i] as EditableOptionConnection;
                    optionCon.Option.parents.Remove(this);
                }
            }

            // This speech is no longer any of my parents child speech 
            for (int i = 0; i < parents.Count; i++)
            {
                parents[i].DeleteConnectionChild(this);
            }
        }    

        public void DeleteConnectionChild(EditableConversationNode node)
        {
            if (Connections.Count == 0)
                return;

            if (node.NodeType == eNodeType.Speech && Connections[0] is EditableSpeechConnection)
            {
                EditableSpeechNode toRemove = node as EditableSpeechNode;

                for (int i = 0; i < Connections.Count; i++)
                {
                    EditableSpeechConnection con = Connections[i] as EditableSpeechConnection;
                    if (con.Speech == toRemove)
                    {
                        Connections.RemoveAt(i);
                        return;
                    }
                }
            }
            else if (node is EditableOptionNode && Connections[0] is EditableOptionConnection)
            {
                EditableOptionNode toRemove = node as EditableOptionNode;

                for (int i = 0; i < Connections.Count; i++)
                {
                    EditableOptionConnection con = Connections[i] as EditableOptionConnection;
                    if (con.Option == toRemove)
                    {
                        Connections.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        public virtual void SerializeAssetData(NPCConversation conversation)
        {
            conversation.GetNodeData(this.ID).TMPFont = this.TMPFont;
        }

        public virtual void DeserializeAssetData(NPCConversation conversation)
        {
            this.TMPFont = conversation.GetNodeData(this.ID).TMPFont;

#if UNITY_EDITOR
            // If under V1.03, Load from database via GUID, so data is not lost for people who are upgrading
            if (conversation.Version < (int)eSaveVersion.V1_03)
            {
                if (this.TMPFont == null)
                {
                    if (!string.IsNullOrEmpty(TMPFontGUID))
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(TMPFontGUID);
                        this.TMPFont = (TMPro.TMP_FontAsset)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(TMPro.TMP_FontAsset));
                    }
                }
            }
#endif
        }
    }




    [DataContract]
    public class EditableSpeechNode : EditableConversationNode
    {
        public EditableSpeechNode() : base()
        {

        }

        public override eNodeType NodeType { get { return eNodeType.Speech; } }

        // ----
        // Serialized Node data

        /// <summary> The NPC Name </summary>
        [DataMember] public string Name;

        /// <summary> The NPC Icon </summary>
        public Sprite Icon;
        /// <summary> Deprecated as of V1.03 </summary>
        [DataMember] public string IconGUID;

        /// <summary> The Audio Clip acompanying this Speech. </summary>
        public AudioClip Audio;
        /// <summary> Deprecated as of V1.03 </summary>
        [DataMember] public string AudioGUID;

        /// <summary> The Volume for the AudioClip; </summary>
        [DataMember] public float Volume;

        /// <summary> If this dialogue leads onto another dialogue... 
        /// Should the dialogue advance automatially? </summary>
        [DataMember] public bool AdvanceDialogueAutomatically;

        /// <summary> If this dialogue automatically advances, should it also display an 
        /// "end" / "continue" button? </summary>
        [DataMember] public bool AutoAdvanceShouldDisplayOption;

        /// <summary>  The time it will take for the Dialogue to automaically advance </summary>
        [DataMember] public float TimeUntilAdvance;


        //--------
        // Deprecated

        /// <summary> Deprecated as of V1.1 </summary>
        public List<EditableOptionNode> Options;
        /// <summary> Deprecated as of V1.1 </summary>
        [DataMember] public List<int> OptionUIDs;
        /// <summary> Deprecated as of V1.1 </summary>
        public EditableSpeechNode Speech;
        /// <summary> Deprecated as of V1.1 </summary>
        [DataMember] public int SpeechUID;


        // ------------------------------

        public void AddOption(EditableOptionNode newOption)
        {
            // Remove any speech connections I may have
            if (this.Connections.Count > 0 && this.Connections[0] is EditableSpeechConnection)
            {
                // I am no longer a parent of these speechs'
                for (int i = 0; i < Connections.Count; i++)
                {
                    (Connections[0] as EditableSpeechConnection).Speech.parents.Remove(this);
                }
                Connections.Clear();
            }

            // Connection to this option already exists
            if (Connections.Count > 0 && Connections[0] is EditableOptionConnection)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    if ((Connections[0] as EditableOptionConnection).Option == newOption)
                        return;
                }
            }

            // Setup option connection
            this.Connections.Add(new EditableOptionConnection(newOption));
            if (!newOption.parents.Contains(this))
                newOption.parents.Add(this);
        }

        public void AddSpeech(EditableSpeechNode newSpeech)
        {
            // Remove any option connections I may have
            if (this.Connections.Count > 0 && this.Connections[0] is EditableOptionConnection)
            {
                // I am no longer a parent of these speechs'
                for (int i = 0; i < Connections.Count; i++)
                {
                    (Connections[0] as EditableOptionConnection).Option.parents.Remove(this);
                }
                Connections.Clear();
            }

            // Connection to this speech already exists
            if (Connections.Count > 0 && Connections[0] is EditableSpeechConnection)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    if ((Connections[0] as EditableSpeechConnection).Speech == newSpeech)
                        return;
                }
            }

            // If a relationship the other-way-around between these speechs already exists, swap it. 
            // A 2way speech<->speech relationship cannot exist.
            if (this.parents.Contains(newSpeech))
            {
                this.parents.Remove(newSpeech);
                newSpeech.DeleteConnectionChild(this);
            }

            // Setup option connection
            this.Connections.Add(new EditableSpeechConnection(newSpeech));
            if (!newSpeech.parents.Contains(this))
                newSpeech.parents.Add(this);
        }

        public override void SerializeAssetData(NPCConversation conversation)
        {
            base.SerializeAssetData(conversation);

            conversation.GetNodeData(this.ID).Audio = this.Audio;
            conversation.GetNodeData(this.ID).Icon = this.Icon;
        }

        public override void DeserializeAssetData(NPCConversation conversation)
        {
            base.DeserializeAssetData(conversation);

            this.Audio = conversation.GetNodeData(this.ID).Audio;
            this.Icon = conversation.GetNodeData(this.ID).Icon;

#if UNITY_EDITOR
            // If under V1.03, Load from database via GUID, so data is not lost for people who are upgrading
            if (conversation.Version < (int)eSaveVersion.V1_03)
            {
                if (this.Audio == null)
                {
                    if (!string.IsNullOrEmpty(AudioGUID))
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(AudioGUID);
                        this.Audio = (AudioClip)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(AudioClip));

                    }
                }

                if (this.Icon == null)
                {
                    if (!string.IsNullOrEmpty(IconGUID))
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(IconGUID);
                        this.Icon = (Sprite)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));

                    }
                }
            }
#endif
        }
    }




    [DataContract]
    public class EditableOptionNode : EditableConversationNode
    {
        public EditableOptionNode() : base()
        {
            SpeechUID = EditableConversation.INVALID_UID;
        }

        public override eNodeType NodeType { get { return eNodeType.Option; } }


        /// <summary> Deprecated as of V1.1 </summary>
        public EditableSpeechNode Speech;
        /// <summary> Deprecated as of V1.1 </summary>
        [DataMember] public int SpeechUID;


        // ------------------------------

        public void AddSpeech(EditableSpeechNode newSpeech)
        {
            // Add new speech connection
            this.Connections.Add(new EditableSpeechConnection(newSpeech));
            newSpeech.parents.Add(this);
        }
    }
}