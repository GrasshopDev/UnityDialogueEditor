namespace DialogueEditor
{
    public abstract class Parameter
    {
        public Parameter(string name)
        {
            ParameterName = name;
        }

        public string ParameterName;
    }

    public class BoolParameter : Parameter
    {
        public BoolParameter(string name, bool defaultValue) : base(name)
        {
            BoolValue = defaultValue;
        }

        public bool BoolValue;
    }

    public class IntParameter : Parameter
    {
        public IntParameter(string name, int defalutValue) : base(name)
        {
            IntValue = defalutValue;
        }

        public int IntValue;
    }
}