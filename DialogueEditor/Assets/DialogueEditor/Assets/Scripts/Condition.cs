namespace DialogueEditor
{
    public abstract class Condition
    {
        public enum eConditionType
        {
            IntCondition,
            BoolCondition
        }

        public abstract eConditionType ConditionType { get; }

        public string ParameterName;
    }

    public class IntCondition : Condition
    {
        public enum eCheckType
        {
            equal,
            lessThan,
            greaterThan
        }

        public override eConditionType ConditionType { get { return eConditionType.IntCondition; } }

        public eCheckType CheckType;
        public int RequiredValue;
    }

    public class BoolCondition : Condition
    {
        public enum eCheckType
        {
            equal,
            notEqual
        }

        public override eConditionType ConditionType { get { return eConditionType.BoolCondition; } }

        public eCheckType CheckType;
        public bool RequiredValue;
    }
}
