using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DialogueEditor
{
    public static class DialogueEditorUtil
    {
        public static bool IsPointerNearConnection(List<UINode> uiNodes, Vector2 mousePos, 
            out EditableConversationNode par, out EditableConversationNode child)
        {
            par = null;
            child = null;
            Vector2 start, end;           
            float minDistance = float.MaxValue;
            const float MIN_DIST = 6;

            for (int i = 0; i < uiNodes.Count; i++)
            {
                List<EditableConnection> connections = uiNodes[i].Info.Connections;

                for (int j = 0; j < connections.Count; j++)
                {
                    if (connections[j] is EditableSpeechConnection)
                    {
                        EditableSpeechConnection speechCon = connections[j] as EditableSpeechConnection;
                        GetConnectionDrawInfo(uiNodes[i].rect, speechCon.Speech, out start, out end);

                        float distance = MinimumDistanceBetweenPointAndLine(start, end, mousePos);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            par = uiNodes[i].Info;
                            child = speechCon.Speech;
                        }
                    }
                    else if (connections[j] is EditableOptionConnection)
                    {
                        EditableOptionConnection optionCon = connections[j] as EditableOptionConnection;
                        GetConnectionDrawInfo(uiNodes[i].rect, optionCon.Option, out start, out end);

                        float distance = MinimumDistanceBetweenPointAndLine(start, end, mousePos);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            par = uiNodes[i].Info;
                            child = optionCon.Option;
                        }
                    }
                }
            }

            if (minDistance < MIN_DIST)
            {
                return true;
            }
            else
            {
                par = null;
                child = null;
                return false;
            }
        }

        public static bool IsPointerNearConnection(List<UINode> uiNodes, Vector2 mousePos, out EditableConnection connection)
        {
            EditableConversationNode parent = null;
            EditableConversationNode child = null;
     
            if (IsPointerNearConnection(uiNodes, mousePos, out parent, out child))
            {
                EditableConversationNode.eNodeType type = child.NodeType;
                for (int i = 0; i < parent.Connections.Count; i++)
                {
                    if (type == EditableConversationNode.eNodeType.Speech)
                    {
                        EditableSpeechConnection con = parent.Connections[i] as EditableSpeechConnection;
                        if (con.Speech == child)
                        {
                            connection = parent.Connections[i];
                            return true;
                        }
                    }
                    else if (type == EditableConversationNode.eNodeType.Option)
                    {
                        EditableOptionConnection con = parent.Connections[i] as EditableOptionConnection;
                        if (con.Option == child)
                        {
                            connection = parent.Connections[i];
                            return true;
                        }
                    }
                }
            }

            connection = null;
            return false;
        }

        // Translated into UnityC# from C++ 
        // Original Source: https://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
        private static float MinimumDistanceBetweenPointAndLine(Vector2 v, Vector2 w, Vector2 p)
        {
            float lsqu = (v - w).sqrMagnitude;
            if (lsqu < 0.01f)
                return (p - v).magnitude;

            float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(p - v, w - v) / lsqu));
            Vector2 projection = v + t * (w - v);
            return (p - projection).magnitude;
        }

        public static void GetConnectionDrawInfo(Rect originRect, 
            EditableConversationNode connectionTarget, out Vector2 start, out Vector2 end)
        {
            float offset = 12;

            Vector2 origin = new Vector2(originRect.x + originRect.width / 2, originRect.y + originRect.height / 2);
            Vector2 target;

            if (connectionTarget.NodeType == EditableConversationNode.eNodeType.Speech)
            {
                target = new Vector2(
                    connectionTarget.EditorInfo.xPos + UISpeechNode.Width / 2,
                    connectionTarget.EditorInfo.yPos + UISpeechNode.Height / 2);

                origin.x -= offset;
                target.x -= offset;
            }
            else if (connectionTarget.NodeType == EditableConversationNode.eNodeType.Option)
            {
                target = new Vector2(
                    connectionTarget.EditorInfo.xPos + UIOptionNode.Width / 2,
                    connectionTarget.EditorInfo.yPos + UIOptionNode.Height / 2);

                origin.x += offset;
                target.x += offset;
            }
            else
            {
                target = Vector2.zero;
            }

            start = origin;
            end = target;
        }

        // Translated from C++ into UnityC#
        // Original source: https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect
        public static bool DoLinesIntersect(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, out Vector2 point)
        {
            Vector2 s1 = new Vector2(p1.x - p0.x, p1.y - p0.y);
            Vector2 s2 = new Vector2(p3.x - p2.x, p3.y - p2.y);

            float s = (-s1.y * (p0.x - p2.x) + s1.x * (p0.y - p2.y)) / (-s2.x * s1.y + s1.x * s2.y);
            float t = (s2.x * (p0.y - p2.y) - s2.y * (p0.x - p2.x)) / (-s2.x * s1.y + s1.x * s2.y);

            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                point = new Vector2(p0.x + (t * s1.x), p0.y + (t * s1.y));
                return true;
            }

            point = Vector2.zero;
            return false;
        }


        public static bool DoesLineIntersectWithBox(Vector2 lineStart, Vector2 lineEnd, 
            Vector2 boxTL, bool isBoxOption, out Vector2 point)
        {
            int width = (isBoxOption) ? UIOptionNode.Width : UISpeechNode.Width;
            int height = (isBoxOption) ? UIOptionNode.Height : UISpeechNode.Height;            
            Vector2 s, e;

            // Check top
            s = new Vector2(boxTL.x , boxTL.y);
            e = new Vector2(boxTL.x + width, s.y);
            Vector2 topIntersect;
            bool t = (DoLinesIntersect(lineStart, lineEnd, s, e, out topIntersect));

            // Check right
            s = new Vector2(boxTL.x + width, boxTL.y);
            e = new Vector2(s.x, boxTL.y + height);
            Vector2 rightIntersect;
            bool r = (DoLinesIntersect(lineStart, lineEnd, s, e, out rightIntersect));

            // check bot
            s = new Vector2(boxTL.x, boxTL.y + height);
            e = new Vector2(boxTL.x + width, s.y);
            Vector2 botIntersect;
            bool b = (DoLinesIntersect(lineStart, lineEnd, s, e, out botIntersect));

            // Check left
            s = new Vector2(boxTL.x, boxTL.y);
            e = new Vector2(s.x, boxTL.y + height);
            Vector2 leftIntersect;
            bool l = (DoLinesIntersect(lineStart, lineEnd, s, e, out leftIntersect));

            // Test/compare all and find closest intersection point
            if (t || r || b || l)
            {
                Vector2 closest = new Vector2(float.MaxValue, float.MaxValue);

                if (t)
                {
                    float closeDist = (lineStart - closest).sqrMagnitude;
                    float topDist = (lineStart - topIntersect).sqrMagnitude;

                    if (topDist < closeDist)
                        closest = topIntersect;
                }

                if (r)
                {
                    float closeDist = (lineStart - closest).sqrMagnitude;
                    float rightDist = (lineStart - rightIntersect).sqrMagnitude;

                    if (rightDist < closeDist)
                        closest = rightIntersect;
                }

                if (b)
                {
                    float closeDist = (lineStart - closest).sqrMagnitude;
                    float botDist = (lineStart - botIntersect).sqrMagnitude;

                    if (botDist < closeDist)
                        closest = botIntersect;
                }

                if (l)
                {
                    float closeDist = (lineStart - closest).sqrMagnitude;
                    float leftDist = (lineStart - leftIntersect).sqrMagnitude;

                    if (leftDist < closeDist)
                        closest = leftIntersect;
                }

                point = closest;
                return true;
            }

            point = Vector2.zero;
            return false;
        }

        public static void DrawArrowTip(Vector2 pos, Vector2 dir, Color color, float width = UINode.LINE_WIDTH)
        {
            const float rotAmount = 25;
            const float len = 15f;
            Vector2 start, end, toStart, toEnd;
            start = pos;

            // Left arc
            Vector2 leftLine = Quaternion.Euler(0, 0, rotAmount) * -dir;
            end = start + leftLine.normalized * len;
            toStart = (start - end).normalized;
            toEnd = (end - start).normalized;
            Handles.DrawBezier(start, end, start + toStart, end + toEnd, color, null, width);

            // Right arc
            Vector2 rightLine = Quaternion.Euler(0, 0, -rotAmount) * -dir;
            end = start + rightLine.normalized * len;
            toStart = (start - end).normalized;
            toEnd = (end - start).normalized;
            Handles.DrawBezier(start, end, start + toStart, end + toEnd, color, null, width);
        }

        public static Color Colour(float r, float g, float b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1);
        }

        public static Color Colour(float r, float g, float b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        public static Texture2D MakeTextureForNode(int width, int height, Color col)
        {
            Texture2D t2d = new Texture2D(width, height);
            for (int x = 0; x < width - 1; x++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    if (y > height - 20)
                    {
                        t2d.SetPixel(x, y, col);
                    }
                    else
                    {
                        t2d.SetPixel(x, y, Color.black);
                    }                  
                }
            }
            t2d.Apply();
            return t2d;
        }

        public static Texture2D MakeTexture(int width, int height, Color col)
        {
            Texture2D t2d = new Texture2D(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    t2d.SetPixel(x, y, col);
                }
            }
            t2d.Apply();
            return t2d;
        }

        public static Color GetEditorColor()
        {
            return EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
        }

        public static Color ProSkinTextColour
        {
            get
            {
                return new Color(200, 200, 200);
            }
        }
    }
}