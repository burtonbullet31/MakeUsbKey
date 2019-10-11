using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MakeUsbKey
{
    public class VolumeInfo
    {
        public string VolumeName { get; set; }
        public string Letter { get; set; }
        public string Label { get; set; }
        public string Fs { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string Status { get; set; }

        public VolumeInfo()
        {
            VolumeName = "";
            Letter = "";
            Label = "";
            Fs = "";
            Type = "";
            Size = "";
            Status = "";
        }
    }


    /// <summary>
    /// Interaction logic for FormatFlashDriveOutput.xaml
    /// </summary>
    public partial class FormatFlashDriveOutput
    {
        /// <summary>
        /// Function delegate used to update the UI Console output
        /// </summary>
        /// <param name="message"></param>
        public delegate void UpdateUiThread(string message);
        public delegate void UpdateUiThreadFail(string message, bool fail);
        public delegate void UpdateUiDriveLetter(string letter);
        public delegate void UpdateUiFileProgress(int progress);
        public delegate void UpdateUiDialogResult();
        public delegate void Closer();

        public string DriveLetter { get; set; }
        public string NewVolumeName { get; set; }
        public string SourceFolder { get; set; }
        public string OrigSource { get; set; }
        private readonly WebClient _fileWebClient;
        private bool _waitForCopy;
        private Thread _workThread;
        private readonly object _messageBaton = new object();

        //ConsoleContent ConsoleOut = new ConsoleContent();

        public FormatFlashDriveOutput(string driveLetter, string source, string origSource, string volume = "")
        {
            InitializeComponent();
            SourceFolder = source;
            NewVolumeName = volume;
            OrigSource = origSource;
            //DataContext = ConsoleOut;
            DriveLetter = driveLetter;
            DriveLetterTextBox.Text = DriveLetter;
            _fileWebClient = new WebClient();

            FileProgress.Maximum = 100;
            FileProgress.Minimum = 0;
            FileProgress.Value = 0;
            _fileWebClient.DownloadProgressChanged += ProgressChanged;
            _fileWebClient.DownloadFileCompleted += Completed;
            
            Loaded += Start;
        }

        public void Start(object j = null, EventArgs e = null)
        {
            Show();
            AddMessage("Initializing...");
            AddMessage("");

            if (!Directory.Exists(SourceFolder))
            {
                OutputWindow.Items.Add(OutBox(""));

                var block = new TextBlock
                {
                    Text = "Directory \"" + SourceFolder + "\" does not exist!", Foreground = Brushes.Red
                };

                OutputWindow.Items.Add(block);
                return;
            }

            _workThread = new Thread(RunProcess);
            _workThread.SetApartmentState(ApartmentState.STA);
            _workThread.Start();
            
            OutputWindow.Items.Refresh();
        }

        private static Process ProcessOp(string program)
        {
            var newProc = new Process
            {
                StartInfo =
                {
                    FileName = program,
                    ErrorDialog = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    LoadUserProfile = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true
                }
            };

            return newProc;
        }

        private void RunProcess()
        {
            char[] separators = { '\n' };
            var outArray = new ArrayList();


            UpdateUiMessages("Building volume information file.");
            
            var p = ProcessOp("diskpart.exe");
            p.StartInfo.ErrorDialog = true;
            p.Start();
            p.StandardInput.WriteLine("list volume");
            p.StandardInput.WriteLine("exit");
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            p.Close();

            UpdateUiMessages(output);

            foreach (var line in output.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                outArray.Add(line);

            var volume = GetVolume(outArray);

            UpdateUiMessages("");
            UpdateUiMessages("Drive to be formatted is: " + volume.VolumeName);
            UpdateUiMessages("Formatting...");

            p = ProcessOp("diskpart.exe");
            p.StartInfo.ErrorDialog = true;
            p.Start();
            p.StandardInput.WriteLine("select volume " + volume.Letter);
            p.StandardInput.WriteLine("remove letter=" + volume.Letter);
            p.StandardInput.WriteLine("clean");
            p.StandardInput.WriteLine("create partition primary");
            p.StandardInput.WriteLine("select partition 1");
            p.StandardInput.WriteLine("active");
            p.StandardInput.WriteLine("format fs=fat32 label=" + NewVolumeName + " quick");
            p.StandardInput.WriteLine("assign letter=" + volume.Letter);
            p.StandardInput.WriteLine("exit");
            p.WaitForExit();

            UpdateUiMessages(p.StandardOutput.ReadToEnd());
            UpdateUiMessages("");
            UpdateUiMessages("Completed formatting drive. Prepping for file transfer.");
            
            p.Close();

            UpdateDriveLetter(volume.Letter + ":\\");

            UpdateUiMessages("");
            UpdateUiMessages("Beginning file transfer for files inside: " + SourceFolder);

            foreach (var directory in Directory.EnumerateDirectories(OrigSource, "*", SearchOption.AllDirectories))
            {
                var path = directory.Substring(directory.Substring((directory.IndexOf(OrigSource, StringComparison.Ordinal))).IndexOf("\\", StringComparison.Ordinal));
                UpdateUiMessages("Creating directory: " + path);
                Directory.CreateDirectory(volume.Letter + ":\\" + path);
            }

            UpdateUiMessages("");
            UpdateUiMessages("Completed creating folder structure. Copying files.");
            UpdateUiMessages("");

            foreach (var file in Directory.EnumerateFiles(SourceFolder,"*.*",SearchOption.AllDirectories))
            {
                _waitForCopy = true;

                var path = Path.GetDirectoryName(file)?.Substring((file.IndexOf(OrigSource, StringComparison.Ordinal)+OrigSource.Length));

                _fileWebClient.DownloadFileAsync(new Uri(file), volume.Letter + ":" + path + "\\" + Path.GetFileName(file));
                UpdateUiMessages("Copying file: " + file);

                if (!File.Exists(file))
                    UpdateUiMessages("FAILED TO FIND: " + volume.Letter + ":" + path + "\\" + Path.GetFileName(file), true);

                while (_waitForCopy)
                    Thread.Sleep(25);
                SetFileProgress();
                if (!File.Exists(volume.Letter + ":" + path + "\\" + Path.GetFileName(file)))
                    UpdateUiMessages("FAILED: " + volume.Letter + ":" + path + "\\" + Path.GetFileName(file), true);
                else
                    UpdateUiMessages("SUCCESS: " + volume.Letter + ":" + path + "\\" + Path.GetFileName(file));

                UpdateUiMessages("");
            }

            UpdateUiMessages("");
            UpdateUiMessages("");
            UpdateUiMessages("Completed creating USB key! USB Key is ready for use.");
            UpdateUiMessages("");

            SetUiDialogResult();
        }
        
        private static TextBlock OutBox(string input)
        {
            var block = new TextBlock {Text = input, Foreground = Brushes.Green};

            return block;
        }
        
        private VolumeInfo GetVolume(IEnumerable input, bool postFormat = false)
        {
            var info = new VolumeInfo();
            char[] sepValue = { ' ' };
            var volumes = (from string inputString in input
            select inputString.Split(sepValue, StringSplitOptions.RemoveEmptyEntries)
            into lineIn
            where lineIn[0].Trim() == "Volume" && !lineIn[1].Trim().Contains("#")
            select new VolumeInfo
            {
                VolumeName = lineIn[0].Trim() + " " + lineIn[1].Trim(),
                Letter = lineIn[2].Trim().Split(':')[0],
                Label = lineIn[3].Trim(),
                Fs = lineIn[4].Trim(),
                Type = lineIn[5].Trim(),
                Size = lineIn[6].Trim() + " " + lineIn[7],
                Status = lineIn[8]
            }).ToDictionary(volume => volume.VolumeName);

            foreach (var volume in volumes.Values.Where(volume => (!postFormat && (DriveLetter.Trim().Split(':')[0] == volume.Letter)) || (postFormat && (volume.Label.ToLower() == "rework_v3"))))
            {
                UpdateUiMessages("\nMatched!\n");
                UpdateUiMessages("Volume: " + volume.VolumeName);
                UpdateUiMessages("Letter: " + volume.Letter);
                UpdateUiMessages("Label: " + volume.Label);
                UpdateUiMessages("File System: " + volume.Fs);
                UpdateUiMessages("Type: " + volume.Type);
                UpdateUiMessages("Size: " + volume.Size);
                UpdateUiMessages("Status: " + volume.Status);
                info = volume;
                break;
            }

            return info;
        }

        private void UpdateUiMessages(string input)
        {
            UpdateUiThread uiMessenger = AddMessage;

            lock (_messageBaton)
            {
                Dispatcher?.Invoke(uiMessenger, input);
            }
        }

        private void UpdateUiMessages(string input, bool fail)
        {
            UpdateUiThreadFail uiMessenger = AddMessage;

            lock (_messageBaton)
            {
                Dispatcher?.Invoke(uiMessenger, input, fail);
            }
        }

        private void UpdateDriveLetter(string letter)
        {
            UpdateUiDriveLetter uiLetter = UpdateLetter;
            lock (_messageBaton)
            {
                Dispatcher?.Invoke(uiLetter, letter);
            }
        }

        public void CloseWindow()
        {
            Thread.Sleep(1000);
            Close();
        }

        private void AddMessage(string message)
        {
            var output = new TextBlock {Text = message, Foreground = Brushes.Green, Background = Brushes.Black};

            OutputWindow.Items.Add(output);
            OutputWindow.Items.Refresh();
            OutputWindow.ScrollIntoView(output);
        }

        private void AddMessage(string message, bool fail)
        {
            var output = new TextBlock
            {
                Text = message, Foreground = !fail ? Brushes.Green : Brushes.Red, Background = Brushes.Black
            };

            OutputWindow.Items.Add(output);
            OutputWindow.Items.Refresh();
            OutputWindow.ScrollIntoView(output);
        }

        private void UpdateLetter(string letter)
        {
            label.Content = "Destination: ";
            DriveLetterTextBox.Text = letter;
        }

        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            SetFileProgress(e.ProgressPercentage);
        }

        internal void SetFileProgress(int progress = 0)
        {
            Dispatcher?.Invoke((UpdateUiFileProgress)FileProgressDelegate, progress);
        }
        internal void SetUiDialogResult()
        {
            lock (_messageBaton)
            {
                Dispatcher?.Invoke((UpdateUiDialogResult) UiDialogResult);
            }
        }

        public void UiDialogResult()
        {
            DialogResult = MessageBox.Show("Completed creating USB key!\n\nUSB Key is ready for use.\n\nDo you want to create another key?", "Completed!", MessageBoxButton.YesNo, MessageBoxImage.None) == MessageBoxResult.Yes;

            CloseWindow();
        }

        public void FileProgressDelegate(int progress = 0)
        {
            FileProgress.Value = progress;
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            _waitForCopy = false;
        }
    }
}
