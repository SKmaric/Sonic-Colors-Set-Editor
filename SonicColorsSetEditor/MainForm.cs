﻿using HedgeLib;
using HedgeLib.Sets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace SonicColorsSetEditor
{

    public partial class MainForm : Form
    {

        public static string SonicColorsShortName = "Colors";
        public static string ProgramName = "Sonic Colors Set Editor";
        public static string GameName = "Sonic Colors";
        public static bool HasCPKMaker = false;
        public static bool UseOtherEnglish = false; // lol
        public static bool HasBeenInit = false;
        public static bool saveAsNow = true; //Handles whether to open the save dialogue box and also to cancel the save if necessary.

        public Dictionary<string, SetObjectType> TemplatesColors = null;
        public SetData SetData = null;
        public SetObject SelectedSetObject = null;
        public string CPKDirectory = "";
        public string LoadedFilePath = "";

        public MainForm()
        {

            // Change English. lol
            var list = new string[] { "AU", "UK" }.ToList();
            if (list.Contains(RegionInfo.CurrentRegion.TwoLetterISORegionName))
            {
                ProgramName = "Sonic Colours Set Editor";
                GameName = "Sonic Colours";
                UseOtherEnglish = true;
            }

            InitializeComponent();
        }

        public void Init()
        {
            HasBeenInit = true;
            Config.LoadConfig("config.bin");

            UpdateObjects();

            if (!Directory.Exists("Templates"))
            {
                var result = MessageBox.Show("No Templates Found\n" +
                    "Please download the Templates folder from HedgeLib's Github page\n" +
                    "and place it in the same directory as the executable\n" +
                    "https://github.com/Radfordhound/HedgeLib/tree/master/HedgeEdit/Templates",
                    ProgramName, MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (result == DialogResult.OK)
                    Process.Start("https://github.com/Radfordhound/HedgeLib/tree/master/HedgeEdit/Templates");
                Environment.Exit(-1);
            }
            else
            {
                // Loads the templates
                TemplatesColors = LoadObjectTemplates(SonicColorsShortName);
                foreach (string objName in TemplatesColors.Keys)
                    ComboBox_ObjectType.Items.Add(objName);
            }

            if (File.Exists("CpkMaker.dll"))
            {
                HasCPKMaker = true;
                ToolStripMenuItem_ExtractCPK.Enabled = true;
            }
            else
            {
                Message("WARNING: Could not find \"CpkMaker.dll\". " +
                    "This is required to create and extract cpks.");
            }
        }

        public void Message(string s)
        {
            ToolStrip_Label.Text = s;
            Console.WriteLine(s);
        }

        public void UpdateObjects()
        {
            Text = ProgramName;
            if (SetData != null)
            {
                if (CPKDirectory.Length != 0 && HasCPKMaker)
                {
                    ToolStripMenuItem_BuildCPK.Enabled = true;
                    ToolStripMenuItem_SaveAndBuildCPK.Enabled = true;
                    ToolStripMenuItem_SaveAndLaunchSC.Enabled = true;
                }

                int longestNameLength = 0;

                ListView_Objects.Items.Clear();
                // Adds all SetObjects into the ListView
                foreach (var setObject in SetData.Objects)
                {
                    var lvi = new ListViewItem();
                    // ID
                    lvi.SubItems[0].Text = setObject.ObjectID.ToString("000");
                    
                    // Object Name
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem());
                    lvi.SubItems[1].Text = setObject.ObjectType;

                    lvi.Tag = setObject;

                    if (lvi.SubItems[1].Text.Length > longestNameLength)
                        longestNameLength = lvi.SubItems[1].Text.Length;

                    ListView_Objects.Items.Add(lvi);
                }

                // Update the column length
                ListView_Objects.AutoResizeColumn(1, 
                    (longestNameLength > ListView_Objects.Columns[1].Text.Length) ?
                    ColumnHeaderAutoResizeStyle.ColumnContent :
                    ColumnHeaderAutoResizeStyle.HeaderSize);
                
                ListView_Objects.Enabled = true;
                Button_AddObject.Enabled = true;
                Button_RemoveObject.Enabled = true;
            }
            else
            {
                ListView_Objects.Enabled = false;
                Button_AddObject.Enabled = false;
                Button_RemoveObject.Enabled = false;
                groupBox1.Enabled = false;
                ToolStripMenuItem_BuildCPK.Enabled = false;
                ToolStripMenuItem_SaveAndBuildCPK.Enabled = false;
            }
        }

        public void UpdateTransform(SetObject setObject, int index)
        {
            var objectPos = setObject.Transform.Position;
            var objectRot = setObject.Transform.Rotation.ToEulerAngles();

            if (index != 0)
            {
                objectPos = setObject.Children[index - 1].Position;
                objectRot = setObject.Children[index - 1].Rotation.ToEulerAngles();
            }
            groupBox1.Text = $"Object: {setObject.ObjectType} " +
                $"(X: {objectPos.X}, Y: {objectPos.Y}, Z: {objectPos.Z})";
            if (index != 0) groupBox1.Text += " (Child)";
            NumericUpDown_X.Value = (decimal)objectPos.X;
            NumericUpDown_Y.Value = (decimal)objectPos.Y;
            NumericUpDown_Z.Value = (decimal)objectPos.Z;
            NumericUpDown_Pitch.Value = (decimal)objectRot.X;
            NumericUpDown_Yaw.Value = (decimal)objectRot.Y;
            NumericUpDown_Roll.Value = (decimal)objectRot.Z;

        }

        public void UpdateParameters(SetObject setObject)
        {
            // Clear the Parameter list
            ListView_Param.Items.Clear();

            // Fill the Parameter list
            foreach (var parameter in setObject.Parameters)
            {
                var lvi = new ListViewItem(parameter.Data as string);
                string parameterName = parameter.DataType.ToString();
                int index = setObject.Parameters.IndexOf(parameter);
                var templateParams = TemplatesColors[setObject.ObjectType].Parameters;
                if (index < templateParams.Count)
                {
                    var objectType = templateParams.ElementAt(index);
                    parameterName = objectType.Name;
                    if (parameter.DataType != objectType.DataType)
                        lvi.ForeColor = Color.OrangeRed;
                }
                else
                    lvi.ForeColor = Color.Red;

                // Parameter Name
                lvi.SubItems[0].Text = parameterName;

                // Parameter Value
                lvi.SubItems.Add(new ListViewItem.ListViewSubItem());
                lvi.SubItems[1].Text = parameter.Data.ToString();

                lvi.Tag = parameter;

                ListView_Param.Items.Add(lvi);
            }
        }

        public void UpdateSetObject(SetObject setObject)
        {
            if (setObject != null)
            {
                Text = $"{ProgramName} - {setObject.ObjectType} ({setObject.ObjectID})";
                // Transform
                NumericUpDown_TransIndex.Maximum = setObject.Children.Length;
                UpdateTransform(setObject, 0);

                ComboBox_ObjectType.Text = setObject.ObjectType;
                NumericUpDown_ObjectID.Value = setObject.ObjectID;

                // Clear the Custom Data list
                ListView_CustomData.Items.Clear();

                // Fill Custom Data list
                foreach (var parameter in setObject.CustomData)
                {
                    var lvi = new ListViewItem(parameter.Key);
                    // Parameter Name
                    lvi.SubItems[0].Text = parameter.Key;

                    // Parameter Value
                    lvi.SubItems.Add(new ListViewItem.ListViewSubItem());
                    lvi.SubItems[1].Text = parameter.Value.Data.ToString();

                    lvi.Tag = parameter.Value.DataType;

                    ListView_CustomData.Items.Add(lvi);
                }
                UpdateParameters(setObject);

            }
        }

        public void OpenSetData(string filePath)
        {
            if (!HasBeenInit)
                Init();
            Console.WriteLine("Opening SetData File: {0}", filePath);
            LoadedFilePath = filePath;
            SetData = new ColorsSetData();

            if (filePath.ToLower().EndsWith(".orc"))
            {
                SetData.Load(filePath, TemplatesColors);
            }else if (filePath.ToLower().EndsWith(".xml"))
            {
                CreateObjectTemplateFromXMLSetData(filePath, true);
                SetData.ImportXML(filePath);
            }
            else if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
            {
                CPKDirectory = Path.GetDirectoryName(filePath);
                new SelectStageForm(this, Path.GetDirectoryName(filePath)).ShowDialog();
            }
            UpdateObjects();
            saveToolStripMenuItem.Enabled = true;
            saveAsToolStripMenuItem.Enabled = true;
            Message($"Loaded {SetData.Objects.Count} Objects.");
        }

        public void SaveSetData()
        {
            if (string.IsNullOrEmpty(LoadedFilePath) || saveAsNow)
            {
                var sfd = new SaveFileDialog()
                {
                    Title = "Save SetData",
                    Filter = $"{GameName} SetData|*.orc|XML|*.xml"
                };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    LoadedFilePath = sfd.FileName;
                    saveAsNow = false;
                }
            }
            if (!string.IsNullOrEmpty(LoadedFilePath) && !saveAsNow)
            {
                Console.WriteLine("Saving SetData File: {0}", LoadedFilePath);
                if (LoadedFilePath.ToLower().EndsWith(".orc"))
                {
                    SetData.Save(LoadedFilePath, true);
                }
                else if (LoadedFilePath.ToLower().EndsWith(".xml"))
                {
                    SetData.ExportXML(LoadedFilePath, TemplatesColors);
                }
                saveToolStripMenuItem.Enabled = true;
                Message("Saved.");
            }

        }

        public void SelectSetObject(SetObject setObject)
        {
            SelectedSetObject = setObject;
            if (SelectedSetObject != null)
            {
                UpdateSetObject(SelectedSetObject);
                groupBox1.Enabled = true;
            }
            else
            {
                groupBox1.Text = "Object: (No Object Selected)";
                groupBox1.Enabled = false;
            }
        }

        /// <summary>
        /// Creates temporary templates from a XML file that contains setdata
        /// </summary>
        /// <param name="filePath">Path to the XML</param>
        /// <param name="save"></param>
        public void CreateObjectTemplateFromXMLSetData(string filePath, bool save = false)
        {
            var xml = XDocument.Load(filePath);
            foreach (var objElem in xml.Root.Elements("Object"))
            {
                var typeAttr = objElem.Attribute("type");
                var parametersElem = objElem.Element("Parameters");
                if (typeAttr == null) continue;
                if (TemplatesColors.ContainsKey(typeAttr.Value))
                    continue;
                var objType = new SetObjectType();
                objType.Name = typeAttr.Value;
                
                if (parametersElem != null)
                {
                    foreach (var paramElem in parametersElem.Elements())
                    {
                        var param = new SetObjectTypeParam();
                        var dataTypeAttr = paramElem.Attribute("type");
                        if (dataTypeAttr == null) continue;

                        var dataType = Types.GetTypeFromString(dataTypeAttr.Value);

                        param.Name = paramElem.Name.ToString();
                        param.DataType = dataType;
                        param.DefaultValue = paramElem.Value;
                        param.Description = "TODO";

                        objType.Parameters.Add(param);
                    }
                }
                TemplatesColors.Add(typeAttr.Value, objType);

                if (save)
                    objType.Save(typeAttr.Value + ".xml", true);
            }
        }

        public static void WriteDefaultCustomData(SetObject setObject)
        {
            setObject.CustomData.Add("Unknown1", new SetObjectParam(typeof(ushort), (ushort)0));
            setObject.CustomData.Add("Unknown2", new SetObjectParam(typeof(ushort), (uint)0));
            setObject.CustomData.Add("Unknown3", new SetObjectParam(typeof(ushort), (uint)0));
            setObject.CustomData.Add("Unknown4", new SetObjectParam(typeof(ushort), (float)0));
            setObject.CustomData.Add("RangeIn", new SetObjectParam(typeof(ushort), (float)1000f));
            setObject.CustomData.Add("RangeOut", new SetObjectParam(typeof(ushort), (float)1200f));
        }

        // From GameList.cs (857cc8cfcb799702e2a1312e78df73eaa60e6ec8)
        private static Dictionary<string, SetObjectType> LoadObjectTemplates(string shortName)
        {
            Console.WriteLine("Loading Templates...");
            var sdir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var dirPath = Helpers.CombinePaths(sdir,
                "Templates", shortName);

            if (!Directory.Exists(dirPath))
            {
                Console.WriteLine("WARNING: Templates folder doesn't exist.");
                Console.WriteLine("         This folder is required for converting .orc files to .xml");
                return null;
            }
            var objectTemplates = new Dictionary<string, SetObjectType>();

            foreach (var dir in Directory.GetDirectories(dirPath))
            {
                //TODO: Categories.
                foreach (var file in Directory.GetFiles(dir, "*" + SetObjectType.Extension))
                {
                    var fileInfo = new FileInfo(file);
                    var template = new SetObjectType();
                    string objTypeName = fileInfo.Name.Substring(0,
                        fileInfo.Name.Length - SetObjectType.Extension.Length);

                    template.Load(file);
                    if (objectTemplates.ContainsKey(objTypeName))
                    {
                        Console.WriteLine("WARNING: Skipping over duplicate template \"" +
                            objTypeName + "\".");
                        continue;
                    }

                    objectTemplates.Add(objTypeName, template);
                    Console.WriteLine("Loaded Template for \"" + objTypeName + "\".");
                }
            }

            Console.WriteLine($"Loaded {objectTemplates.Count} Templates.\n");
            return objectTemplates;
        }
        
        #region ToolStripMenuItem

        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetData = new ColorsSetData();
            LoadedFilePath = null;
            saveToolStripMenuItem.Enabled = false;
            saveAsToolStripMenuItem.Enabled = true;
            UpdateObjects();
        }

        private void ToolStripMenuItem_OpenFile_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog()
            {
                Title = "Open SetData",
                Filter = $"{GameName} SetData|*.orc|XML|*.xml"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string filePath = ofd.FileName;
                OpenSetData(filePath);
            }
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveAsNow = false;
            SaveSetData();
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveAsNow = true;
            SaveSetData();
        }

        private void ToolStripMenuItem_OpenExtractedCPK_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog()
            {
                Title = "Open Game Data Directory (sonic2010_0)",
                FileName = "Go into sonic2010_0 and press Save"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                CPKDirectory = Path.GetDirectoryName(sfd.FileName);
                new SelectStageForm(this, Path.GetDirectoryName(sfd.FileName)).ShowDialog();
            }
        }

        private void ToolStripMenuItem_BuildCPK_Click(object sender, EventArgs e)
        {
            var cpkMaker = new CPKMaker();
            cpkMaker.BuildCPK(CPKDirectory);
            MessageBox.Show("Done.", ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ToolStripMenuItem_SaveAndBuildCPK_Click(object sender, EventArgs e)
        {
            SaveSetData();
            var cpkMaker = new CPKMaker();
            cpkMaker.BuildCPK(CPKDirectory);
            MessageBox.Show("Done.", ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ToolStripMenuItem_ExtractCPK_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog()
            {
                Title = "Open CPK (Extract CPK)",
                Filter = "CRIWARE CPK Archive|*.cpk"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var cpkMaker = new CPKMaker();
                string filePath = ofd.FileName;
                string directory = cpkMaker.ExtractCPK(filePath);
                if (Directory.Exists(Helpers.CombinePaths(directory, "set")))
                {
                    if (Directory.GetFiles(Helpers.CombinePaths(directory, "set")).Length > 0)
                    {
                        var dialogResult =
                            MessageBox.Show("One or more set files has been found\n" +
                            "Would you like to load one of the set files?", ProgramName,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dialogResult == DialogResult.Yes)
                        {
                            new SelectStageForm(this, directory).ShowDialog();
                        }
                        else
                        {
                            MessageBox.Show("Done.", ProgramName, MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        private void ToolStripMenuItem_SaveAndLaunchSC_Click(object sender, EventArgs e)
        {
            SaveSetData();
            var cpkMaker = new CPKMaker();
            Console.Write("Building CPK... ");
            cpkMaker.BuildCPK(CPKDirectory);
            Console.WriteLine("Done.");

            if (Config.DolphinExecutablePath.Length == 0)
            {
                Message("Dolphin is not set up yet");
                MessageBox.Show("Please locate your Dolphin executable.", ProgramName);
                var ofd = new OpenFileDialog()
                {
                    Title = "Locate Dolphin.exe",
                    Filter = "Windows Executable|*.exe"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                    Config.DolphinExecutablePath = ofd.FileName;
                else
                    return;
                Config.SaveConfig("config.bin");
            }

            try
            {
                string dvdRootPath = Directory.GetParent(CPKDirectory).FullName;
                string dolFilePath = Helpers.CombinePaths(dvdRootPath, "boot.dol");
                string apploaderPath = Helpers.CombinePaths(dvdRootPath, "apploader.img");

                if (!File.Exists(dolFilePath))
                {
                    MessageBox.Show("Could not find boot.dol\n" +
                    "Make sure boot.dol is in the parent directory of \"sonic2010_0\"");
                    return;
                }

                // Dolphin Config
                string dolphinConfigPath = Helpers.CombinePaths(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Dolphin Emulator", "Config", "Dolphin.ini");
                string[] dolphinConfig = File.ReadAllLines(dolphinConfigPath);

                for (int i = 0; i < dolphinConfig.Length; ++i)
                {
                    if (dolphinConfig[i].StartsWith("DVDRoot"))
                        dolphinConfig[i] = $"DVDRoot = {dvdRootPath}";
                    if (dolphinConfig[i].StartsWith("Apploader"))
                        dolphinConfig[i] = $"Apploader = {apploaderPath}";
                }
                Console.WriteLine("Saving Dolphin Config");
                File.WriteAllLines(dolphinConfigPath, dolphinConfig);

                var process = Process.Start(Config.DolphinExecutablePath, $"/b -e \"{dolFilePath}\"");
                Console.WriteLine("Starting Dolphin... [{0}]", process.StartInfo.Arguments);
                while (!process.HasExited)
                {
                    Enabled = false;
                    Thread.Sleep(1000);

                    IntPtr messageBoxHandle = FindWindow(null, "Warning");
                    if (messageBoxHandle == IntPtr.Zero)
                        continue;
                    IntPtr labelHandle = GetDlgItem(messageBoxHandle, 0xFFFF);
                    var sb = new StringBuilder(GetWindowTextLength(labelHandle));
                    GetWindowText(labelHandle, sb, sb.Capacity);
                    if (sb.ToString().Contains("Unknown instruction"))
                    {
                        process.Kill();
                        Console.WriteLine("The Guest has stopped working!");
                        Console.WriteLine("Returned: {0}", sb);
                        MessageBox.Show("The Guest has stopped working.\n" + sb, ProgramName,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                Console.WriteLine("Process is nolonger running");
                Enabled = true;
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Could not find Dolphin\n" +
                    "Reseting executable path");
                Config.DolphinExecutablePath = "";
                Config.SaveConfig("config.bin");
            } catch { }
        }

        #endregion ToolStripMenuItem

        #region Transforms

        private void Button_TransformApply_Click(object sender, EventArgs e)
        {
            if (ListView_Objects.SelectedItems.Count != 0)
            {
                var transformIndex = (int)NumericUpDown_TransIndex.Value;
                var lvi = ListView_Objects.SelectedItems[0];
                var setObject = (SetObject)lvi.Tag;
                var objectTransform = transformIndex == 0 ? setObject.Transform :
                    setObject.Children[transformIndex - 1];
                var objectPos = new Vector3();

                // Set the Transformation
                objectPos.X = (float)NumericUpDown_X.Value;
                objectPos.Y = (float)NumericUpDown_Y.Value;
                objectPos.Z = (float)NumericUpDown_Z.Value;

                objectTransform.Rotation = new Quaternion(new Vector3(
                    (float)NumericUpDown_Pitch.Value,
                    (float)NumericUpDown_Yaw.Value,
                    (float)NumericUpDown_Roll.Value), false);
                objectTransform.Position = objectPos;

                UpdateSetObject(setObject);
                UpdateTransform(setObject, (int)NumericUpDown_TransIndex.Value);
            }
        }

        private void NumericUpDown_TransIndex_ValueChanged(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                UpdateTransform(SelectedSetObject, (int)NumericUpDown_TransIndex.Value);
                // Disable The remove button if 0 is selected;
                Button_RemoveTransform.Enabled = NumericUpDown_TransIndex.Value != 0;
            }
        }

        private void Button_Add_Transform_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                // Adds an extra slot so we can add another transform
                Array.Resize(ref SelectedSetObject.Children, SelectedSetObject.Children.Length + 1);
                SelectedSetObject.Children[SelectedSetObject.Children.Length - 1] = new SetObjectTransform();
                UpdateSetObject(SelectedSetObject);
                NumericUpDown_TransIndex.Value = SelectedSetObject.Children.Length;
            }
        }

        private void Button_RemoveTransform_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                var list = new List<SetObjectTransform>(SelectedSetObject.Children);
                list.RemoveAt((int)(NumericUpDown_TransIndex.Value - 1));
                SelectedSetObject.Children = list.ToArray();
                UpdateSetObject(SelectedSetObject);
            }
        }

        #endregion Transforms

        #region ObjectSelection

        private void ListView_Objects_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            SelectSetObject(e.IsSelected ? e.Item.Tag as SetObject : null);
        }

        private void Button_AddObject_Click(object sender, EventArgs e)
        {
            if (SetData != null)
                new NewObjectForm(this).ShowDialog();
            UpdateObjects();
        }

        private void Button_RemoveObject_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                SetData.Objects.Remove(SelectedSetObject);
                SelectSetObject(null);
                UpdateObjects();
            }
        }

        #endregion ObjectSelection

        private void ListView_Param_DoubleClick(object sender, EventArgs e)
        {
            if (ListView_Param.SelectedItems.Count != 0)
            {
                var lvi = ListView_Param.SelectedItems[0];
                var param = (SetObjectParam)lvi.Tag;

                new EditParamForm(param).ShowDialog();
                lvi.SubItems[1].Text = param.Data.ToString();
            }
        }
        
        private void Button_MainApply_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                SelectedSetObject.ObjectType = ComboBox_ObjectType.Text;
                SelectedSetObject.ObjectID = (uint)NumericUpDown_ObjectID.Value;
                UpdateObjects();
            }
        }

        private void Button_AddParam_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                new AddParamForm(SelectedSetObject).ShowDialog();
                UpdateSetObject(SelectedSetObject);
            }
        }

        private void Button_RemoveParam_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                if (ListView_Param.SelectedItems.Count != 0)
                {
                    var lvi = ListView_Param.SelectedItems[0];
                    var param = (SetObjectParam)lvi.Tag;
                    // Removes the parameter from the ListView
                    ListView_Param.Items.Remove(lvi);
                    // Removes the parameter from the SetObject
                    SelectedSetObject.Parameters.Remove(param);
                }
            }
        }

        private void ListView_Param_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            Button_RemoveParam.Enabled = e.IsSelected;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                if (files.Length > 0 && (files[0].ToLower().EndsWith(".orc") ||
                        files[0].ToLower().EndsWith(".xml")))
                    e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                if (files.Length > 0 && (files[0].ToLower().EndsWith(".orc") ||
                        files[0].ToLower().EndsWith(".xml")))
                    OpenSetData(files[0]);
            }
        }

        // Pinvokes

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialogHandle, int controlID);

        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr Handle, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string WindowName);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr Handle);

    }
}
