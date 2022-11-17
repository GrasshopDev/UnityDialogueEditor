using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DialogueEditor
{
    [DataContract]
    public abstract class EditableSetParamAction
    {
        public enum eParamActionType
        {
            Int,
            Bool
        }

        public EditableSetParamAction(string paramName)
        {
            this.ParameterName = paramName;
        }

        public abstract eParamActionType ParamActionType { get; }

        [DataMember] public string ParameterName;
    }

    [DataContract]
    public class EditableSetIntParamAction : EditableSetParamAction
    {
        public EditableSetIntParamAction(string paramName) : base(paramName) { }

        public override eParamActionType ParamActionType { get { return eParamActionType.Int; } }

        [DataMember] public int Value;
    }

    [DataContract]
    public class EditableSetBoolParamAction : EditableSetParamAction
    {
        public EditableSetBoolParamAction(string paramName) : base(paramName) { }

        public override eParamActionType ParamActionType { get { return eParamActionType.Bool; } }

        [DataMember] public bool Value;
    }
}