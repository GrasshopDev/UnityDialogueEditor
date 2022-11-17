using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DialogueEditor
{
    public enum eSaveVersion
    {
        V1_03 = 103,    // Initial save data
        V1_10 = 110,    // Parameters
    }
    

    //--------------------------------------
    // Conversation Monobehaviour (Serialized)
    //--------------------------------------

    [System.Serializable]
    [DisallowMultipleComponent]
    public class NPCConversation : MonoBehaviour
    {
        // Consts
        /// <summary> Version 1.10 </summary>
        public const int CurrentVersion = (int)eSaveVersion.V1_10;
        private readonly string CHILD_NAME = "ConversationEventInfo";

        // Getters
        public int Version { get { return saveVersion; } }

        // Serialized data
        [SerializeField] public int CurrentIDCounter = 1;
        [SerializeField] private string json;
        [SerializeField] private int saveVersion;
        [SerializeField] public string DefaultName;
        [SerializeField] public Sprite DefaultSprite;
        [SerializeField] public TMPro.TMP_FontAsset DefaultFont;
        [FormerlySerializedAs("Events")]
        [SerializeField] private List<NodeEventHolder> NodeSerializedDataList;
        [SerializeField] public TMPro.TMP_FontAsset ContinueFont;
        [SerializeField] public TMPro.TMP_FontAsset EndConversationFont;

        // Runtime vars
        public UnityEngine.Events.UnityEvent Event;
        public List<EditableParameter> ParameterList; // Serialized into the json string

        


        //--------------------------------------
        // Util
        //--------------------------------------

        public NodeEventHolder GetNodeData(int id)
        {
            // Create list if none
            if (NodeSerializedDataList == null)
                NodeSerializedDataList = new List<NodeEventHolder>();

            // Look through list to find by ID
            for (int i = 0; i < NodeSerializedDataList.Count; i++)
                if (NodeSerializedDataList[i].NodeID == id)
                    return NodeSerializedDataList[i];

            // If none exist, create a new GameObject
            Transform EventInfo = this.transform.Find(CHILD_NAME);
            if (EventInfo == null)
            {
                GameObject obj = new GameObject(CHILD_NAME);
                obj.transform.SetParent(this.transform);
            }
            EventInfo = this.transform.Find(CHILD_NAME);

            // Add a new Component for this node
            NodeEventHolder h = EventInfo.gameObject.AddComponent<NodeEventHolder>();
            h.NodeID = id;
            h.Event = new UnityEngine.Events.UnityEvent();
            NodeSerializedDataList.Add(h);
            return h;
        }

        public void DeleteDataForNode(int id)
        {
            if (NodeSerializedDataList == null)
                return;

            for (int i = 0; i < NodeSerializedDataList.Count; i++)
            {
                if (NodeSerializedDataList[i].NodeID == id)
                {
                    GameObject.DestroyImmediate(NodeSerializedDataList[i]);
                    NodeSerializedDataList.RemoveAt(i);
                }
            }
        }

        public EditableParameter GetParameter(string name)
        {
            for (int i = 0; i < this.ParameterList.Count; i++)
            {
                if (ParameterList[i].ParameterName == name)
                {
                    return ParameterList[i];
                }
            }
            return null;
        }




        //--------------------------------------
        // Serialize and Deserialize
        //--------------------------------------

        public void Serialize(EditableConversation conversation)
        {
            saveVersion = CurrentVersion;

            conversation.Parameters = this.ParameterList;
            json = Jsonify(conversation);
        }

        public Conversation Deserialize()
        {
            // Deserialize an editor-version (containing all info) that 
            // we will use to construct the user-facing Conversation data structure. 
            EditableConversation ec = this.DeserializeForEditor();

            return ConstructConversationObject(ec);
        }

        public EditableConversation DeserializeForEditor()
        {
            // Dejsonify 
            EditableConversation conversation = Dejsonify();
            
            if (conversation != null)
            {
                // Copy the param list
                this.ParameterList = conversation.Parameters;

                // Deserialize the indivudual nodes
                {
                    if (conversation.SpeechNodes != null)
                        for (int i = 0; i < conversation.SpeechNodes.Count; i++)
                            conversation.SpeechNodes[i].DeserializeAssetData(this);

                    if (conversation.Options != null)
                        for (int i = 0; i < conversation.Options.Count; i++)
                            conversation.Options[i].DeserializeAssetData(this);
                }
            }
            else
            {
                conversation = new EditableConversation();
            }

            conversation.SaveVersion = this.saveVersion;

            // Clear our dummy event
            Event = new UnityEngine.Events.UnityEvent();

            // Reconstruct
            ReconstructEditableConversation(conversation);

            return conversation;
        }

        private void ReconstructEditableConversation(EditableConversation conversation)
        {
            if (conversation == null)
                conversation = new EditableConversation();

            // Get a list of every node in the conversation
            List<EditableConversationNode> allNodes = new List<EditableConversationNode>();
            for (int i = 0; i < conversation.SpeechNodes.Count; i++)
                allNodes.Add(conversation.SpeechNodes[i]);
            for (int i = 0; i < conversation.Options.Count; i++)
                allNodes.Add(conversation.Options[i]);

            // For every node: 
            // Find the children and parents by UID
            for (int i = 0; i < allNodes.Count; i++)
            {
                // New parents list 
                allNodes[i].parents = new List<EditableConversationNode>();

                // Get parents by UIDs
                //-----------------------------------------------------------------------------
                // UPDATE:  This behaviour has now been removed. Later in this function, 
                //          the child->parent connections are constructed by using the 
                //          parent->child connections. Having both of these behaviours run 
                //          results in each parent being in the "parents" list twice. 
                // 
                // for (int j = 0; j < allNodes[i].parentUIDs.Count; j++)
                // {
                //     allNodes[i].parents.Add(conversation.GetNodeByUID(allNodes[i].parentUIDs[j]));
                // }
                //-----------------------------------------------------------------------------

                // Construct the parent->child connections
                //
                // V1.03
                if (conversation.SaveVersion <= (int)eSaveVersion.V1_03)
                {
                    // Construct Connections from the OptionUIDs and SpeechUIDs (which are now deprecated)
                    // This supports upgrading from V1.03 +

                    allNodes[i].Connections = new List<EditableConnection>();
                    allNodes[i].ParamActions = new List<EditableSetParamAction>();

                    if (allNodes[i].NodeType == EditableConversationNode.eNodeType.Speech)
                    {
                        EditableSpeechNode thisSpeech = allNodes[i] as EditableSpeechNode;

                        // Speech options
                        int count = thisSpeech.OptionUIDs.Count;
                        for (int j = 0; j < count; j++)
                        {
                            int optionUID = thisSpeech.OptionUIDs[j];
                            EditableOptionNode option = conversation.GetOptionByUID(optionUID);

                            thisSpeech.Connections.Add(new EditableOptionConnection(option));
                        }

                        // Speech following speech
                        {
                            int speechUID = thisSpeech.SpeechUID;
                            EditableSpeechNode speech = conversation.GetSpeechByUID(speechUID);

                            if (speech != null)
                            {
                                thisSpeech.Connections.Add(new EditableSpeechConnection(speech));
                            }
                        }
                    }
                    else if (allNodes[i] is EditableOptionNode)
                    {
                        int speechUID = (allNodes[i] as EditableOptionNode).SpeechUID;
                        EditableSpeechNode speech = conversation.GetSpeechByUID(speechUID);

                        if (speech != null)
                        {
                            allNodes[i].Connections.Add(new EditableSpeechConnection(speech));
                        }
                    }
                }
                //
                // V1.10 +
                else
                {
                    // For each node..  Reconstruct the connections
                    for (int j = 0; j < allNodes[i].Connections.Count; j++)
                    {
                        if (allNodes[i].Connections[j] is EditableSpeechConnection)
                        {
                            EditableSpeechNode speech = conversation.GetSpeechByUID(allNodes[i].Connections[j].NodeUID);
                            (allNodes[i].Connections[j] as EditableSpeechConnection).Speech = speech;
                        }
                        else if (allNodes[i].Connections[j] is EditableOptionConnection)
                        {
                            EditableOptionNode option = conversation.GetOptionByUID(allNodes[i].Connections[j].NodeUID);
                            (allNodes[i].Connections[j] as EditableOptionConnection).Option = option;
                        }
                    }
                }
            }

            // For every node: 
            // Tell any of the nodes children that the node is the childs parent
            for (int i = 0; i < allNodes.Count; i++)
            {
                EditableConversationNode thisNode = allNodes[i];

                for (int j = 0; j < thisNode.Connections.Count; j++)
                {
                    if (thisNode.Connections[j].ConnectionType == EditableConnection.eConnectiontype.Speech)
                    {
                        (thisNode.Connections[j] as EditableSpeechConnection).Speech.parents.Add(thisNode);
                    }
                    else if (thisNode.Connections[j].ConnectionType == EditableConnection.eConnectiontype.Option)
                    {
                        (thisNode.Connections[j] as EditableOptionConnection).Option.parents.Add(thisNode);
                    }
                }
            }
        }

        private string Jsonify(EditableConversation conversation)
        {
            if (conversation == null || conversation.Options == null) { return ""; }

            System.IO.MemoryStream ms = new System.IO.MemoryStream();

            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(EditableConversation));
            ser.WriteObject(ms, conversation);
            byte[] jsonData = ms.ToArray();
            ms.Close();
            string toJson = System.Text.Encoding.UTF8.GetString(jsonData, 0, jsonData.Length);

            return toJson;
        }

        private EditableConversation Dejsonify()
        {
            if (json == null || json == "")
                return null;

            EditableConversation conversation = new EditableConversation();
            System.IO.MemoryStream ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            DataContractJsonSerializer ser = new DataContractJsonSerializer(conversation.GetType());
            conversation = ser.ReadObject(ms) as EditableConversation;
            ms.Close();

            return conversation;
        }




        //--------------------------------------
        // Construct User-Facing Conversation Object and Nodes
        //--------------------------------------

        private Conversation ConstructConversationObject(EditableConversation ec)
        {
            // Create a conversation object
            Conversation conversation = new Conversation();

            // Construct the parameters
            CreateParameters(ec, conversation);

            // Construct the Conversation-Based variables (not node-based)
            conversation.ContinueFont = this.ContinueFont;
            conversation.EndConversationFont = this.EndConversationFont;

            // Create a dictionary to store our created nodes by UID
            Dictionary<int, SpeechNode> speechByID = new Dictionary<int, SpeechNode>();
            Dictionary<int, OptionNode> optionsByID = new Dictionary<int, OptionNode>();

            // Create a Dialogue and Option node for each in the conversation
            // Put them in the dictionary
            for (int i = 0; i < ec.SpeechNodes.Count; i++)
            {
                SpeechNode node = CreateSpeechNode(ec.SpeechNodes[i]);
                speechByID.Add(ec.SpeechNodes[i].ID, node);
            }

            for (int i = 0; i < ec.Options.Count; i++)
            {
                OptionNode node = CreateOptionNode(ec.Options[i]);
                optionsByID.Add(ec.Options[i].ID, node);
            }

            // Now that we have every node in the dictionary, reconstruct the tree 
            // And also look for the root
            ReconstructTree(ec, conversation, speechByID, optionsByID);

            return conversation;
        }

        private void CreateParameters(EditableConversation ec, Conversation conversation)
        {
            for (int i = 0; i < ec.Parameters.Count; i++)
            {
                if (ec.Parameters[i].ParameterType == EditableParameter.eParamType.Bool)
                {
                    EditableBoolParameter editableParam = ec.Parameters[i] as EditableBoolParameter;
                    BoolParameter boolParam = new BoolParameter(editableParam.ParameterName, editableParam.BoolValue);
                    conversation.Parameters.Add(boolParam);
                }
                else if (ec.Parameters[i].ParameterType == EditableParameter.eParamType.Int)
                {
                    EditableIntParameter editableParam = ec.Parameters[i] as EditableIntParameter;
                    IntParameter intParam = new IntParameter(editableParam.ParameterName, editableParam.IntValue);
                    conversation.Parameters.Add(intParam);
                }
            }
        }

        private SpeechNode CreateSpeechNode(EditableSpeechNode editableNode)
        {
            SpeechNode speech = new SpeechNode();
            speech.Name = editableNode.Name;
            speech.Text = editableNode.Text;
            speech.AutomaticallyAdvance = editableNode.AdvanceDialogueAutomatically;
            speech.AutoAdvanceShouldDisplayOption = editableNode.AutoAdvanceShouldDisplayOption;
            speech.TimeUntilAdvance = editableNode.TimeUntilAdvance;
            speech.TMPFont = editableNode.TMPFont;
            speech.Icon = editableNode.Icon;
            speech.Audio = editableNode.Audio;
            speech.Volume = editableNode.Volume;

            CopyParamActions(editableNode, speech);

            NodeEventHolder holder = this.GetNodeData(editableNode.ID);
            if (holder != null)
            {
                speech.Event = holder.Event;
            }

            return speech;
        }

        private OptionNode CreateOptionNode(EditableOptionNode editableNode)
        {
            OptionNode option = new OptionNode();
            option.Text = editableNode.Text;
            option.TMPFont = editableNode.TMPFont;

            CopyParamActions(editableNode, option);

            NodeEventHolder holder = this.GetNodeData(editableNode.ID);
            if (holder != null)
            {
                option.Event = holder.Event;
            }

            return option;
        }

        public void CopyParamActions(EditableConversationNode editable, ConversationNode node)
        {
            node.ParamActions = new List<SetParamAction>();

            for (int i = 0; i < editable.ParamActions.Count; i++)
            {
                if (editable.ParamActions[i].ParamActionType == EditableSetParamAction.eParamActionType.Int)
                {
                    EditableSetIntParamAction setIntEditable = editable.ParamActions[i] as EditableSetIntParamAction;

                    SetIntParamAction setInt = new SetIntParamAction();
                    setInt.ParameterName = setIntEditable.ParameterName;
                    setInt.Value = setIntEditable.Value;
                    node.ParamActions.Add(setInt);
                }
                else if (editable.ParamActions[i].ParamActionType == EditableSetParamAction.eParamActionType.Bool)
                {
                    EditableSetBoolParamAction setBoolEditable = editable.ParamActions[i] as EditableSetBoolParamAction;

                    SetBoolParamAction setBool = new SetBoolParamAction();
                    setBool.ParameterName = setBoolEditable.ParameterName;
                    setBool.Value = setBoolEditable.Value;
                    node.ParamActions.Add(setBool);
                }
            }
        }

        private void ReconstructTree(EditableConversation ec, Conversation conversation, Dictionary<int, SpeechNode> dialogues, Dictionary<int, OptionNode> options)
        {
            // Speech nodes
            List<EditableSpeechNode> editableSpeechNodes = ec.SpeechNodes;
            for (int i = 0; i < editableSpeechNodes.Count; i++)
            {
                EditableSpeechNode editableNode = editableSpeechNodes[i];
                SpeechNode speechNode = dialogues[editableNode.ID];

                // Connections
                List<EditableConnection> editableConnections = editableNode.Connections;
                for (int j = 0; j < editableConnections.Count; j++)
                {

                    int childID = editableConnections[j].NodeUID;

                    // Construct node->Speech
                    if (editableConnections[j].ConnectionType == EditableConnection.eConnectiontype.Speech)
                    {
                        SpeechConnection connection = new SpeechConnection(dialogues[childID]);
                        CopyConnectionConditions(editableConnections[j], connection);
                        speechNode.Connections.Add(connection);
                    }
                    // Construct node->Option
                    else if (editableConnections[j].ConnectionType == EditableConnection.eConnectiontype.Option)
                    {
                        OptionConnection connection = new OptionConnection(options[childID]);
                        CopyConnectionConditions(editableConnections[j], connection);
                        speechNode.Connections.Add(connection);
                    }
                }

                // Root?
                if (editableNode.EditorInfo.isRoot)
                {
                    conversation.Root = dialogues[editableNode.ID];
                }
            }


            // Option nodes
            List<EditableOptionNode> editableOptionNodes = ec.Options;
            for (int i = 0; i < editableOptionNodes.Count; i++)
            {
                EditableOptionNode editableNode = editableOptionNodes[i];
                OptionNode optionNode = options[editableNode.ID];

                // Connections
                List<EditableConnection> editableConnections = editableNode.Connections;
                for (int j = 0; j < editableConnections.Count; j++)
                {
                    int childID = editableConnections[j].NodeUID;

                    // Construct node->Speech
                    if (editableConnections[j].ConnectionType == EditableConnection.eConnectiontype.Speech)
                    {
                        SpeechConnection connection = new SpeechConnection(dialogues[childID]);
                        CopyConnectionConditions(editableConnections[j], connection);
                        optionNode.Connections.Add(connection);
                    }
                }
            }
        }

        private void CopyConnectionConditions(EditableConnection editableConnection, Connection connection)
        {
            List<EditableCondition> editableConditions = editableConnection.Conditions;
            for (int i = 0; i < editableConditions.Count; i++)
            {
                if (editableConditions[i].ConditionType == EditableCondition.eConditionType.BoolCondition)
                {
                    EditableBoolCondition ebc = editableConditions[i] as EditableBoolCondition;

                    BoolCondition bc = new BoolCondition();
                    bc.ParameterName = ebc.ParameterName;
                    switch (ebc.CheckType)
                    {
                        case EditableBoolCondition.eCheckType.equal:
                            bc.CheckType = BoolCondition.eCheckType.equal;
                            break;
                        case EditableBoolCondition.eCheckType.notEqual:
                            bc.CheckType = BoolCondition.eCheckType.notEqual;
                            break;
                    }
                    bc.RequiredValue = ebc.RequiredValue;

                    connection.Conditions.Add(bc);
                }
                else if (editableConditions[i].ConditionType == EditableCondition.eConditionType.IntCondition)
                {
                    EditableIntCondition eic = editableConditions[i] as EditableIntCondition;

                    IntCondition ic = new IntCondition();
                    ic.ParameterName = eic.ParameterName;
                    switch (eic.CheckType)
                    {
                        case EditableIntCondition.eCheckType.equal:
                            ic.CheckType = IntCondition.eCheckType.equal;
                            break;
                        case EditableIntCondition.eCheckType.greaterThan:
                            ic.CheckType = IntCondition.eCheckType.greaterThan;
                            break;
                        case EditableIntCondition.eCheckType.lessThan:
                            ic.CheckType = IntCondition.eCheckType.lessThan;
                            break;
                    }
                    ic.RequiredValue = eic.RequiredValue;

                    connection.Conditions.Add(ic);
                }
            }
        }
    }
}