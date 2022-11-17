using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//--------------------------------------
// Node C# class - User Facing
//--------------------------------------

namespace DialogueEditor
{
    public abstract class ConversationNode
    {
        public enum eNodeType
        {
            Speech,
            Option
        }

        public ConversationNode()
        {
            Connections = new List<Connection>();
            ParamActions = new List<SetParamAction>();
        }

        public abstract eNodeType NodeType { get; }
        public Connection.eConnectionType ConnectionType
        {
            get
            {
                if (Connections.Count > 0)
                    return Connections[0].ConnectionType;
                return Connection.eConnectionType.None;
            }
        }

        /// <summary> The body text of the node. </summary>
        public string Text;

        /// <summary> The child connections this node has. </summary>
        public List<Connection> Connections;

        /// <summary> This nodes parameter actions. </summary>
        public List<SetParamAction> ParamActions;

        /// <summary> The Text Mesh Pro FontAsset for the text of this node. </summary>
        public TMPro.TMP_FontAsset TMPFont;
    }


    public class SpeechNode : ConversationNode
    {
        public override eNodeType NodeType { get { return eNodeType.Speech; } }

        /// <summary> The name of the NPC who is speaking. </summary>
        public string Name;

        /// <summary> Should this speech node go onto the next one automatically? </summary>
        public bool AutomaticallyAdvance;

        /// <summary> Should this speech node, althought auto-advance, also display a "continue" or "end" option, for users to click through quicker? </summary>
        public bool AutoAdvanceShouldDisplayOption;

        /// <summary> If AutomaticallyAdvance==True, how long should this speech node 
        /// display before going onto the next one? </summary>
        public float TimeUntilAdvance;

        /// <summary> The Icon of the speaking NPC </summary>
        public Sprite Icon;

        public AudioClip Audio;
        public float Volume;

        /// <summary> UnityEvent, to betriggered when this Node starts. </summary>
        public UnityEngine.Events.UnityEvent Event;
    }


    public class OptionNode : ConversationNode
    {
        public override eNodeType NodeType { get { return eNodeType.Option; } }

        /// <summary> UnityEvent, to betriggered when this Option is chosen. </summary>
        public UnityEngine.Events.UnityEvent Event;
    }
}
