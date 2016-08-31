using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Make_USB_Key
{
    class VolumeInfo
    {
        public string VolumeName { get; set; }
        public string Letter { get; set; }
        public string Label { get; set; }
        public string FS { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string Status { get; set; }

        public VolumeInfo()
        {
            VolumeName = "";
            Letter = "";
            Label = "";
            FS = "";
            Type = "";
            Size = "";
            Status = "";
        }
    }


    /// <summary>
    /// Interaction logic for FormatFlashDriveOutput.xaml
    /// </summary>
    public partial class FormatFlashDriveOutput : Window
    {
        /// <summary>
        /// Function deleegate used to update the UI Console output
        /// </summary>
        /// <param name="Message"></param>
        public delegate void UpdateUIThread(string Message);
        public delegate void UpdateUIThreadFail(string Message, bool fail);
        public delegate void UpdateUIDriveLetter(string Letter);
        public delegate void UpdateUIFileProgress(int Progress);
        public delegate void UpdateUIDialogResult();
        public delegate void Closer();

        public string DriveLetter { get; set; }
        public bool GetDriveInfo;
        public string NewVolumeName { get; set; }
        public string SourceFolder { get; set; }
        public string OrigSource { get; set; }
        WebClient FileWebClient = null;
        bool WaitForCopy = false;
        private Thread WorkThread = null;
        string Mess = "";
        object MessageBaton = new object();
        private bool ThreadAlive { get { if (WorkThread == null) return false; else return WorkThread.IsAlive; } }
        
        //ConsoleContent ConsoleOut = new ConsoleContent();

        public FormatFlashDriveOutput(string DriveLetter, string Source, string origsource, string Volume = "")
        {
            InitializeComponent();
            SourceFolder = Source;
            NewVolumeName = Volume;
            OrigSource = origsource;
            //DataContext = ConsoleOut;
            this.DriveLetter = DriveLetter;
            DriveLetterTextBox.Text = this.DriveLetter;
            FileWebClient = new WebClient();

            FileProgress.Maximum = 100;
            FileProgress.Minimum = 0;
            FileProgress.Value = 0;
            FileWebClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
            FileWebClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);
            
            Loaded += Start;
        }

        public void Start(object j = null, EventArgs e = null)
        {
            Show();
            AddMessage("Initializzing...");
            AddMessage("");

            if (!System.IO.Directory.Exists(SourceFolder))
            {
                OutputWindow.Items.Add(OutBox(""));

                TextBlock Block = new TextBlock();
                Block.Text = "Directory \"" + SourceFolder + "\" does not exist!";
                Block.Foreground = Brushes.Red;

                OutputWindow.Items.Add(Block);
                return;
            }

            WorkThread = new Thread(new ThreadStart(RunProcess));
            WorkThread.SetApartmentState(ApartmentState.STA);
            WorkThread.Start();
            
            OutputWindow.Items.Refresh();
        }

        private Process ProcessOp(string Program)
        {
            Process NewProc = new Process();
            NewProc.StartInfo.FileName = Program;
            NewProc.StartInfo.ErrorDialog = true;
            NewProc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            NewProc.StartInfo.LoadUserProfile = true;
            NewProc.StartInfo.UseShellExecute = false;
            NewProc.StartInfo.CreateNoWindow = true;
            NewProc.StartInfo.RedirectStandardOutput = true;
            NewProc.StartInfo.RedirectStandardInput = true;

            return NewProc;
        }

        private void RunProcess()
        {
            char[] Separators = { '\n' };
            VolumeInfo Volume = null;
            ArrayList OutArray = new ArrayList();
            string Output;
            

            UpdateUIMessages("Building volume information file.");
            
            Process p = ProcessOp("diskpart.exe");
            p.StartInfo.ErrorDialog = true;
            p.Start();
            p.StandardInput.WriteLine("list volume");
            p.StandardInput.WriteLine("exit");
            Output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            p.Close();

            UpdateUIMessages(Output);

            foreach (string Line in Output.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                OutArray.Add(Line);

            Volume = GetVolume(OutArray);

            UpdateUIMessages("");
            UpdateUIMessages("Drive to be formatted is: " + Volume.VolumeName);
            UpdateUIMessages("Formatting...");

            p = ProcessOp("diskpart.exe");
            p.StartInfo.ErrorDialog = true;
            p.Start();
            p.StandardInput.WriteLine("select volume " + Volume.Letter);
            p.StandardInput.WriteLine("remove letter=" + Volume.Letter);
            p.StandardInput.WriteLine("clean");
            p.StandardInput.WriteLine("create partition primary");
            p.StandardInput.WriteLine("select partition 1");
            p.StandardInput.WriteLine("active");
            p.StandardInput.WriteLine("format fs=fat32 label=" + NewVolumeName + " quick");
            p.StandardInput.WriteLine("assign letter=" + Volume.Letter);
            p.StandardInput.WriteLine("exit");
            p.WaitForExit();

            UpdateUIMessages(p.StandardOutput.ReadToEnd());
            UpdateUIMessages("");
            UpdateUIMessages("Completed formatting drive. Prepping for file transfer.");
            
            p.Close();

            UpdateDriveLetter(Volume.Letter + ":\\");

            UpdateUIMessages("");
            UpdateUIMessages("Beginning file transfer for files inside: " + SourceFolder);

            foreach (string directory in System.IO.Directory.EnumerateDirectories(OrigSource, "*", SearchOption.AllDirectories))
            {
                string path = directory.Substring(directory.Substring((directory.IndexOf(OrigSource))).IndexOf("\\"));
                UpdateUIMessages("Creating directory: " + path);
                Directory.CreateDirectory(Volume.Letter + ":\\" + path);
            }

            UpdateUIMessages("");
            UpdateUIMessages("Completed creating folder structure. Copying files.");
            UpdateUIMessages("");

            foreach (string file in System.IO.Directory.EnumerateFiles(SourceFolder,"*.*",SearchOption.AllDirectories))
            {
                WaitForCopy = true;

                string path = Path.GetDirectoryName(file).Substring((file.IndexOf(OrigSource)+OrigSource.Length));

                FileWebClient.DownloadFileAsync(new Uri(file), Volume.Letter + ":" + path + "\\" + Path.GetFileName(file));
                UpdateUIMessages("Copying file: " + file);

                if (!System.IO.File.Exists(file))
                    UpdateUIMessages("FAILED TO FIND: " + Volume.Letter + ":" + path + "\\" + Path.GetFileName(file), true);

                while (WaitForCopy)
                    Thread.Sleep(25);
                SetFileProgres();
                if (!System.IO.File.Exists(Volume.Letter + ":" + path + "\\" + Path.GetFileName(file)))
                    UpdateUIMessages("FAILED: " + Volume.Letter + ":" + path + "\\" + Path.GetFileName(file), true);
                else
                    UpdateUIMessages("SUCCESS: " + Volume.Letter + ":" + path + "\\" + Path.GetFileName(file));

                UpdateUIMessages("");
            }

            UpdateUIMessages("");
            UpdateUIMessages("");
            UpdateUIMessages("Completed creating USB key! USB Key is ready for use.");
            UpdateUIMessages("");

            SetUIDialogResult();
        }
        
        private TextBlock OutBox(string Input)
        {
            TextBlock Block = new TextBlock();
            Block.Text = Input;
            Block.Foreground = Brushes.Green;

            return Block;
        }
        
        private VolumeInfo GetVolume(ArrayList Input, bool PostFormat = false)
        {
            VolumeInfo Info = new VolumeInfo();
            char[] SepValue = { ' ' };
            Dictionary<string, VolumeInfo> Volumes = new Dictionary<string, VolumeInfo>();

            foreach (string InputString in Input)
            {
                string[] LineIn = InputString.Split(SepValue, StringSplitOptions.RemoveEmptyEntries);

                if (LineIn[0].Trim() == "Volume" && !LineIn[1].Trim().Contains("#"))
                {
                    VolumeInfo Volume = new VolumeInfo();
                    Volume.VolumeName = LineIn[0].Trim() + " " + LineIn[1].Trim();
                    Volume.Letter = LineIn[2].Trim().Split(':')[0];
                    Volume.Label = LineIn[3].Trim();
                    Volume.FS = LineIn[4].Trim();
                    Volume.Type = LineIn[5].Trim();
                    Volume.Size = LineIn[6].Trim() + " " + LineIn[7];
                    Volume.Status = LineIn[8];

                    Volumes.Add(Volume.VolumeName, Volume);
                }
            }

            foreach (VolumeInfo Volume in Volumes.Values)
            {
                if ((!PostFormat && (DriveLetter.Trim().Split(':')[0] == Volume.Letter)) || 
                    (PostFormat && (Volume.Label.ToLower() == "rework_v3")))
                {
                    UpdateUIMessages("\nMatched!\n");
                    UpdateUIMessages("Volume: " + Volume.VolumeName);
                    UpdateUIMessages("Letter: " + Volume.Letter);
                    UpdateUIMessages("Label: " + Volume.Label);
                    UpdateUIMessages("File System: " + Volume.FS);
                    UpdateUIMessages("Type: " + Volume.Type);
                    UpdateUIMessages("Size: " + Volume.Size);
                    UpdateUIMessages("Status: " + Volume.Status);
                    Info = Volume;
                    break;
                }
            }

            return Info;
        }
        
        private void UpdateUIMessagesByEvent(object Sender, EventArgs e)
        {
            UpdateUIThread UIMessenger = new UpdateUIThread(AddMessage);

            lock (MessageBaton)
                Dispatcher.Invoke(UIMessenger, Mess);
        }

        private void UpdateUIMessages(string Input)
        {
            UpdateUIThread UIMessenger = new UpdateUIThread(AddMessage);

            lock (MessageBaton)
                Dispatcher.Invoke(UIMessenger, Input);
        }

        private void UpdateUIMessages(string Input, bool fail)
        {
            UpdateUIThreadFail UIMessenger = new UpdateUIThreadFail(AddMessage);

            lock (MessageBaton)
                Dispatcher.Invoke(UIMessenger, Input, fail);
        }

        private void UpdateDriveLetter(string letter)
        {
            UpdateUIDriveLetter UILetter = new UpdateUIDriveLetter(UpdateLetter);
            lock (MessageBaton)
                Dispatcher.Invoke(UILetter, letter);
        }

        private void UICloser()
        {
            Closer UIClose = new Closer(CloseWindow);

            lock (MessageBaton)
                Dispatcher.Invoke(UIClose);
        }

        public void CloseWindow()
        {
            Thread.Sleep(1000);
            Close();
        }

        private void AddMessage(string Message)
        {
            TextBlock output = new TextBlock();
            output.Text = Message;
            output.Foreground = Brushes.Green;
            output.Background = Brushes.Black;
            
            OutputWindow.Items.Add(output);
            OutputWindow.Items.Refresh();
            OutputWindow.ScrollIntoView(output);
        }

        private void AddMessage(string Message, bool fail)
        {
            TextBlock output = new TextBlock();
            output.Text = Message;
            if (!fail)
                output.Foreground = Brushes.Green;
            else
                output.Foreground = Brushes.Red;
            output.Background = Brushes.Black;

            OutputWindow.Items.Add(output);
            OutputWindow.Items.Refresh();
            OutputWindow.ScrollIntoView(output);
        }

        private void UpdateLetter(string Letter)
        {
            label.Content = "Destination: ";
            DriveLetterTextBox.Text = Letter;
        }

        private bool BuildFormatScript(string volInfo)
        {
            FileStream FileIn = null;
            StreamReader Reader = null;
            FileStream FileOut = null;
            StreamWriter Writer = null;
            char[] SepValue = { ' ' };
            Dictionary<string, VolumeInfo> Volumes = new Dictionary<string, VolumeInfo>();

            try
            {
                FileIn = new FileStream(volInfo, FileMode.Open);
                Reader = new StreamReader(FileIn);
                FileOut = new FileStream("format.dps", FileMode.Create);
                Writer = new StreamWriter(FileOut);

                while(!Reader.EndOfStream)
                {
                    string []LineIn = Reader.ReadLine().Split(SepValue, StringSplitOptions.RemoveEmptyEntries);

                    if (LineIn[0].Trim() == "Volume" && !LineIn[1].Trim().Contains("#"))
                    {
                        VolumeInfo Volume = new VolumeInfo();
                        Volume.VolumeName = LineIn[0].Trim() + " " + LineIn[1].Trim();
                        Volume.Letter    = LineIn[2].Trim().Split(':')[0];
                        Volume.Label     = LineIn[3].Trim();
                        Volume.FS        = LineIn[4].Trim();
                        Volume.Type      = LineIn[5].Trim();
                        Volume.Size      = LineIn[6].Trim() + " " + LineIn[7];
                        Volume.Status    = LineIn[8];

                        Volumes.Add(Volume.VolumeName, Volume);
                    }
                }

                foreach(VolumeInfo Volume in Volumes.Values)
                {
                    if (DriveLetter == Volume.Letter)
                    {
                        Writer.WriteLine("select " + Volume.VolumeName);
                        Writer.WriteLine("format fs=fat32 quick label=\"REWORK_V3\"");
                        break;
                    }
                }

                Reader.Close();
                Writer.Close();
                FileOut.Close();
                FileIn.Close();

            }
            catch { return false; }

            return System.IO.File.Exists("format.dps");
        }
        
        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            SetFileProgres(e.ProgressPercentage);
        }

        private void CompleteProc(object sender, EventHandler e)
        {
            SetUIDialogResult();
        }

        internal void SetFileProgres(int Progress = 0)
        {
            UpdateUIFileProgress Prog = new UpdateUIFileProgress(FileProg);
            Dispatcher.Invoke(Prog, Progress);
        }
        internal void SetUIDialogResult()
        {
            UpdateUIDialogResult Prog = new UpdateUIDialogResult(UIDialogResult);
            lock (MessageBaton)
                Dispatcher.Invoke(Prog);
        }

        public void UIDialogResult()
        {
            DialogResult = MessageBox.Show("Completed creating USB key!\n\nUSB Key is ready for use.\n\nDo you want to create another key?", "Completed!", MessageBoxButton.YesNo, MessageBoxImage.None) == MessageBoxResult.Yes;

            CloseWindow();
        }

        public void FileProg(int progress = 0)
        {
            FileProgress.Value = progress;
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            WaitForCopy = false;
        }
    }
}
