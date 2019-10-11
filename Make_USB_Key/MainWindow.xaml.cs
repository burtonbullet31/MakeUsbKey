using System;
using System.Windows;
using System.Xml.Linq;
using System.Management;
using System.IO;
using System.Windows.Forms;
using MakeUsbKey.ArgEngine.Enumerations;

namespace MakeUsbKey
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ArgEngine.ArgEngine _commandArguments;
        private bool Rerun { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            _commandArguments = new ArgEngine.ArgEngine(Environment.GetCommandLineArgs());

            _commandArguments.SetArg(Arg.VolumeLabel, _commandArguments.GetArgValue(Arg.VolumeLabel));

            LoadXml();

            Loaded += MainWindow_Loaded;
        }

        private void LoadXml()
        {
            if (!File.Exists("settings.xml")) return;

            var doc = XDocument.Load("settings.xml");
            var root = doc.Root;

            if (root == null) return;
            foreach (var element in root.Elements())
            {
                switch (element.Name.ToString())
                {
                    case "defaultpath":
                        _commandArguments.SetArg(Arg.Source, element.Value);
                        SourceTextBox.Text = element.Value;
                        break;
                    case "editdefaultpath":
                        if (!IsTrue(element.Value))
                        {
                            SourceTextBox.IsEnabled = false;
                            PathButton.IsEnabled = false;
                        }

                        break;
                    case "volumelabel":
                        _commandArguments.SetArg(Arg.VolumeLabel, element.Value);
                        break;
                }
            }
        }

        private static bool IsTrue(string value)
        {
            switch (value.ToLower())
            {
                case "true":
                case "yes":
                    return true;
                default:
                    return false;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BeginButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotReady())
            {
                System.Windows.MessageBox.Show("Not all fields are populated.", "Unable To Begin", MessageBoxButton.OK);
                return;
            }

            if (System.Windows.MessageBox.Show("WARNING!\n\nDrive \"" + ConvertDrive(DestinationComboBox.SelectedItem as string) + "\"will be formatted!!!\n\nDo you wish to continue?", "WARNING!!! Format Drive?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                return;

            Hide();

            _commandArguments.SetArg(Arg.Source, SourceTextBox.Text);
            _commandArguments.SetArg(Arg.Destination, ConvertDrive(DestinationComboBox.SelectedItem as string));

            Rerun = FormatFlashDrive(ConvertDrive(DestinationComboBox.SelectedItem as string), SourceTextBox.Text, SourceTextBox.Text, _commandArguments.GetArgValue(Arg.VolumeLabel));

            if (!Rerun)
                Close();
            else
            {
                UpdateSystemDrives();
                Show();
            }
        }

        private static string GetFullPath(string text)
        {
            if (RelativeSource(text))
            {
                return (Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + text);
            }
            return text;
        }

        private static bool RelativeSource(string text)
        {
            return !text.Contains(":");
        }

        private static bool FormatFlashDrive(string path, string source, string origSource, string volume = "")
        {
            var formatter =
                new FormatFlashDriveOutput(path, GetFullPath(source), origSource, volume)
                {
                    DriveLetter = path
                };
            formatter.ShowDialog();

            return formatter.DialogResult ?? false;
        }
        
        private static string ConvertDrive(string selectedDrive)
        {
            var temp = selectedDrive?.Split(' ');

            return temp?[0];
        }
        
        private void UpdateSystemDrives()
        {
            var done = false;
            while (!done)
            {
                try
                {
                    var ms = new ManagementObjectSearcher("Select * from Win32_LogicalDisk");
                    var mo = ms.Get();
                    DestinationComboBox.Items.Clear();

                    foreach (var logicalDrive in Directory.GetLogicalDrives())
                    {
                        var drivePlusVolume = logicalDrive;
                        foreach (var o in mo)
                        {
                            var drive = (ManagementObject) o;
                            if ((uint) drive["DriveType"] != 2 && (uint) drive["DriveType"] != 3) continue;
                            if (logicalDrive != drive["DeviceID"] + "\\") continue;
                            
                            drivePlusVolume += " " + (string)drive["VolumeName"] ?? string.Empty;

                            DestinationComboBox.Items.Add(drivePlusVolume);
                        }
                    }
                    done = true;
                    ms.Dispose();
                }
                catch (InvalidOperationException) { }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSystemDrives();
        }

        private bool NotReady() => DestinationComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(SourceTextBox.Text);

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSystemDrives();
        }

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            var newPath = GetFolderPath("Source Path", false);
            if (string.IsNullOrWhiteSpace(newPath))
            {
                return;
            }

            SourceTextBox.Text = newPath;
        }

        private static string GetFolderPath(string description = null, bool newFolder = true)
        {
            // Create Folder Dialog object
            var folder = new FolderBrowserDialog();

            // Set Folder options
            if (description != null) folder.Description = description;
            folder.ShowNewFolderButton = newFolder;

            // Display Dialog
            DialogResult? result = folder.ShowDialog();

            // Return selected path
            if (result.ToString() != "OK")
            {
                folder.Dispose();
                return "";
            }

            var path = folder.SelectedPath;
            folder.Dispose();
            return path;

        }
    }
}
