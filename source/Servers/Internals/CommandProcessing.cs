using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace EQEmulator.Servers.Internals
{
    internal sealed class CommandEntry
    {
        internal int ReqGmStatus { get; set; }
        internal string Usage { get; set; }
        internal string Description { get; set; }
        internal Action<Dictionary<string, string>> Command { get; set; }
    }

    public sealed class CommandSyntaxException : Exception
    {
        public CommandSyntaxException(string cmdName)
            : base("Command " + cmdName + " has an invalid syntax specified.")
        { }
    }

    public sealed class Argument
    {
        public string Original { get; private set; }
        public string Switch { get; private set; }
        public ReadOnlyCollection<string> SubArguments { get; private set; }
        private List<string> subArguments;
        public Argument(string original)
        {
            Original = original;
            Switch = string.Empty;
            subArguments = new List<string>();
            SubArguments = new ReadOnlyCollection<string>(subArguments);
            Parse();
        }

        private void Parse()
        {
            if (string.IsNullOrEmpty(Original)) {
                return;
            }
            char[] switchChars = { '/', '-' };
            if (!switchChars.Contains(Original[0])) {
                return;
            }
            string switchString = Original.Substring(1);
            string subArgsString = string.Empty;
            int colon = switchString.IndexOf(':');
            if (colon >= 0) {
                subArgsString = switchString.Substring(colon + 1);
                switchString = switchString.Substring(0, colon);
            }
            Switch = switchString;
            if (!string.IsNullOrEmpty(subArgsString))
                subArguments.AddRange(subArgsString.Split(';'));
        }

        public bool IsSimple
        { get { return SubArguments.Count == 0; } }
        public bool IsSimpleSwitch
        { get { return !string.IsNullOrEmpty(Switch) && SubArguments.Count == 0; } }
        public bool IsCompoundSwitch
        { get { return !string.IsNullOrEmpty(Switch) && SubArguments.Count == 1; } }
        public bool IsComplexSwitch
        { get { return !string.IsNullOrEmpty(Switch) && SubArguments.Count > 0; } }
    }

    public sealed class ArgumentDefinition
    {
        public string ArgumentSwitch { get; private set; }
        public string Syntax { get; private set; }
        public string Description { get; private set; }
        public Func<Argument, bool> Verifier { get; private set; }

        public ArgumentDefinition(string argumentSwitch,
                                  string syntax,
                                  string description,
                                  Func<Argument, bool> verifier)
        {
            ArgumentSwitch = argumentSwitch.ToUpper();
            Syntax = syntax;
            Description = description;
            Verifier = verifier;
        }

        public bool Verify(Argument arg)
        {
            return Verifier(arg);
        }
    }

    public sealed class ArgumentSemanticAnalyzer
    {
        private List<ArgumentDefinition> argumentDefinitions = new List<ArgumentDefinition>();
        private Dictionary<string, Action<Argument>> argumentActions = new Dictionary<string, Action<Argument>>();

        public ReadOnlyCollection<Argument> UnrecognizedArguments { get; private set; }
        public ReadOnlyCollection<Argument> MalformedArguments { get; private set; }
        public ReadOnlyCollection<Argument> RepeatedArguments { get; private set; }

        public ReadOnlyCollection<ArgumentDefinition> ArgumentDefinitions
        {
            get { return new ReadOnlyCollection<ArgumentDefinition>(argumentDefinitions); }
        }

        public IEnumerable<string> DefinedSwitches
        {
            get
            {
                return from argumentDefinition in argumentDefinitions
                       select argumentDefinition.ArgumentSwitch;
            }
        }

        public void AddArgumentVerifier(ArgumentDefinition verifier)
        {
            argumentDefinitions.Add(verifier);
        }

        public void RemoveArgumentVerifier(ArgumentDefinition verifier)
        {
            var verifiersToRemove = from v in argumentDefinitions
                                    where v.ArgumentSwitch == verifier.ArgumentSwitch
                                    select v;
            foreach (var v in verifiersToRemove)
                argumentDefinitions.Remove(v);
        }

        public void AddArgumentAction(string argumentSwitch, Action<Argument> action)
        {
            argumentActions.Add(argumentSwitch, action);
        }

        public void RemoveArgumentAction(string argumentSwitch)
        {
            if (argumentActions.Keys.Contains(argumentSwitch))
                argumentActions.Remove(argumentSwitch);
        }

        public bool VerifyArguments(IEnumerable<Argument> arguments)
        {
            // no parameter to verify with, fail.
            if (!argumentDefinitions.Any())
                return false;

            // Identify if any of the arguments are not defined
            this.UnrecognizedArguments = (from argument in arguments
                                          where !DefinedSwitches.Contains(argument.Switch.ToUpper())
                                          select argument).ToList().AsReadOnly();

            //Check for all the arguments where the switch matches a known switch, 
            //but our well-formedness predicate is false. 
            this.MalformedArguments = (from argument in arguments
                                       join argumentDefinition in argumentDefinitions
                                       on argument.Switch.ToUpper() equals
                                           argumentDefinition.ArgumentSwitch
                                       where !argumentDefinition.Verify(argument)
                                       select argument).ToList().AsReadOnly();

            //Sort the arguments into “groups” by their switch, count every group, 
            //and select any groups that contain more than one element, 
            //We then get a read only list of the items.
            this.RepeatedArguments =
                    (from argumentGroup in
                         from argument in arguments
                         where !argument.IsSimple
                         group argument by argument.Switch.ToUpper()
                     where argumentGroup.Count() > 1
                     select argumentGroup).SelectMany(ag => ag).ToList().AsReadOnly();

            if (this.UnrecognizedArguments.Any() ||
                this.MalformedArguments.Any() ||
                this.RepeatedArguments.Any())
                return false;

            return true;
        }

        public void EvaluateArguments(IEnumerable<Argument> arguments)
        {
            //Now we just apply each action:
            foreach (Argument argument in arguments)
                argumentActions[argument.Switch.ToUpper()](argument);
        }

        public string InvalidArgumentsDisplay()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Invalid arguments: ");
            // Add the unrecognized arguments
            FormatInvalidArguments(builder, this.UnrecognizedArguments, "Unrecognized argument: {0}{1}");

            // Add the malformed arguments
            FormatInvalidArguments(builder, this.MalformedArguments, "Malformed argument: {0}{1}");

            // For the repeated arguments, we want to group them for the display
            // so group by switch and then add it to the string being built.
            var argumentGroups = from argument in this.RepeatedArguments
                                 group argument by argument.Switch.ToUpper() into ag
                                 select new { Switch = ag.Key, Instances = ag };

            foreach (var argumentGroup in argumentGroups) {
                builder.AppendFormat("Repeated argument: {0} ", argumentGroup.Switch);
                FormatInvalidArguments(builder, argumentGroup.Instances.ToList(), " {0}{1}");
            }
            return builder.ToString();
        }

        private void FormatInvalidArguments(StringBuilder builder, IEnumerable<Argument> invalidArguments, string errorFormat)
        {
            if (invalidArguments != null) {
                foreach (Argument argument in invalidArguments) {
                    builder.AppendFormat(errorFormat, argument.Original, " ");
                }
            }
        }
    }
}
