using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DialogueEditor
{
    [DataContract]
    public abstract class EditableCondition
    {
        public enum eConditionType
        {
            IntCondition,
            BoolCondition
        }

        public EditableCondition(string name)
        {
            ParameterName = name;
        }

        public abstract eConditionType ConditionType { get; }

        [DataMember] public string ParameterName;
    }

    [DataContract]
    public class EditableIntCondition : EditableCondition
    {
        public enum eCheckType
        {
            equal,
            lessThan,
            greaterThan
        }

        public EditableIntCondition(string name) : base(name) { }

        public override eConditionType ConditionType { get { return eConditionType.IntCondition; } }

        [DataMember] public eCheckType CheckType;
        [DataMember] public int RequiredValue;
    }

    [DataContract]
    public class EditableBoolCondition : EditableCondition
    {
        public enum eCheckType
        {
            equal,
            notEqual
        }

        public EditableBoolCondition(string name) : base(name) { }

        public override eConditionType ConditionType { get { return eConditionType.BoolCondition; } }

        [DataMember] public eCheckType CheckType;
        [DataMember] public bool RequiredValue;
    }
}