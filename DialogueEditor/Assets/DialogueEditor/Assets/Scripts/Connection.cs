using System.Collections;
using System.Collections.Generic;

namespace DialogueEditor
{
    public abstract class Connection
    {
        public enum eConnectionType
        {
            None,
            Speech,
            Option
        }

        public Connection()
        {
            Conditions = new List<Condition>();
        }

        public abstract eConnectionType ConnectionType { get; }

        public List<Condition> Conditions;
    }

    public class SpeechConnection : Connection
    {
        public SpeechConnection(SpeechNode node)
        {
            SpeechNode = node;
        }

        public override eConnectionType ConnectionType { get { return eConnectionType.Speech; } }

        public SpeechNode SpeechNode;
    }

    public class OptionConnection : Connection
    {
        public OptionConnection(OptionNode node)
        {
            OptionNode = node;
        }

        public override eConnectionType ConnectionType { get { return eConnectionType.Option; } }

        public OptionNode OptionNode;
    }
}