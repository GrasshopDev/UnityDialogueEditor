using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DialogueEditor
{
    public abstract class UINode
    {
        // Events
        public delegate void UINodeSelectedEvent(UINode node, bool selected);
        public static UINodeSelectedEvent OnUINodeSelected;

        public delegate void UINodeDeletedEvent(UINode node);
        public static UINodeDeletedEvent OnUINodeDeleted;

        public delegate void CreateSpeechEvent(UINode node);
        public static CreateSpeechEvent OnCreateSpeech;

        public delegate void ConnectToNodeEvent(UINode node);
        public static ConnectToNodeEvent OnConnect;


        // Consts
        protected const int TEXT_BORDER = 5;
        protected const int TITLE_HEIGHT = 18;
        protected const int TITLE_GAP = 4;
        protected const int TEXT_BOX_HEIGHT = 40;
        public const int LINE_WIDTH = 3;

        // Members
        public Rect rect;
        public bool isDragged;
        public bool isSelected;
        protected string title;
        protected GUIStyle currentBoxStyle;

        // Static
        private static GUIStyle titleStyle;
        protected static GUIStyle textStyle;

        // Properties
        public EditableConversationNode Info { get; protected set; }
        public abstract Color DefaultColor { get; }
        public abstract Color SelectedColor { get; }


        //---------------------------------
        // Constructor 
        //---------------------------------

        public UINode(EditableConversationNode infoNode, Vector2 pos)
        {
            Info = infoNode;

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle();
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.normal.textColor = Color.white;
            }
            if (textStyle == null)
            {
                textStyle = new GUIStyle();
                textStyle.normal.textColor = Color.white;
                textStyle.wordWrap = true;
                textStyle.stretchHeight = false;
                textStyle.clipping = TextClipping.Clip;
            }
        }

        protected void CreateRect(Vector2 pos, float wid, float hei)
        {
            rect = new Rect(pos.x, pos.y, wid, hei);
            Info.EditorInfo.xPos = rect.x;
            Info.EditorInfo.yPos = rect.y;
        }


        // Generic methods, called from window
        public void SetPosition(Vector2 newPos)
        {
            Vector2 centeredPos = new Vector2(newPos.x - rect.width / 2, newPos.y - rect.height / 2);
            rect.position = centeredPos;
            Info.EditorInfo.xPos = centeredPos.x;
            Info.EditorInfo.yPos = centeredPos.y;
        }




        //---------------------------------
        // Drawing
        //---------------------------------

        public void Draw()
        {
            // Box
            GUI.Box(rect, title, currentBoxStyle);

            OnDraw();
        }

        protected void DrawTitle(string text)
        {
            Rect title = new Rect(rect.x, rect.y, rect.width, TITLE_HEIGHT);
            GUI.Label(title, text, titleStyle);
        }

        protected void DrawInternalText(string text, float leftOffset = 0, float heightOffset = 0)
        {
            Rect internalText = new Rect(rect.x + TEXT_BORDER + leftOffset, 
                rect.y + TITLE_HEIGHT + TITLE_GAP + heightOffset, 
                rect.width - TEXT_BORDER * 2 - leftOffset, 
                TEXT_BOX_HEIGHT);
            GUI.Box(internalText, text, textStyle);
        }

        public void DrawConnections(EditableConnection currentlySelected)
        {
            Vector2 start = Vector2.zero;
            Vector2 end = Vector2.zero;
            float xPos = 0;
            float yPos = 0;

            for (int i = 0; i < Info.Connections.Count; i++)
            {
                bool connectingToOption = false;

                if (Info.Connections[i].ConnectionType == EditableConnection.eConnectiontype.Speech)
                {
                    EditableSpeechConnection connection = Info.Connections[i] as EditableSpeechConnection;

                    DialogueEditorUtil.GetConnectionDrawInfo(rect, connection.Speech, out start, out end);
                    xPos = connection.Speech.EditorInfo.xPos;
                    yPos = connection.Speech.EditorInfo.yPos;
                }
                else if (Info.Connections[i].ConnectionType == EditableConnection.eConnectiontype.Option)
                {
                    EditableOptionConnection connection = Info.Connections[i] as EditableOptionConnection;

                    DialogueEditorUtil.GetConnectionDrawInfo(rect, connection.Option, out start, out end);
                    xPos = connection.Option.EditorInfo.xPos;
                    yPos = connection.Option.EditorInfo.yPos;

                    connectingToOption = true;
                }

                bool selected = (currentlySelected != null && currentlySelected == Info.Connections[i]);


                Vector2 toStart = (start - end).normalized;
                Vector2 toEnd = (end - start).normalized;
                if (selected)
                    Handles.DrawBezier(start, end, start + toStart, end + toEnd, SelectedColor, null, LINE_WIDTH * 3);
                Handles.DrawBezier(start, end, start + toStart, end + toEnd, DefaultColor, null, LINE_WIDTH);

                Vector2 intersection;
                Vector2 boxPos = new Vector2(xPos, yPos);
                if (DialogueEditorUtil.DoesLineIntersectWithBox(start, end, boxPos, connectingToOption, out intersection))
                {
                    if (selected)
                        DialogueEditorUtil.DrawArrowTip(intersection, toEnd, SelectedColor, LINE_WIDTH * 3);
                    DialogueEditorUtil.DrawArrowTip(intersection, toEnd, DefaultColor);
                }
            }
        }


        //---------------------------------
        // Interactions / Input Events
        //---------------------------------

        public void Drag(Vector2 moveDelta)
        {
            rect.position += moveDelta;
            Info.EditorInfo.xPos = rect.x;
            Info.EditorInfo.yPos = rect.y;
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                isDragged = true;
                isSelected = true;
            }
            else
            {
                isSelected = false;
            }

            OnSetSelected(selected);
        }

        public bool ProcessEvents(Event e, bool inPanel)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        if (rect.Contains(e.mousePosition) && !inPanel)
                        {
                            DialogueEditorWindow.SelectableClickedOnThisUpdate = true;
                            OnUINodeSelected?.Invoke(this, true);
                            e.Use();
                        }

                        GUI.changed = true;                     
                    }
                    else if (e.button == 1 && rect.Contains(e.mousePosition))
                    {
                        ProcessContextMenu();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    isDragged = false;
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && isDragged)
                    {
                        Drag(e.delta);
                        e.Use();
                    }
                    return true;
            }

            return false;
        }




        //---------------------------------
        // Inhereted, shared behaviour
        //---------------------------------

        protected void CreateSpeech()
        {
            OnCreateSpeech?.Invoke(this);
        }

        protected void ConnectToNode()
        {
            OnConnect?.Invoke(this);
        }

        protected void DeleteThisNode()
        {
            OnUINodeDeleted?.Invoke(this);
        }



        //---------------------------------
        // Abstract methods
        //---------------------------------

        public abstract void OnDraw();
        protected abstract void ProcessContextMenu();
        protected abstract void OnSetSelected(bool selected);
    }



    //--------------------------------------
    // Speech Node
    //--------------------------------------

    public class UISpeechNode : UINode
    {
        protected const float SPRITE_SZ = 40;
        protected const int NAME_HEIGHT = 12;

        // Events
        public delegate void CreateOptionEvent(UISpeechNode node);
        public static CreateOptionEvent OnCreateOption;

        // Static properties
        public static int Width { get { return 200; } }
        public static int Height { get { return 80; } }

        // Properties
        public EditableSpeechNode SpeechNode { get { return Info as EditableSpeechNode; } }
        public override Color DefaultColor { get { return DialogueEditorUtil.Colour(189, 0, 0); } }
        public override Color SelectedColor { get { return DialogueEditorUtil.Colour(255, 0, 0); } }

        // Static styles
        protected static GUIStyle defaultNodeStyle;
        protected static GUIStyle selectedNodeStyle;

        protected static GUIStyle npcNameStyle;


        //---------------------------------
        // Constructor
        //---------------------------------

        public UISpeechNode(EditableConversationNode infoNode, Vector2 pos) : base(infoNode, pos)
        {
            if (defaultNodeStyle == null || defaultNodeStyle.normal.background == null)
            {
                defaultNodeStyle = new GUIStyle();
                defaultNodeStyle.normal.background = DialogueEditorUtil.MakeTextureForNode(Width, Height, DefaultColor);
            }
            if (selectedNodeStyle == null || selectedNodeStyle.normal.background == null)
            {
                selectedNodeStyle = new GUIStyle();
                selectedNodeStyle.normal.background = DialogueEditorUtil.MakeTextureForNode(Width, Height, SelectedColor);
            }
            if (npcNameStyle == null)
            {
                npcNameStyle = new GUIStyle();
                npcNameStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1);
                npcNameStyle.wordWrap = true;
                npcNameStyle.stretchHeight = false;
                npcNameStyle.alignment = TextAnchor.MiddleCenter;
                npcNameStyle.clipping = TextClipping.Clip;
            }

            currentBoxStyle = defaultNodeStyle;

            CreateRect(pos, Width, Height);
        }



        //---------------------------------
        // Drawing
        //---------------------------------

        public override void OnDraw()
        {
            if (DialogueEditorWindow.ConversationRoot == SpeechNode)
                DrawTitle(isSelected ? "[Root]Speech node (selected)." : "[Root] Speech node.");
            else
                DrawTitle(isSelected ? "Speech node (selected)." : "Speech node.");

            // Name
            const int NAME_PADDING = 1;
            Rect name = new Rect(rect.x + TEXT_BORDER * 0.5f, rect.y + NAME_PADDING + TITLE_HEIGHT, rect.width - TEXT_BORDER * 0.5f, NAME_HEIGHT);
            GUI.Box(name, SpeechNode.Name, npcNameStyle);

            // Icon
            Rect icon = new Rect(rect.x + TEXT_BORDER * 0.5f, rect.y + TITLE_HEIGHT + TITLE_GAP + NAME_HEIGHT, SPRITE_SZ, SPRITE_SZ);
            if (SpeechNode.Icon != null)
                GUI.DrawTexture(icon, SpeechNode.Icon.texture, ScaleMode.ScaleToFit);

            // Text
            DrawInternalText(SpeechNode.Text, SPRITE_SZ + 5, NAME_HEIGHT + NAME_PADDING);
        }




        //---------------------------------
        // Interactions
        //---------------------------------

        protected override void OnSetSelected(bool selected)
        {
            if (selected)
                currentBoxStyle = selectedNodeStyle;
            else
                currentBoxStyle = defaultNodeStyle;
        }




        //---------------------------------
        // Right clicked
        //---------------------------------

        protected override void ProcessContextMenu()
        {
            GenericMenu rightClickMenu = new GenericMenu();
            rightClickMenu.AddItem(new GUIContent("Create Option"), false, CreateOption);
            rightClickMenu.AddItem(new GUIContent("Create Speech"), false, CreateSpeech);
            rightClickMenu.AddItem(new GUIContent("Connect"), false, ConnectToNode);
            rightClickMenu.AddItem(new GUIContent("Delete"), false, DeleteThisNode);
            rightClickMenu.ShowAsContext();
        }

        private void CreateOption()
        {
            OnCreateOption?.Invoke(this);
        }
    }




    //--------------------------------------
    // OptionNode
    //--------------------------------------

    public class UIOptionNode : UINode
    {
        // Static properties
        public static int Width { get { return 200; } }
        public static int Height { get { return 50; } }

        // Properties
        public EditableOptionNode OptionNode { get { return Info as EditableOptionNode; } }
        public override Color DefaultColor { get { return DialogueEditorUtil.Colour(0, 158, 118); } }
        public override Color SelectedColor { get { return DialogueEditorUtil.Colour(0, 201, 150); } }

        // Static styles
        protected static GUIStyle defaultNodeStyle;
        protected static GUIStyle selectedNodeStyle;


        //---------------------------------
        // Constructor 
        //---------------------------------

        public UIOptionNode(EditableConversationNode infoNode, Vector2 pos) : base(infoNode, pos)
        {
            if (defaultNodeStyle == null || defaultNodeStyle.normal.background == null)
            {
                defaultNodeStyle = new GUIStyle();
                defaultNodeStyle.normal.background = DialogueEditorUtil.MakeTextureForNode(Width, Height, DefaultColor);
            }
            if (selectedNodeStyle == null || selectedNodeStyle.normal.background == null)
            {
                selectedNodeStyle = new GUIStyle();
                selectedNodeStyle.normal.background = DialogueEditorUtil.MakeTextureForNode(Width, Height, SelectedColor);
            }

            currentBoxStyle = defaultNodeStyle;

            CreateRect(pos, Width, Height);
        }




        //---------------------------------
        // Drawing
        //---------------------------------

        public override void OnDraw()
        {
            DrawTitle( isSelected ? "Option node (selected)." : "Option node.");
            DrawInternalText(OptionNode.Text);
        }




        //---------------------------------
        // Interactions
        //---------------------------------

        protected override void OnSetSelected(bool selected)
        {
            if (selected)
                currentBoxStyle = selectedNodeStyle;
            else
                currentBoxStyle = defaultNodeStyle;
        }




        //---------------------------------
        // Right clicked
        //---------------------------------

        protected override void ProcessContextMenu()
        {
            GenericMenu rightClickMenu = new GenericMenu();
            rightClickMenu.AddItem(new GUIContent("Create Speech"), false, CreateSpeech);
            rightClickMenu.AddItem(new GUIContent("Connect"), false, ConnectToNode);
            rightClickMenu.AddItem(new GUIContent("Delete"), false, DeleteThisNode);
            rightClickMenu.ShowAsContext();
        }
    }
}