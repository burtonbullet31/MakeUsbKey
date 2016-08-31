using System.Collections.Generic;
using System.Linq;

namespace Make_USB_Key.ArgEngine
{
    public enum ARGS { SOURCE, DESTINATION, VOLUME_LABEL }
    public class ArgEngine
    {
        Dictionary<ARGS, string> Arguments = null;
        string[] ArgInput = null;

        public bool ArgsGood { get { return (Arguments.Count != 0 && ArgInput != null); } }

        public bool RequiredArgsSet
        {
            get
            {
                if (!Arguments.ContainsKey(ARGS.SOURCE) ||
                    !Arguments.ContainsKey(ARGS.DESTINATION) ||
                    !Arguments.ContainsKey(ARGS.VOLUME_LABEL))
                    return false;

                return
                    (Arguments[ARGS.SOURCE] != null &&
                     Arguments[ARGS.DESTINATION] != null &&
                     Arguments[ARGS.VOLUME_LABEL] != null);
            }
        }

        public string InvalidArgsMessage
        {
            get
            {
                return
                       "Invalid command line options!!!!\n\nOptions are as follows:\n -Source=<Path to Source files>\n" +
                   " -Destination=<Path to copy destination>\n" +
                   " -Label=<Volume label to give USB key>\n\n" +
                   "Sample usage:\n " + System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location) + 
                   " -source=\"e:\\\" -dest=\"e:\\Zuri\" -label=\"REWORK_V3\n\n";
            }
        }

        public string GetArgValue(ARGS Argument)
        {
            if (Arguments.ContainsKey(Argument))
                return Arguments[Argument];
            return "";
        }

        public ArgEngine()
        {
            Init();
        }

        public ArgEngine(string[] Args)
        {
            Init();
            ParseArgs(Args);
        }

        private void Init()
        {
            Arguments = new Dictionary<ARGS, string>();
            ArgInput = null;
            Arguments.Add(ARGS.SOURCE, null);
            Arguments.Add(ARGS.DESTINATION, null);
            Arguments.Add(ARGS.VOLUME_LABEL, null);
        }

        public bool SetArg(ARGS Arg, string Value)
        {
            if (Arguments.ContainsKey(Arg))
                Arguments[Arg] = Value;
            return Arguments.Contains(new KeyValuePair<ARGS, string>(Arg, Value));
        }

        public void ParseArgs(string[] Args)
        {

            foreach (string argument in Args)
            {
                string[] arg = argument.Split('=');
                List<string> Argrepo = new List<string>();
                foreach (string a in arg)
                    Argrepo.Add(a);
                if (Argrepo.Count != 2) continue;

                switch (arg[0].ToLower())
                {
                    case "-source":
                        Arguments[ARGS.SOURCE] = arg[1];
                        break;
                    case "-destination":
                        Arguments[ARGS.DESTINATION] = arg[1];
                        break;
                    case "-label":
                        Arguments[ARGS.VOLUME_LABEL] = arg[1];
                        break;
                    default:
                        break;
                }
            
            }
        } 
    }
}
