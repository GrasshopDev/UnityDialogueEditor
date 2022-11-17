using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DialogueEditor
{
    public class DialogueEditorWindow : EditorWindow
    {
        public abstract class SelectableUI
        {
            public enum eType
            {
                Node,
                Connection
            }

            public abstract eType Type { get; }           
        }

        public class SelectableUINode : SelectableUI
        {
            public SelectableUINode(UINode node)
            {
                this.Node = node;
            }

            public override eType Type { get { return eType.Node; } }
            public UINode Node { get; private set; }
        }

        public class SelectableUIConnection : SelectableUI
        {
            public SelectableUIConnection(EditableConnection connection)
            {
                this.Connection = connection;
            }

            public override eType Type { get { return eType.Connection; } }
            public EditableConnection Connection { get; private set; }
        }

        public enum eInputState
        {
            Regular,
            PlacingOption,
            PlacingSpeech,
            ConnectingNode,         
            draggingPanel,
        }

        // Consts
        public const float TOOLBAR_HEIGHT = 17;
        public const float START_PANEL_WIDTH = 250;
        private const float PANEL_RESIZER_PADDING = 5;
        private const string WINDOW_NAME = "DIALOGUE_EDITOR_WINDOW";
        private const string HELP_URL = "https://grasshopdev.github.io/dialogueeditor.html";
        private const string CONTROL_NAME = "DEFAULT_CONTROL";
        public const int MIN_PANEL_WIDTH = 180;
        private const string UNAVAILABLE_DURING_PLAY_TEXT = "Dialogue Editor unavaiable during play mode.";

        // Static properties
        public static bool SelectableClickedOnThisUpdate { get; set; }
        private static SelectableUI CurrentlySelectedObject { get; set; }

        // Private variables:     
        private NPCConversation CurrentAsset;           // The Conversation scriptable object that is currently being viewed/edited
        public static EditableSpeechNode ConversationRoot { get; private set; }    // The root node of the conversation
        private List<UINode> uiNodes;                   // List of all UI nodes

        // Selected asset logic
        private NPCConversation currentlySelectedAsset;
        private Transform newlySelectedAsset;

        // Right-hand display pannel vars
        private float panelWidth;
        private Rect panelRect;
        private GUIStyle panelStyle;
        private GUIStyle panelTitleStyle;
        private GUIStyle panelPropertyStyle;
        private Rect panelResizerRect;
        private GUIStyle resizerStyle;
        private SelectableUI m_cachedSelectedObject;

        // Dragging information
        private bool dragging;
        private bool clickInBox;
        private Vector2 offset;
        private Vector2 dragDelta;

        // Input and input-state logic
        private eInputState m_inputState;
        private UINode m_currentPlacingNode = null;
        private UINode m_currentConnectingNode = null;
        private EditableConversationNode m_connectionDeleteParent, m_connectionDeleteChild;




        //--------------------------------------
        // Open window
        //--------------------------------------

        [MenuItem("Window/DialogueEditor")]
        public static DialogueEditorWindow ShowWindow()
        {
            return EditorWindow.GetWindow<DialogueEditorWindow>("Dialogue Editor");
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OpenDialogue(int assetInstanceID, int line)
        {
            NPCConversation conversation = EditorUtility.InstanceIDToObject(assetInstanceID) as NPCConversation;

            if (conversation != null)
            {
                DialogueEditorWindow window = ShowWindow();
                window.LoadNewAsset(conversation);
                return true;
            }
            return false;
        }




        //--------------------------------------
        // Load New Asset
        //--------------------------------------

        public void LoadNewAsset(NPCConversation asset)
        {
            if (Application.isPlaying)
            {
                Log("Load new asset aborted. Will not open assets during play.");
                return;
            }

            CurrentAsset = asset;
            Log("Loading new asset: " + CurrentAsset.name);

            // Clear all current UI Nodes
            if (uiNodes == null)
            {
                uiNodes = new List<UINode>();
            }
            uiNodes.Clear();

            // Deseralize the asset and get the conversation root
            EditableConversation conversation = CurrentAsset.DeserializeForEditor();

            // Get root
            ConversationRoot = conversation.GetRootNode();

            // If it's null, create a root
            if (ConversationRoot == null)
            {
                ConversationRoot = new EditableSpeechNode();
                ConversationRoot.EditorInfo.xPos = (Screen.width / 2) - (UISpeechNode.Width / 2);
                ConversationRoot.EditorInfo.yPos = 0;
                ConversationRoot.EditorInfo.isRoot = true;
                conversation.SpeechNodes.Add(ConversationRoot);
            }

            // Create UI
            RecreateUI(conversation);

            // Refresh the Editor window
            Recenter();
            Repaint();

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
#endif
        }

        

        public void RecreateUI(EditableConversation conversation)
        {
            // Get a list of every node in the conversation
            List<EditableConversationNode> allNodes = new List<EditableConversationNode>();
            for (int i = 0; i < conversation.SpeechNodes.Count; i++)
                allNodes.Add(conversation.SpeechNodes[i]);
            for (int i = 0; i < conversation.Options.Count; i++)
                allNodes.Add(conversation.Options[i]);

            // For every node: 
            // Create a corresponding UI Node to represent it, and add it to the list
            // 2: Tell any of the nodes children that the node is the childs parent
            for (int i = 0; i < allNodes.Count; i++)
            {
                EditableConversationNode thisNode = allNodes[i];

                // 1
                if (thisNode.NodeType == EditableConversationNode.eNodeType.Speech)
                {
                    UISpeechNode uiNode = new UISpeechNode(thisNode, new Vector2(thisNode.EditorInfo.xPos, thisNode.EditorInfo.yPos));
                    uiNodes.Add(uiNode);
                }
                else if (thisNode.NodeType == EditableConversationNode.eNodeType.Option)
                {
                    UIOptionNode uiNode = new UIOptionNode(thisNode, new Vector2(thisNode.EditorInfo.xPos, thisNode.EditorInfo.yPos));
                    uiNodes.Add(uiNode);
                }
            }

            Recenter();
            Repaint();
            MarkSceneDirty();
        }


        //--------------------------------------
        // OnEnable, OnDisable, OnFocus, LostFocus, 
        // Destroy, SelectionChange, ReloadScripts
        //--------------------------------------

        private void OnEnable()
        {
            if (uiNodes == null)
            {
                uiNodes = new List<UINode>();
            }

            InitGUIStyles();

            UINode.OnUINodeSelected += SelectNode;
            UINode.OnUINodeDeleted += DeleteUINode;
            UISpeechNode.OnCreateOption += CreateNewOption;
            UIOptionNode.OnCreateSpeech += CreateNewSpeech;
            UISpeechNode.OnConnect += ConnectNode;

            this.name = WINDOW_NAME;
            panelWidth = START_PANEL_WIDTH;

            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        private void InitGUIStyles()
        {
            // Panel style
            panelStyle = new GUIStyle();
            panelStyle.normal.background = DialogueEditorUtil.MakeTexture(10, 10, DialogueEditorUtil.GetEditorColor());

            // Panel title style
            panelTitleStyle = new GUIStyle();
            panelTitleStyle.alignment = TextAnchor.MiddleCenter;
            panelTitleStyle.fontStyle = FontStyle.Bold;
            panelTitleStyle.wordWrap = true;
            if (EditorGUIUtility.isProSkin)
            {
                panelTitleStyle.normal.textColor = DialogueEditorUtil.ProSkinTextColour;
            }


            // Resizer style
            resizerStyle = new GUIStyle();
            resizerStyle.normal.background = EditorGUIUtility.Load("icons/d_AvatarBlendBackground.png") as Texture2D;
        }

        private void OnDisable()
        {
            UINode.OnUINodeSelected -= SelectNode;
            UINode.OnUINodeDeleted -= DeleteUINode;
            UISpeechNode.OnCreateOption -= CreateNewOption;
            UIOptionNode.OnCreateSpeech -= CreateNewSpeech;
            UISpeechNode.OnConnect -= ConnectNode;

            EditorApplication.playModeStateChanged -= PlayModeStateChanged;

            if (Application.isPlaying)
            {
                return;
            }

            Log("Saving. Reason: Disable.");
            Save();
        }

        protected void OnFocus()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (uiNodes == null)
            {
                uiNodes = new List<UINode>();
            }

            // Get asset the user is selecting
            newlySelectedAsset = Selection.activeTransform;

            // If it's not null
            if (newlySelectedAsset != null)
            {
                // If its a conversation scriptable, load new asset
                if (newlySelectedAsset.GetComponent<NPCConversation>() != null)
                {
                    currentlySelectedAsset = newlySelectedAsset.GetComponent<NPCConversation>();

                    if (currentlySelectedAsset != CurrentAsset)
                    {
                        LoadNewAsset(currentlySelectedAsset);
                    }
                }
            }
        }

        protected void OnLostFocus()
        {
            if (Application.isPlaying)
            {
                return;
            }

            bool keepOnWindow = EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.titleContent.text.Equals("Dialogue Editor");

            if (CurrentAsset != null && !keepOnWindow)
            {
                Log("Saving conversation. Reason: Window Lost Focus.");
                Save();
            }
        }

        protected void OnDestroy()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Log("Saving conversation. Reason: Window closed.");
            Save();
        }

        protected void OnSelectionChange()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (uiNodes == null)
            {
                uiNodes = new List<UINode>();
            }

            // Get asset the user is selecting
            newlySelectedAsset = Selection.activeTransform;

            // If it's not null
            if (newlySelectedAsset != null)
            {
                // If it's a different asset and our current asset isn't null, save our current asset
                if (currentlySelectedAsset != null && currentlySelectedAsset != newlySelectedAsset)
                {
                    Log("Saving conversation. Reason: Different asset selected");
                    Save();
                    currentlySelectedAsset = null;
                }

                // If its a conversation scriptable, load new asset
                currentlySelectedAsset = newlySelectedAsset.GetComponent<NPCConversation>();
                if (currentlySelectedAsset != null && currentlySelectedAsset != CurrentAsset)
                {
                    LoadNewAsset(currentlySelectedAsset);
                }
                else
                {
                    CurrentAsset = null;
                    Repaint();
                }
            }
            else
            {
                Log("Saving conversation. Reason: Conversation asset de-selected");
                Save();

                CurrentAsset = null;
                currentlySelectedAsset = null;
                Repaint();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            // Clear our reffrence to the CurrentAsset on script reload in order to prevent 
            // save detection overwriting the object with an empty conversation (save triggerred 
            // with no uiNodes present in window due to recompile). 

            // UPDATE 2021/04/23 - This is no longer neccessary as a better fix has been put in place. Thus, 
            // this can be commented out, and this also prevents the window from opening up again after every recompile. 

            //Log("Scripts reloaded. Clearing current asset.");
            //ShowWindow().CurrentAsset = null;
        }



        //--------------------------------------
        // Update
        //--------------------------------------

        private void Update()
        {
            if (Application.isPlaying) { return; }

            switch (m_inputState)
            {
                case eInputState.PlacingOption:
                case eInputState.PlacingSpeech:
                    Repaint();
                    break;
            }
        }



        //--------------------------------------
        // Draw
        //--------------------------------------

        private void OnGUI()
        {
            if (Application.isPlaying)
            {
                DrawMessageDuringPlay();
                return;
            }

            if (CurrentAsset == null)
            {
                DrawTitleBar();
                if (GUI.changed)
                {
                    Repaint();
                }
                return;
            }

            // Process interactions
            ProcessInput();

            // Draw
            DrawGrid(20, 0.2f, Color.gray);
            DrawGrid(100, 0.4f, Color.gray);
            DrawConnections();
            DrawNodes();
            DrawPanel();
            DrawResizer();
            DrawTitleBar();

            if (GUI.changed)
                Repaint();
        }

        private void DrawMessageDuringPlay()
        {
            float width = this.position.width;
            float centerX = width / 2;
            float height = this.position.height;
            float centerY = height / 2;
            Vector2 textDimensions = GUI.skin.label.CalcSize(new GUIContent(UNAVAILABLE_DURING_PLAY_TEXT));
            Rect textRect = new Rect(centerX - (textDimensions.x / 2), centerY - (textDimensions.y / 2), textDimensions.x, textDimensions.y);
            EditorGUI.LabelField(textRect, UNAVAILABLE_DURING_PLAY_TEXT);
        }

        private void DrawTitleBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Reset view", EditorStyles.toolbarButton))
            {
                Recenter();
            }
            if (GUILayout.Button("Reset panel", EditorStyles.toolbarButton))
            {
                ResetPanelSize();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Manual Save", EditorStyles.toolbarButton))
            {
                Save(true);
            }
            if (GUILayout.Button("Help", EditorStyles.toolbarButton))
            {
                Application.OpenURL(HELP_URL);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawNodes()
        {
            if (uiNodes != null)
            {
                for (int i = 0; i < uiNodes.Count; i++)
                {
                    uiNodes[i].Draw();
                }
            }
        }

        private void DrawConnections()
        {
            EditableConnection selectedConnection = null;
            if (CurrentlySelectedObject != null && CurrentlySelectedObject.Type == SelectableUI.eType.Connection)
            {
                selectedConnection = (CurrentlySelectedObject as SelectableUIConnection).Connection;

            }

            for (int i = 0; i < uiNodes.Count; i++)
            {
                uiNodes[i].DrawConnections(selectedConnection);
            }

            //----

            if (m_inputState == eInputState.ConnectingNode)
            {
                // Validate check
                if (m_currentConnectingNode == null)
                {
                    m_inputState = eInputState.Regular;
                    return;
                }

                Vector2 start, end;
                start = new Vector2(
                    m_currentConnectingNode.rect.x + UIOptionNode.Width / 2,
                    m_currentConnectingNode.rect.y + UIOptionNode.Height / 2
                    );
                end = Event.current.mousePosition;

                Vector2 toOption = (start - end).normalized;
                Vector2 toSpeech = (end - start).normalized;

                Handles.DrawBezier(
                    start, end,
                    start + toSpeech * 50f,
                    end + toOption * 50f,
                    Color.black, null, 5f);

                Repaint();
            }
        }

        private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
        {
            int widthDivs = Mathf.CeilToInt(position.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(position.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

            offset += dragDelta * 0.5f;
            Vector3 newOffset = new Vector3(offset.x % gridSpacing, offset.y % gridSpacing, 0);

            // Vertical lines
            for (int i = 0; i < widthDivs; i++)
            {
                Vector3 start = new Vector3(gridSpacing * i, -gridSpacing, 0) + newOffset;
                Vector3 end = new Vector3(gridSpacing * i, position.height, 0f) + newOffset;
                Handles.DrawLine(start, end);
            }

            // Horitonzal lines
            for (int j = 0; j < heightDivs; j++)
            {
                Vector3 start = new Vector3(-gridSpacing, gridSpacing * j, 0) + newOffset;
                Vector3 end = new Vector3(position.width, gridSpacing * j, 0f) + newOffset;
                Handles.DrawLine(start, end);
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private Vector2 panelVerticalScroll;

        private void DrawPanel()
        {
            const int VERTICAL_GAP = 20;
            const int VERTICAL_PADDING = 10;

            panelRect = new Rect(position.width - panelWidth, TOOLBAR_HEIGHT, panelWidth, position.height - TOOLBAR_HEIGHT);
            if (panelStyle.normal.background == null)
                InitGUIStyles();
            GUILayout.BeginArea(panelRect, panelStyle);
            GUILayout.BeginVertical();
            panelVerticalScroll = GUILayout.BeginScrollView(panelVerticalScroll);

            GUI.SetNextControlName("CONTROL_TITLE");

            GUILayout.Space(10);

            if (CurrentlySelectedObject == null)
            {
                // Parameters
                if (CurrentAsset.ParameterList == null)
                    CurrentAsset.ParameterList = new List<EditableParameter>();

                GUILayout.Label("Conversation: " + CurrentAsset.gameObject.name, panelTitleStyle);
                GUILayout.Space(VERTICAL_GAP);

                GUILayout.Label("Parameters", panelTitleStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Add bool"))
                {
                    string newname = GetValidParamName("New bool");
                    CurrentAsset.ParameterList.Add(new EditableBoolParameter(newname));
                }
                if (GUILayout.Button("Add int"))
                {
                    string newname = GetValidParamName("New int");
                    CurrentAsset.ParameterList.Add(new EditableIntParameter(newname));
                }
                GUILayout.EndHorizontal();

                for (int i = 0; i < CurrentAsset.ParameterList.Count; i++)
                {
                    GUILayout.BeginHorizontal();

                    float paramNameWidth = panelWidth * 0.6f;
                    CurrentAsset.ParameterList[i].ParameterName = GUILayout.TextField(CurrentAsset.ParameterList[i].ParameterName, 
                        EditableParameter.MAX_NAME_SIZE, GUILayout.Width(paramNameWidth), GUILayout.ExpandWidth(false));

                    if (CurrentAsset.ParameterList[i] is EditableBoolParameter)
                    {
                        EditableBoolParameter param = CurrentAsset.ParameterList[i] as EditableBoolParameter;
                        param.BoolValue = EditorGUILayout.Toggle(param.BoolValue);
                    }
                    else if (CurrentAsset.ParameterList[i] is EditableIntParameter)
                    {
                        EditableIntParameter param = CurrentAsset.ParameterList[i] as EditableIntParameter;
                        param.IntValue = EditorGUILayout.IntField(param.IntValue);
                    }

                    if (GUILayout.Button("X"))
                    {
                        CurrentAsset.ParameterList.RemoveAt(i);
                        i--;
                    }

                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


                // Default options
                GUILayout.Label("Default Speech-Node values", panelTitleStyle);

                float labelWidth = panelWidth * 0.4f;
                float fieldWidth = panelWidth * 0.6f;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Default Name:", GUILayout.MinWidth(labelWidth), GUILayout.MaxWidth(labelWidth));
                CurrentAsset.DefaultName = EditorGUILayout.TextField(CurrentAsset.DefaultName, GUILayout.MaxWidth(fieldWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Default Icon:", GUILayout.MinWidth(labelWidth), GUILayout.MaxWidth(labelWidth));
                CurrentAsset.DefaultSprite = (Sprite)EditorGUILayout.ObjectField(CurrentAsset.DefaultSprite, typeof(Sprite), false, GUILayout.MaxWidth(fieldWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Default Font:", GUILayout.MinWidth(labelWidth), GUILayout.MaxWidth(labelWidth));
                CurrentAsset.DefaultFont = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(CurrentAsset.DefaultFont, typeof(TMPro.TMP_FontAsset), false, GUILayout.MaxWidth(fieldWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                // Font options
                GUILayout.Label("'Continue' and 'End' button font", panelTitleStyle);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("'Continue' font:", GUILayout.MinWidth(labelWidth), GUILayout.MaxWidth(labelWidth));
                CurrentAsset.ContinueFont = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(CurrentAsset.ContinueFont, typeof(TMPro.TMP_FontAsset), false, GUILayout.MaxWidth(fieldWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("'End' font:", GUILayout.MinWidth(labelWidth), GUILayout.MaxWidth(labelWidth));
                CurrentAsset.EndConversationFont = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(CurrentAsset.EndConversationFont, typeof(TMPro.TMP_FontAsset), false, GUILayout.MaxWidth(fieldWidth));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                bool differentNodeSelected = (m_cachedSelectedObject != CurrentlySelectedObject);
                m_cachedSelectedObject = CurrentlySelectedObject;
                if (differentNodeSelected)
                {
                    GUI.FocusControl(CONTROL_NAME);
                }

                if (CurrentlySelectedObject.Type == SelectableUI.eType.Node)
                {
                    UINode selectedNode = (CurrentlySelectedObject as SelectableUINode).Node;

                    if (selectedNode is UISpeechNode)
                    {
                        EditableSpeechNode node = (selectedNode.Info as EditableSpeechNode);
                        GUILayout.Label("[" + node.ID + "] NPC Dialogue Node.", panelTitleStyle);
                        EditorGUILayout.Space();

                        GUILayout.Label("Character Name", EditorStyles.boldLabel);
                        GUI.SetNextControlName(CONTROL_NAME);
                        node.Name = GUILayout.TextField(node.Name);
                        EditorGUILayout.Space();

                        GUILayout.Label("Dialogue", EditorStyles.boldLabel);
                        node.Text = GUILayout.TextArea(node.Text);
                        EditorGUILayout.Space();

                        // Advance
                        if (node.Connections.Count > 0 && node.Connections[0] is EditableSpeechConnection)
                        {
                            GUILayout.Label("Auto-Advance options", EditorStyles.boldLabel);
                            node.AdvanceDialogueAutomatically = EditorGUILayout.Toggle("Automatically Advance", node.AdvanceDialogueAutomatically);
                            if (node.AdvanceDialogueAutomatically)
                            {
                                node.AutoAdvanceShouldDisplayOption = EditorGUILayout.Toggle("Display continue option", node.AutoAdvanceShouldDisplayOption);
                                node.TimeUntilAdvance = EditorGUILayout.FloatField("Dialogue Time", node.TimeUntilAdvance);
                                if (node.TimeUntilAdvance < 0.1f)
                                    node.TimeUntilAdvance = 0.1f;
                            }
                            EditorGUILayout.Space();
                        }

                        GUILayout.Label("Icon", EditorStyles.boldLabel);
                        node.Icon = (Sprite)EditorGUILayout.ObjectField(node.Icon, typeof(Sprite), false, GUILayout.ExpandWidth(true));
                        EditorGUILayout.Space();

                        GUILayout.Label("Audio Options", EditorStyles.boldLabel);
                        GUILayout.Label("Audio");
                        node.Audio = (AudioClip)EditorGUILayout.ObjectField(node.Audio, typeof(AudioClip), false);

                        GUILayout.Label("Audio Volume");
                        node.Volume = EditorGUILayout.Slider(node.Volume, 0, 1);
                        EditorGUILayout.Space();

                        GUILayout.Label("TMP Font", EditorStyles.boldLabel);
                        node.TMPFont = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(node.TMPFont, typeof(TMPro.TMP_FontAsset), false);
                        EditorGUILayout.Space();

                        // Event
                        {
                            NodeEventHolder NodeEvent = CurrentAsset.GetNodeData(node.ID);
                            if (differentNodeSelected)
                            {
                                CurrentAsset.Event = NodeEvent.Event;
                            }

                            if (NodeEvent != null && NodeEvent.Event != null)
                            {
                                // Load the object and property of the node
                                SerializedObject o = new SerializedObject(NodeEvent);
                                SerializedProperty p = o.FindProperty("Event");

                                // Load the dummy event
                                SerializedObject o2 = new SerializedObject(CurrentAsset);
                                SerializedProperty p2 = o2.FindProperty("Event");

                                // Draw dummy event
                                GUILayout.Label("Events:", EditorStyles.boldLabel);
                                EditorGUILayout.PropertyField(p2);

                                // Apply changes to dummy
                                o2.ApplyModifiedProperties();

                                // Copy dummy changes onto the nodes event
                                p = p2;
                                o.ApplyModifiedProperties();
                            }
                        }

                        Panel_NodeParamActions(node);


                    }
                    else if (selectedNode is UIOptionNode)
                    {
                        EditableOptionNode node = (selectedNode.Info as EditableOptionNode);
                        GUILayout.Label("[" + node.ID + "] Option Node.", panelTitleStyle);
                        EditorGUILayout.Space();

                        GUILayout.Label("Option text:", EditorStyles.boldLabel);
                        node.Text = GUILayout.TextArea(node.Text);
                        EditorGUILayout.Space();

                        GUILayout.Label("TMP Font", EditorStyles.boldLabel);
                        node.TMPFont = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(node.TMPFont, typeof(TMPro.TMP_FontAsset), false);
                        EditorGUILayout.Space();


                        // Event
                        {
                            NodeEventHolder NodeEvent = CurrentAsset.GetNodeData(node.ID);
                            if (differentNodeSelected)
                            {
                                CurrentAsset.Event = NodeEvent.Event;
                            }

                            if (NodeEvent != null && NodeEvent.Event != null)
                            {
                                // Load the object and property of the node
                                SerializedObject o = new SerializedObject(NodeEvent);
                                SerializedProperty p = o.FindProperty("Event");

                                // Load the dummy event
                                SerializedObject o2 = new SerializedObject(CurrentAsset);
                                SerializedProperty p2 = o2.FindProperty("Event");

                                // Draw dummy event
                                GUILayout.Label("Events:", EditorStyles.boldLabel);
                                EditorGUILayout.PropertyField(p2);

                                // Apply changes to dummy
                                o2.ApplyModifiedProperties();

                                // Copy dummy changes onto the nodes event
                                p = p2;
                                o.ApplyModifiedProperties();
                            }
                        }

                        Panel_NodeParamActions(node);
                    }
                }
                else if (CurrentlySelectedObject.Type == SelectableUI.eType.Connection)
                {
                    GUILayout.Label("Connection.", panelTitleStyle);
                    EditorGUILayout.Space();

                    EditableConnection connection = (CurrentlySelectedObject as SelectableUIConnection).Connection;

                    // Validate conditions
                    for (int i = 0; i < connection.Conditions.Count; i++)
                    {
                        if (CurrentAsset.GetParameter(connection.Conditions[i].ParameterName) == null)
                        {
                            connection.Conditions.RemoveAt(i);
                            i--;
                        }
                    }


                    // Button
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Add condition"))
                        {
                            GenericMenu rightClickMenu = new GenericMenu();

                            for (int i = 0; i < this.CurrentAsset.ParameterList.Count; i++)
                            {
                                // Skip if node already has action for this param
                                if (ConnectionContainsParameter(connection, CurrentAsset.ParameterList[i].ParameterName))
                                {
                                    continue;
                                }

                                if (this.CurrentAsset.ParameterList[i].ParameterType == EditableParameter.eParamType.Int)
                                {
                                    EditableIntParameter intParam = CurrentAsset.ParameterList[i] as EditableIntParameter;
                                    rightClickMenu.AddItem(new GUIContent(intParam.ParameterName), false, delegate
                                    {
                                        connection.AddCondition(new EditableIntCondition(intParam.ParameterName));
                                    });
                                }
                                else if (this.CurrentAsset.ParameterList[i].ParameterType == EditableParameter.eParamType.Bool)
                                {
                                    EditableBoolParameter boolParam = CurrentAsset.ParameterList[i] as EditableBoolParameter;
                                    rightClickMenu.AddItem(new GUIContent(boolParam.ParameterName), false, delegate
                                    {
                                        connection.AddCondition(new EditableBoolCondition(boolParam.ParameterName));
                                    });
                                }
                            }

                            rightClickMenu.ShowAsContext();
                        }
                        GUILayout.EndHorizontal();
                    }

                    // Draw conditions
                    GUILayout.Space(VERTICAL_PADDING);
                    GUILayout.Label("Required conditions.", EditorStyles.boldLabel);
                    float conditionNameWidth = panelWidth * 0.4f;
                    for (int i = 0; i < connection.Conditions.Count; i++)
                    {
                        GUILayout.BeginHorizontal();

                        string name = connection.Conditions[i].ParameterName;
                        GUILayout.Label(name, GUILayout.MinWidth(conditionNameWidth), GUILayout.MaxWidth(conditionNameWidth));

                        if (connection.Conditions[i].ConditionType == EditableCondition.eConditionType.IntCondition)
                        {
                            EditableIntCondition intCond = connection.Conditions[i] as EditableIntCondition;

                            intCond.CheckType = (EditableIntCondition.eCheckType)EditorGUILayout.EnumPopup(intCond.CheckType);
                            intCond.RequiredValue = EditorGUILayout.IntField(intCond.RequiredValue);

                        }
                        else if (connection.Conditions[i].ConditionType == EditableCondition.eConditionType.BoolCondition)
                        {
                            EditableBoolCondition boolCond = connection.Conditions[i] as EditableBoolCondition;

                            boolCond.CheckType = (EditableBoolCondition.eCheckType)EditorGUILayout.EnumPopup(boolCond.CheckType);
                            boolCond.RequiredValue = EditorGUILayout.Toggle(boolCond.RequiredValue);
                        }

                        if (GUILayout.Button("X"))
                        {
                            connection.Conditions.RemoveAt(i);
                            i--;
                            GUI.changed = true;
                        }

                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawResizer()
        {
            panelResizerRect = new Rect(
                position.width - panelWidth - 2,
                0,
                5,
                (position.height) - TOOLBAR_HEIGHT);
            GUILayout.BeginArea(new Rect(panelResizerRect.position, new Vector2(2, position.height)), resizerStyle);
            GUILayout.EndArea();
        }

        private void Panel_NodeParamActions(EditableConversationNode node)
        {
            // Param Actions
            GUILayout.Label("Set Param:", EditorStyles.boldLabel);
            {
                // Button
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add Parameter Action"))
                    {
                        GenericMenu rightClickMenu = new GenericMenu();

                        for (int i = 0; i < this.CurrentAsset.ParameterList.Count; i++)
                        {
                            // Skip if node already has action for this param
                            if (NodeContainsSetParamAction(node, CurrentAsset.ParameterList[i].ParameterName))
                            {
                                continue;
                            }

                            if (this.CurrentAsset.ParameterList[i].ParameterType == EditableParameter.eParamType.Int)
                            {
                                EditableIntParameter intParam = CurrentAsset.ParameterList[i] as EditableIntParameter;
                                rightClickMenu.AddItem(new GUIContent(intParam.ParameterName), false, delegate
                                {
                                    node.ParamActions.Add(new EditableSetIntParamAction(intParam.ParameterName));
                                });
                            }
                            else if (this.CurrentAsset.ParameterList[i].ParameterType == EditableParameter.eParamType.Bool)
                            {
                                EditableBoolParameter boolParam = CurrentAsset.ParameterList[i] as EditableBoolParameter;
                                rightClickMenu.AddItem(new GUIContent(boolParam.ParameterName), false, delegate
                                {
                                    node.ParamActions.Add(new EditableSetBoolParamAction(boolParam.ParameterName));
                                });
                            }
                        }

                        rightClickMenu.ShowAsContext();
                    }
                    GUILayout.EndHorizontal();
                }

                // Validate all params exist
                for (int i = 0; i < node.ParamActions.Count; i++)
                {
                    if (CurrentAsset.GetParameter(node.ParamActions[i].ParameterName) == null)
                    {
                        node.ParamActions.RemoveAt(i);
                        i--;
                    }
                }

                // Draw
                float conditionNameWidth = panelWidth * 0.4f;
                for (int i = 0; i < node.ParamActions.Count; i++)
                {
                    GUILayout.BeginHorizontal();

                    string name = node.ParamActions[i].ParameterName;
                    GUILayout.Label(name, GUILayout.MinWidth(conditionNameWidth), GUILayout.MaxWidth(conditionNameWidth));

                    if (node.ParamActions[i].ParamActionType == EditableSetParamAction.eParamActionType.Int)
                    {
                        EditableSetIntParamAction intAction = node.ParamActions[i] as EditableSetIntParamAction;
                        intAction.Value = EditorGUILayout.IntField(intAction.Value);

                    }
                    else if (node.ParamActions[i].ParamActionType == EditableSetParamAction.eParamActionType.Bool)
                    {
                        EditableSetBoolParamAction boolAction = node.ParamActions[i] as EditableSetBoolParamAction;
                        boolAction.Value = EditorGUILayout.Toggle(boolAction.Value);
                    }

                    if (GUILayout.Button("X"))
                    {
                        node.ParamActions.RemoveAt(i);
                        i--;
                        GUI.changed = true;
                    }

                    GUILayout.EndHorizontal();
                }
            }
        }




        //--------------------------------------
        // Input
        //--------------------------------------

        private void ProcessInput()
        {
            Event e = Event.current;

            switch (m_inputState)
            {
                case eInputState.Regular:
                    bool inPanel = panelRect.Contains(e.mousePosition) || e.mousePosition.y < TOOLBAR_HEIGHT;
                    SelectableClickedOnThisUpdate = false;
                    ProcessNodeEvents(e, inPanel);
                    ProcessConnectionEvents(e, inPanel);
                    ProcessEvents(e);
                    break;

                case eInputState.draggingPanel:
                    panelWidth = (position.width - e.mousePosition.x);
                    if (panelWidth < MIN_PANEL_WIDTH)
                        panelWidth = MIN_PANEL_WIDTH;

                    if (e.type == EventType.MouseUp && e.button == 0)
                    {
                        m_inputState = eInputState.Regular;
                        e.Use();
                    }
                    Repaint();
                    break;

                case eInputState.PlacingOption:
                    m_currentPlacingNode.SetPosition(e.mousePosition);

                    // Left click
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        // Place the option
                        SelectNode(m_currentPlacingNode, true);
                        m_inputState = eInputState.Regular;
                        Repaint();
                        e.Use();
                    }
                    break;

                case eInputState.PlacingSpeech:
                    m_currentPlacingNode.SetPosition(e.mousePosition);

                    // Left click
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        // Place the option
                        SelectNode(m_currentPlacingNode, true);
                        m_inputState = eInputState.Regular;
                        Repaint();
                        e.Use();
                    }
                    break;

                case eInputState.ConnectingNode:
                    // Click.
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        // Loop through each node
                        for (int i = 0; i < uiNodes.Count; i++)
                        {
                            if (uiNodes[i] == m_currentConnectingNode)
                                continue;

                            // Clicked on node
                            if (uiNodes[i].rect.Contains(e.mousePosition))
                            {
                                UINode parent = m_currentConnectingNode;
                                UINode target = uiNodes[i];

                                // Connecting node->Option
                                if (target is UIOptionNode)
                                {
                                    UIOptionNode targetOption = target as UIOptionNode;

                                    // Only speech -> option is valid
                                    if (parent is UISpeechNode)
                                    {
                                        (parent as UISpeechNode).SpeechNode.AddOption(targetOption.OptionNode);
                                    }
                                }

                                // Connectingnode->Speech
                                else if (target is UISpeechNode)
                                {
                                    UISpeechNode targetSpeech = target as UISpeechNode;

                                    // Connect
                                    if (parent is UISpeechNode)
                                    {
                                        (parent as UISpeechNode).SpeechNode.AddSpeech(targetSpeech.SpeechNode);
                                    }
                                    else if (parent is UIOptionNode)
                                    {
                                        (parent as UIOptionNode).OptionNode.AddSpeech(targetSpeech.SpeechNode);
                                    }
                                }

                                m_inputState = eInputState.Regular;
                                e.Use();
                                break;
                            }
                        }
                    }

                    // Esc
                    if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                    {
                        m_inputState = eInputState.Regular;
                    }
                    break;
            }
        }

        private void ProcessEvents(Event e)
        {
            dragDelta = Vector2.zero;

            switch (e.type)
            {
                case EventType.MouseDown:
                    // Left click
                    if (e.button == 0)
                    {
                        if (panelRect.Contains(e.mousePosition))
                        {
                            clickInBox = true;
                        }
                        else if (InPanelDrag(e.mousePosition))
                        {
                            clickInBox = true;
                            m_inputState = eInputState.draggingPanel;
                        }
                        else if (e.mousePosition.y > TOOLBAR_HEIGHT)
                        {
                            clickInBox = false;
                            if (!DialogueEditorWindow.SelectableClickedOnThisUpdate)
                            {
                                UnselectObject();
                                e.Use();
                            }
                        }
                    }
                    // Right click
                    else if (e.button == 1)
                    {
                        if (DialogueEditorUtil.IsPointerNearConnection(uiNodes, e.mousePosition, out m_connectionDeleteParent, out m_connectionDeleteChild))
                        {
                            GenericMenu rightClickMenu = new GenericMenu();
                            rightClickMenu.AddItem(new GUIContent("Delete connection"), false, DeleteConnection);
                            rightClickMenu.ShowAsContext();
                        }
                    }

                    if (e.button == 0 || e.button == 2)
                        dragging = true;
                    else
                        dragging = false;
                    break;

                case EventType.MouseDrag:
                    if (dragging && (e.button == 0 || e.button == 2) && !clickInBox && !IsANodeSelected())
                    {
                        OnDrag(e.delta);
                    }
                    break;

                case EventType.MouseUp:
                    dragging = false;
                    break;
            }
        }

        private void ProcessNodeEvents(Event e, bool inPanel)
        {
            if (uiNodes != null)
            {
                for (int i = 0; i < uiNodes.Count; i++)
                {
                    bool guiChanged = uiNodes[i].ProcessEvents(e, inPanel);
                    if (guiChanged)
                        GUI.changed = true;
                }
            }
        }

        private void ProcessConnectionEvents(Event e, bool inPanel)
        {
            if (uiNodes != null && !inPanel && e.type == EventType.MouseDown && e.button == 0)
            {
                EditableConnection selectedConnection;
                bool success = DialogueEditorUtil.IsPointerNearConnection(uiNodes, e.mousePosition, out selectedConnection);

                if (success)
                {
                    SelectableClickedOnThisUpdate = true;
                    SelectConnection(selectedConnection, true);
                }
            }
        }

        private void OnDrag(Vector2 delta)
        {
            dragDelta = delta;

            if (uiNodes != null)
            {
                for (int i = 0; i < uiNodes.Count; i++)
                {
                    uiNodes[i].Drag(delta);
                }
            }

            GUI.changed = true;
        }




        //--------------------------------------
        // Event listeners
        //--------------------------------------

        /* -- Creating Nodes -- */

        public void CreateNewOption(UISpeechNode speechUI)
        {
            // Create new option, the argument speech is the options parent
            EditableOptionNode newOption = new EditableOptionNode();
            newOption.ID = CurrentAsset.CurrentIDCounter++;

            // Give the speech it's default values
            newOption.TMPFont = CurrentAsset.DefaultFont;

            // Add the option to the speechs' list of options
            speechUI.SpeechNode.AddOption(newOption);

            // Create a new UI object to represent the new option
            UIOptionNode ui = new UIOptionNode(newOption, Vector2.zero);
            uiNodes.Add(ui);

            // Set the input state appropriately
            m_inputState = eInputState.PlacingOption;
            m_currentPlacingNode = ui;
        }


        public void CreateNewSpeech(UINode node)
        {
            // Create new speech, the argument option is the speechs parent
            EditableSpeechNode newSpeech = new EditableSpeechNode();
            newSpeech.ID = CurrentAsset.CurrentIDCounter++;

            // Give the speech it's default values
            newSpeech.Name = CurrentAsset.DefaultName;
            newSpeech.Icon = CurrentAsset.DefaultSprite;
            newSpeech.TMPFont = CurrentAsset.DefaultFont;

            // Set this new speech as the options child
            if (node is UIOptionNode)
                (node as UIOptionNode).OptionNode.AddSpeech(newSpeech);
            else if (node is UISpeechNode)
                (node as UISpeechNode).SpeechNode.AddSpeech(newSpeech);

            // Create a new UI object to represent the new speech
            UISpeechNode ui = new UISpeechNode(newSpeech, Vector2.zero);
            uiNodes.Add(ui);

            // Set the input state appropriately
            m_inputState = eInputState.PlacingSpeech;
            m_currentPlacingNode = ui;
        }


        /* -- Connecting Nodes -- */

        public void ConnectNode(UINode option)
        {
            // The option if what we are connecting
            m_currentConnectingNode = option;

            // Set the input state appropriately
            m_inputState = eInputState.ConnectingNode;
        }


        /* -- Deleting Nodes -- */

        public void DeleteUINode(UINode node)
        {
            if (ConversationRoot == node.Info)
            {
                Log("Cannot delete root node.");
                return;
            }

            // Delete tree/internal objects
            node.Info.RemoveSelfFromTree();

            // Delete the EventHolder script if it's an speech node
            CurrentAsset.DeleteDataForNode(node.Info.ID);

            // Delete the UI classes
            uiNodes.Remove(node);
            node = null;

            // "Unselect" what we were looking at.
            CurrentlySelectedObject = null;
        }

        /* -- Deleting connection -- */

        public void DeleteConnection()
        {
            if (m_connectionDeleteParent != null && m_connectionDeleteChild != null)
            {
                // Remove child->parent relationship
                m_connectionDeleteChild.parents.Remove(m_connectionDeleteParent);

                // Remove parent->child relationship
                // Look through each connection the parent has
                // Remove the connection if it points to the child
                for (int i = 0; i < m_connectionDeleteParent.Connections.Count; i++)
                {
                    EditableConnection connection = m_connectionDeleteParent.Connections[i];

                    if (connection is EditableSpeechConnection && (connection as EditableSpeechConnection).Speech == m_connectionDeleteChild)
                    {
                        m_connectionDeleteParent.Connections.RemoveAt(i);
                        i--;
                    }
                    else if (connection is EditableOptionConnection && (connection as EditableOptionConnection).Option == m_connectionDeleteChild)
                    {
                        m_connectionDeleteParent.Connections.RemoveAt(i);
                        i--;
                    }
                }
            }

            m_connectionDeleteParent = null;
            m_connectionDeleteChild = null;
        }




        //--------------------------------------
        // Util
        //--------------------------------------

        private void SelectNode(UINode node, bool selected)
        {
            UnselectObject();

            if (selected)
            {
                CurrentlySelectedObject = new SelectableUINode(node);
                node.SetSelected(true);
            }
        }

        public void SelectConnection(EditableConnection connection, bool selected)
        {
            UnselectObject();

            if (selected)
            {
                CurrentlySelectedObject = new SelectableUIConnection(connection);
            }
        }

        private void UnselectObject()
        {
            if (CurrentlySelectedObject != null && CurrentlySelectedObject.Type == SelectableUI.eType.Node)
            {
                (CurrentlySelectedObject as SelectableUINode).Node.SetSelected(false);
            }

            CurrentlySelectedObject = null;
        }

        private bool IsANodeSelected()
        {
            if (uiNodes != null)
            {
                for (int i = 0; i < uiNodes.Count; i++)
                {
                    if (uiNodes[i].isSelected) return true;
                }
            }
            return false;
        }

        private bool InPanelDrag(Vector2 pos)
        {
            return (
                pos.x > panelResizerRect.x - panelResizerRect.width - PANEL_RESIZER_PADDING &&
                pos.x < panelResizerRect.x + panelResizerRect.width + PANEL_RESIZER_PADDING &&
                pos.y > panelResizerRect.y &&
                panelResizerRect.y < panelResizerRect.y + panelResizerRect.height);        
        }

        public bool NodeContainsSetParamAction(EditableConversationNode node, string parameterName)
        {
            for (int i = 0; i < node.ParamActions.Count; i++)
            {
                if (node.ParamActions[i].ParameterName == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ConnectionContainsParameter(EditableConnection connection, string parameterName)
        {
            for (int i = 0; i < connection.Conditions.Count; i++)
            {
                if (connection.Conditions[i].ParameterName == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetValidParamName(string baseName)
        {
            string newName = baseName;

            if (CurrentAsset.GetParameter(newName) != null)
            {
                int counter = 0;
                do
                {
                    newName = baseName + "_" + counter;
                    counter++;
                } while (CurrentAsset.GetParameter(newName) != null);
            }

            return newName;
        }

        private static void Log(string str)
        {
#if DIALOGUE_DEBUG
            Debug.Log("[DialogueEditor]: " + str);
#endif
        }

        private void PlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Log("Saving. Reason: Editor exiting edit mode.");
                Save();
            }
        }

        private void MarkSceneDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
#endif
        }




        //--------------------------------------
        // User / Save functionality
        //--------------------------------------

        private void Recenter()
        {
            if (ConversationRoot == null) { return; }

            // Calc delta to move head to (middle, 0) and then apply this to all nodes
            Vector2 target = new Vector2((position.width / 2) - (UISpeechNode.Width / 2) - (panelWidth / 2), TOOLBAR_HEIGHT + 5);
            Vector2 delta = target - new Vector2(ConversationRoot.EditorInfo.xPos, ConversationRoot.EditorInfo.yPos);
            for (int i = 0; i < uiNodes.Count; i++)
            {
                uiNodes[i].Drag(delta);
            }
            Repaint();
        }

        private void ResetPanelSize()
        {
            panelWidth = START_PANEL_WIDTH;
        }

        private void Save(bool manual = false)
        {
            if (Application.isPlaying)
            {
                Log("Save failed. Reason: Play mode.");
                return;
            }

            if (CurrentAsset != null)
            {
                EditableConversation conversation = new EditableConversation();

                // Prepare each node for serialization
                for (int i = 0; i < uiNodes.Count; i++)
                {
                    uiNodes[i].Info.SerializeAssetData(CurrentAsset);
                }

                // Now that each node has been prepared for serialization: 
                // - Register the UIDs of their parents/children
                // - Add it to the conversation
                for (int i = 0; i < uiNodes.Count; i++)
                {
                    uiNodes[i].Info.RegisterUIDs();

                    if (uiNodes[i] is UISpeechNode)
                    {
                        conversation.SpeechNodes.Add((uiNodes[i] as UISpeechNode).SpeechNode);
                    }
                    else if (uiNodes[i] is UIOptionNode)
                    {
                        conversation.Options.Add((uiNodes[i] as UIOptionNode).OptionNode);
                    }
                }

                // Serialize
                CurrentAsset.Serialize(conversation);

                // Null / clear everything. We aren't pointing to it anymore. 
                if (!manual)
                {
                    CurrentAsset = null;
                    while (uiNodes.Count != 0)
                        uiNodes.RemoveAt(0);
                    CurrentlySelectedObject = null;
                }

                MarkSceneDirty();
            }
        }
    }
}