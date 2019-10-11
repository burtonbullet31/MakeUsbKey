using System.Collections.Generic;
using System.Linq;
using MakeUsbKey.ArgEngine.Enumerations;

namespace MakeUsbKey.ArgEngine
{
    public class ArgEngine
    {
        private Dictionary<Arg, string> _arguments;

        public string InvalidArgsMessage =>
            "Invalid command line options!!!!\n\nOptions are as follows:\n -Source=<Path to Source files>\n" +
            " -Destination=<Path to copy destination>\n" +
            " -Label=<Volume label to give USB key>\n\n" +
            $"Sample usage:\n {System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location)}" + 
            " -source=\"e:\\\" -dest=\"e:\\Zuri\" -label=\"REWORK_V3\n\n";

        public string GetArgValue(Arg argument) => _arguments.ContainsKey(argument) ? _arguments[argument] : "";

        public ArgEngine()
        {
            Init();
        }

        public ArgEngine(string[] args)
        {
            Init();
            ParseArgs(args);
        }

        private void Init()
        {
            _arguments = new Dictionary<Arg, string>();
            _arguments.Add(Arg.Source, null);
            _arguments.Add(Arg.Destination, null);
            _arguments.Add(Arg.VolumeLabel, null);
        }

        public bool SetArg(Arg arg, string value)
        {
            if (_arguments.ContainsKey(arg))
                _arguments[arg] = value;
            return _arguments.Contains(new KeyValuePair<Arg, string>(arg, value));
        }

        public void ParseArgs(string[] args)
        {

            foreach (var argument in args)
            {
                var arg = argument.Split('=');
                var argList = arg.ToList();
                if (argList.Count != 2) continue;

                switch (arg[0].ToLower())
                {
                    case "-source":
                        _arguments[Arg.Source] = arg[1];
                        break;
                    case "-destination":
                        _arguments[Arg.Destination] = arg[1];
                        break;
                    case "-label":
                        _arguments[Arg.VolumeLabel] = arg[1];
                        break;
                }
            
            }
        } 
    }
}
