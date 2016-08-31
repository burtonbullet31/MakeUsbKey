using System;
using System.Windows;
using Make_USB_Key.ArgEngine;
using System.Xml.Linq;
using System.Management;
using System.IO;
using System.Windows.Forms;

namespace Make_USB_Key
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ArgEngine.ArgEngine CommandArguments = null;
        bool Rerun { get; set; } = false;

        public MainWindow()
        {
            InitializeComponent();

            CommandArguments = new ArgEngine.ArgEngine(Environment.GetCommandLineArgs());

            CommandArguments.SetArg(ARGS.VOLUME_LABEL, CommandArguments.GetArgValue(ARGS.VOLUME_LABEL));

            LoadXML();

            Loaded += MainWindow_Loaded;
        }

        private void LoadXML()
        {
            if (!System.IO.File.Exists("settings.xml")) return;

            XDocument doc = XDocument.Load("settings.xml");
            XElement root = doc.Root;

            foreach(XElement element in root.Elements())
            {
                switch(element.Name.ToString())
                {
                    case "defaultpath":
                        CommandArguments.SetArg(ARGS.SOURCE, element.Value);
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
                        CommandArguments.SetArg(ARGS.VOLUME_LABEL, element.Value);
                        break;
                    default:
                        break;
                }
            }
            
        }

        private bool IsTrue(string value)
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

            CommandArguments.SetArg(ARGS.SOURCE, SourceTextBox.Text);
            CommandArguments.SetArg(ARGS.DESTINATION, ConvertDrive(DestinationComboBox.SelectedItem as string));

            Rerun = FormatFlashDrive(ConvertDrive(DestinationComboBox.SelectedItem as string), SourceTextBox.Text, SourceTextBox.Text, CommandArguments.GetArgValue(ARGS.VOLUME_LABEL));

            if (!Rerun)
                Close();
            else
            {
                UpdateSystemDrives();
                Show();
            }
        }

        private string GetFullPath(string text)
        {
            if (RelativeSource(text))
            {
                return (System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + text);
            }
            return text;
        }

        private bool RelativeSource(string text)
        {
            if (text.Contains(":"))
                return false;
            return true;
        }

        private bool FormatFlashDrive(string path, string source, string origsource, string volume = "")
        {
            FormatFlashDriveOutput Formatter = new FormatFlashDriveOutput(path, GetFullPath(source), origsource, volume);
            Formatter.DriveLetter = path;
            Nullable<bool> dialogResult = Formatter.ShowDialog();

            return Formatter.DialogResult ?? false;
        }
        
        private string ConvertDrive(string SelectedDrive)
        {
            if (SelectedDrive == null)
            {
                return null;
            }
            string[] Temp = SelectedDrive.Split(' ');

            return Temp[0];
        }
        
        private void UpdateSystemDrives()
        {
            bool Done = false;
            while (!Done)
            {
                try
                {
                    ManagementObjectSearcher ms = new ManagementObjectSearcher("Select * from Win32_LogicalDisk");
                    ManagementObjectCollection mo = ms.Get();
                    DestinationComboBox.Items.Clear();

                    foreach (string LogicalDrive in Directory.GetLogicalDrives())
                    {
                        string DrivePlusVolume = LogicalDrive;
                        foreach (ManagementObject drive in mo)
                        {
                            string Label = null;
                            if (mo == null) continue;
                            switch ((uint)drive["DriveType"])
                            {
                                case 2:
                                case 3:
                                    break;
                                default:
                                    continue;
                            }
                            if (LogicalDrive != drive["DeviceID"].ToString() + "\\") continue;
                            try
                            {
                                Label = (string)drive["VolumeName"];
                            }
                            catch { }

                            if (Label == null || Label == "")
                                break;

                            DrivePlusVolume += " " + Label;

                            DestinationComboBox.Items.Add(DrivePlusVolume);
                        }
                    }
                    Done = true;
                }
                catch (System.InvalidOperationException) { }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSystemDrives();
        }
        private bool NotReady()
        {
            if (DestinationComboBox.SelectedItem == null) return true;

            bool notready = ((DestinationComboBox.SelectedItem == null ||
                    SourceTextBox.Text == ""));

            return notready;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSystemDrives();
        }

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            string NewPath = GetFolderPath("Source Path", false);
            if (NewPath == "") return;
            SourceTextBox.Text = NewPath;
        }

        private string GetFolderPath(string Description = null, bool NewFolder = true)
        {
            // Create Folder Dialog object
            FolderBrowserDialog Folder = new FolderBrowserDialog();

            // Set Folder options
            if (Description != null) Folder.Description = Description;
            Folder.ShowNewFolderButton = NewFolder;

            // Display Dialog
            Nullable<DialogResult> Result = Folder.ShowDialog();

            // Return selected path
            if (Result.ToString() == "OK")
                return Folder.SelectedPath;

            return "";
        }
    }
}
