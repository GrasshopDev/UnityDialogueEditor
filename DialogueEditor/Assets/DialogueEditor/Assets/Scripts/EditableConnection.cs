using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DialogueEditor
{
    [DataContract]
    [KnownType(typeof(EditableIntCondition))]
    [KnownType(typeof(EditableBoolCondition))]
    public abstract class EditableConnection
    {
        public enum eConnectiontype
        {
            Speech,
            Option
        }

        public abstract eConnectiontype ConnectionType { get; }

        public EditableConnection()
        {
            Conditions = new List<EditableCondition>();
        }

        public void AddCondition(EditableCondition condition)
        {
            Conditions.Add(condition);
        }

        [DataMember] public List<EditableCondition> Conditions;
        [DataMember] public int NodeUID;
    }

    [DataContract]
    public class EditableSpeechConnection : EditableConnection
    {
        public override eConnectiontype ConnectionType { get { return eConnectiontype.Speech; } }

        public EditableSpeechNode Speech;

        public EditableSpeechConnection(EditableSpeechNode node) : base()
        {
            Speech = node;
            NodeUID = node.ID;
        }
    }

    [DataContract]
    public class EditableOptionConnection : EditableConnection
    {
        public override eConnectiontype ConnectionType { get { return eConnectiontype.Option; } }

        public EditableOptionNode Option;

        public EditableOptionConnection(EditableOptionNode node) : base()
        {
            Option = node;
            NodeUID = node.ID;
        }
    }
}