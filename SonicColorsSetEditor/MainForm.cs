﻿using HedgeLib;
using HedgeLib.Sets;
using SonicColorsSetEditor.Properties;
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

        // Variables/Constants
        public static string TemplatesPath = Path.Combine(Application.StartupPath, "Templates");
        public static string SonicColorsShortName = "Colors";
        public static string ProgramName = "Sonic Colors Set Editor";
        public static string GameName = "Sonic Colors";

        public static bool HasCPKMaker = false;
        public static bool UseOtherEnglish = false; // lol
        public static bool HasBeenInit = false;

        public Dictionary<string, SetObjectType> TemplatesColors = null;
        public ColorsSetData SetData = null;
        public SetObject SelectedSetObject = null;

        public string CPKDirectory = "";
        public string LoadedFilePath = "";

        //Generations conversion
        public ColorstoGensSetData ColorstoGensSetData = null;
        public Dictionary<string, string> ColorstoGensRenamers = null;
        public Dictionary<string, string> ColorstoGensObjPhys = null;
        public Dictionary<string, string> ColorstoGensPosYMods = null;
        public Dictionary<string, string> ColorstoGensRotateXMods = null;
        public Dictionary<string, string> ColorstoGensRotateYMods = null;
        public Dictionary<string, string> ColorstoGensParamMods = null;

        // Constructors
        public MainForm()
        {
            InitializeComponent();
            
            // Change English. lol
            var list = new string[] { "AU", "UK" }.ToList();
            UseOtherEnglish = list.Contains(RegionInfo.CurrentRegion.TwoLetterISORegionName);
            
            ProgramName = ChangeEnglish(ProgramName);
            GameName    = ChangeEnglish(GameName);

            // Check if compiled in Debug
            #if DEBUG
            debugToolStripMenuItem.Visible = true;
            #endif
        }

        // Methods
        public string ChangeEnglish(string text)
        {
            var conversions = new string[] { "Color", "Colour" };
            if (UseOtherEnglish)
                for (int i = 0; i < conversions.Length; i += 2)
                    text = text.Replace(conversions[i], conversions[i + 1]);
            return text;
        }

        /// <summary>
        /// Initialises Sonic Colors Set Editor
        /// </summary>
        public void Init()
        {
            HasBeenInit = true;
            Config.LoadConfig("config.bin");

            UpdateObjects();

            if (!Directory.Exists("Templates"))
            {
                var result = MessageBox.Show(Resources.NoTemplatesText,
                    ProgramName, MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (result == DialogResult.OK)
                    Process.Start(Resources.TemplatesURL);
                Environment.Exit(-1);
            }
            else
            {
                // Loads the object templates
                TemplatesColors = SetObjectType.LoadObjectTemplates(TemplatesPath, SonicColorsShortName);
                foreach (string objName in TemplatesColors.Keys)
                    ComboBox_ObjectType.Items.Add(objName);
                Console.WriteLine("Loaded {0} Templates.", TemplatesColors.Count);
            }

            if (File.Exists("CpkMaker.dll"))
            {
                HasCPKMaker = true;
                ToolStripMenuItem_ExtractCPK.Enabled = true;
            }
            else
            {
                Message(Resources.CPKMakerNotFoundText);
            }
        }

        public void Message(string s)
        {
            ToolStrip_Label.Text = s;
            Console.WriteLine(s);
        }

        /// <summary>
        /// Updates the Object List
        /// </summary>
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
                    ToolStripMenuItem_LaunchSCWithoutSaving.Enabled = true;
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

                    if (!TemplatesColors.ContainsKey(setObject.ObjectType))
                        lvi.ForeColor = Color.Red;

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
                ReassignAllObjects_ToolStripMenuItem.Enabled = true;
            }
            else
            {
                ListView_Objects.Enabled = false;
                Button_AddObject.Enabled = false;
                Button_RemoveObject.Enabled = false;
                groupBox1.Enabled = false;
                ToolStripMenuItem_BuildCPK.Enabled = false;
                ToolStripMenuItem_SaveAndBuildCPK.Enabled = false;
                ReassignAllObjects_ToolStripMenuItem.Enabled = false;
            }
        }


        /// <summary>
        /// Updates the GUI to show the selected transform from the passed SetObject
        /// </summary>
        /// <param name="setObject"></param>
        /// <param name="index"></param>
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

        /// <summary>
        /// Updates the GUI to show all the parameters from the passed SetObject
        /// </summary>
        /// <param name="setObject"></param>
        public void UpdateParameters(SetObject setObject)
        {
            // Clear the Parameter list
            ListView_Param.Items.Clear();

            // Fill the Parameter list
            foreach (var parameter in setObject.Parameters)
            {
                var lvi = new ListViewItem(parameter.Data as string);

                var setObjectType = TemplatesColors[setObject.ObjectType];
                var setObjectParams = setObjectType.Parameters;
                string parameterName = parameter.DataType.ToString();
                int parameterIndex = setObject.Parameters.IndexOf(parameter);

                if (parameterIndex < setObjectParams.Count && parameterIndex != -1)
                {
                    var parameterType = setObjectParams.ElementAt(parameterIndex);
                    lvi.ToolTipText = parameterType.Description;
                    parameterName = parameterType.Name;
                    if (parameter.DataType != parameterType.DataType)
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

        /// <summary>
        /// Updates the GUI to show the information of the passed SetObject
        /// </summary>
        /// <param name="setObject"></param>
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
                if (Path.GetDirectoryName(filePath).EndsWith("set"))
                    CPKDirectory = Directory.GetParent(Path.GetDirectoryName(filePath))
                        .FullName;
                SetData.Load(filePath, TemplatesColors);

                //Hacky solution for JumpBoard
                if (SetData.Header.IsBigEndian)
                {
                    foreach (var Obj in SetData.Objects) if (Obj.ObjectType == "JumpBoard")
                        {
                            if (Obj.Parameters[3].DataType == typeof(float))
                            {
                                byte value = BitConverter.GetBytes((float)Obj.Parameters[3].Data)[3];
                                Obj.Parameters[3].Data = 0f;
                                Obj.Parameters[4].Data = value;
                            }
                        }
                }

            }
            else if (filePath.ToLower().EndsWith(".xml"))
            {
                CreateObjectTemplateFromXMLSetData(filePath, true);
                SetData.ImportXML(filePath);
            }
            else if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
            {
                CPKDirectory = Path.GetDirectoryName(filePath);
                new SelectStageForm(this, Path.GetDirectoryName(filePath)).ShowDialog();
            }

            if (!string.IsNullOrEmpty(LoadedFilePath))
                ReloadSetData_ToolStripMenuItem.Enabled = true;

            saveToolStripMenuItem.Enabled = true;
            saveAsToolStripMenuItem.Enabled = true;
            Message($"Loaded {SetData.Objects.Count} Objects. Removing Unknown Objects");
            // Remove SetObjects that have no templates
            SetData.Objects.RemoveAll(t => string.IsNullOrEmpty(t.ObjectType));
            Message($"Loaded {SetData.Objects.Count} Objects.");
            chkbox_IsUltimate.Checked = !SetData.Header.IsBigEndian;
            UpdateObjects();
        }

        public void SaveSetData(bool saveAs = false)
        {
            // Opens the Save dialog if saveAs is true or there is no path for setData
            if (string.IsNullOrEmpty(LoadedFilePath) || saveAs)
            {
                var sfd = new SaveFileDialog()
                {
                    Title = "Save SetData",
                    Filter = $"{GameName} SetData|*.orc|Sonic Generations SetData|*.set.xml|XML|*.xml"
                };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    LoadedFilePath = sfd.FileName;
                    saveAs = false;
                }
            }
            if (!string.IsNullOrEmpty(LoadedFilePath) && !saveAs)
            {
                Console.WriteLine("Saving SetData File: {0}", LoadedFilePath);
                if (LoadedFilePath.ToLower().EndsWith(".orc"))
                {
                    SetData.Header.IsBigEndian = !chkbox_IsUltimate.Checked;

                    //Hacky solution for JumpBoard
                    if(!chkbox_IsUltimate.Checked)
                    {
                        foreach (var Obj in SetData.Objects) if (Obj.ObjectType == "JumpBoard")
                        {
                            if (Obj.Parameters[3].DataType == typeof(float))
                                    Obj.Parameters.RemoveAt(3);
                        }
                    }
                    else
                    {
                        foreach (var Obj in SetData.Objects) if (Obj.ObjectType == "JumpBoard")
                        {
                            if (Obj.Parameters[3].DataType != typeof(float))
                                Obj.Parameters.Insert(3, new SetObjectParam(typeof(float), 0f));
                        }
                    }

                    SetData.Save(LoadedFilePath, true);
                }
                else if (LoadedFilePath.ToLower().EndsWith(".set.xml"))
                {
                    // Load the Modifiers file
                    if (File.Exists("Templates/Colors/Modifiers-ColorsToGenerations.xml"))
                    {
                        var doc = XDocument.Load("Templates/Colors/Modifiers-ColorsToGenerations.xml");
                        var TemplatesGenerations = SetObjectType.LoadObjectTemplates(TemplatesPath, "Generations");

                        // Load path file
                        XDocument pathDoc = null;
                        var ofd = new OpenFileDialog()
                        {
                            Title = "Load Path file for " + Path.GetFileNameWithoutExtension(LoadedFilePath),
                            Filter = $"Sonic Generations Path Data|*.path.xml"
                        };
                        if (ofd.ShowDialog() == DialogResult.OK)
                            pathDoc = XDocument.Load(ofd.FileName);

                        ColorstoGensSetData = new ColorstoGensSetData();
                        ColorstoGensSetData.Name = SetData.Name;
                        ColorstoGensSetData.ObjectTemplates = TemplatesColors;
                        ColorstoGensSetData.TargetTemplates = TemplatesGenerations;

                        ColorstoGensSetData.GensExportXML(LoadedFilePath, SetData.Objects, doc, pathDoc);

                        MessageBox.Show("Complete");
                    }
                    else
                    {
                        MessageBox.Show("No config file found (Templates/Colors/Modifiers-ColorsToGenerations.xml)");
                    }
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
            setObject.CustomData.Add("Class", new SetObjectParam(typeof(ushort), (ushort)0));
            setObject.CustomData.Add("ParamArray", new SetObjectParam(typeof(uint), 0u));
            setObject.CustomData.Add("UnitArray", new SetObjectParam(typeof(uint), 0u));
            setObject.CustomData.Add("ID", new SetObjectParam(typeof(float), 0f));
            setObject.CustomData.Add("RangeIn" , new SetObjectParam(typeof(float), 1000f));
            setObject.CustomData.Add("RangeOut", new SetObjectParam(typeof(float), 1200f));
        }

        /// <summary>
        /// Builds the CPK (if CPKMaker.dll exists)
        /// </summary>
        /// <returns>INT: The Status</returns>
        public int BuildCPK()
        {
            // Checks if CPKMaker is enabled
            if (HasCPKMaker)
            {
                // Creates an instance of CPKMaker
                var cpkMaker = new CPKMaker();
                Console.Write("Building CPK... ");
                cpkMaker.BuildCPK(CPKDirectory);
                var status = new WaitCPKBuildForm(cpkMaker).ShowDialog();
                Console.WriteLine("Done.");
                if (status == DialogResult.Yes)
                    return 0;
                else if (status == DialogResult.Cancel)
                    return 2;
                else if (status == DialogResult.No)
                    return 3;

                return 1;
            }else
                return -1;
        }

        public void LaunchDolphin()
        {
            if (Config.DolphinExecutablePath.Length == 0)
            {
                Message(Resources.DolphinNotSetupText);
                MessageBox.Show(Resources.LocateDolphinText, ProgramName);
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

            if (Config.GameRootPath.Length == 0)
            {
                MessageBox.Show(ChangeEnglish(Resources.LocateSCExecutableText), ProgramName);
                var ofd = new OpenFileDialog()
                {
                    Title = "Locate boot.dol",
                    Filter = "Dolphin Executable|*.dol"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                    Config.GameRootPath = Path.GetDirectoryName(ofd.FileName);
                else
                    return;
                Config.SaveConfig("config.bin");
            }

            try
            {
                string dvdRootPath = Config.GameRootPath;
                string dolFilePath = Helpers.CombinePaths(dvdRootPath, "boot.dol");
                string apploaderPath = Helpers.CombinePaths(dvdRootPath, "apploader.img");

                if (!File.Exists(dolFilePath))
                {
                    MessageBox.Show(Resources.DolNotFoundText);
                    return;
                }

                // Dolphin Config
                string dolphinConfigPath = Helpers.CombinePaths(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Dolphin Emulator", "Config", "Dolphin.ini");
                var dolphinConfig = File.ReadAllLines(dolphinConfigPath);

                for (int i = 0; i < dolphinConfig.Length; ++i)
                {
                    if (dolphinConfig[i].StartsWith("DVDRoot"))
                        dolphinConfig[i] = $"DVDRoot = {dvdRootPath}";
                    if (dolphinConfig[i].StartsWith("Apploader"))
                        dolphinConfig[i] = $"Apploader = {apploaderPath}";
                }
                Console.WriteLine("Saving Dolphin Config");
                File.WriteAllLines(dolphinConfigPath, dolphinConfig);

                // Starts Dolphin
                var process = Process.Start(Config.DolphinExecutablePath, $"/b -e \"{dolFilePath}\"");
                Console.WriteLine("Starting Dolphin... [{0}]", process.StartInfo.Arguments);

                // Actively checks if Dolphin is running and if there is a "Warning" message box
                while (!process.HasExited)
                {
                    Enabled = false;
                    Thread.Sleep(1000);

                    var messageBoxHandle = FindWindow(null, "Warning");
                    if (messageBoxHandle == IntPtr.Zero)
                        continue;

                    var labelHandle = GetDlgItem(messageBoxHandle, 0xFFFF);
                    var sb = new StringBuilder(GetWindowTextLength(labelHandle));
                    // Gets the text from the message box
                    GetWindowText(labelHandle, sb, sb.Capacity);
                    // Checks if it contains "Unknown instruction", if found, then kill the process
                    if (sb.ToString().Contains("Unknown instruction"))
                    {
                        process.Kill();
                        Console.WriteLine(Resources.Unknown_InstructionFoundText);
                        Console.WriteLine("Returned: {0}", sb);
                        MessageBox.Show($"{Resources.Unknown_InstructionFoundText}\n{sb}",
                            ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                Console.WriteLine("Process is nolonger running");
                Enabled = true;
                Activate();
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show(Resources.DolNotFoundText);
                Config.DolphinExecutablePath = "";
                Config.SaveConfig("config.bin");
            }
            catch { }
        }

        public SetObjectTypeParam GetSetObjectTypeParam(string name)
        {
            var setObjectType = TemplatesColors[SelectedSetObject.ObjectType];
            var setObjectParams = setObjectType.Parameters;
            return setObjectParams.Find(t => name == t.Name);
        }

        // GUI Events
        #region ToolStripMenuItem

        private void EnableDarkTheme_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Theme.ApplyDarkThemeToAll(this);
        }

        private void StageNamesEditor_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new StageNameEditorForm().ShowDialog();
        }

        private void New_SOBJ_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SetData != null)
                new NewObjectForm(this).ShowDialog();
            UpdateObjects();
        }

        private void Duplicate_SOBJ_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                var sobj = new SetObject();
                sobj.ObjectID = NewObjectForm.GenerateID(SetData);
                sobj.ObjectType = SelectedSetObject.ObjectType;
                sobj.Transform = new SetObjectTransform();
                sobj.Transform.Position = SelectedSetObject.Transform.Position;
                sobj.Transform.Rotation = SelectedSetObject.Transform.Rotation;
                sobj.Transform.RotationV3 = SelectedSetObject.Transform.RotationV3;
                sobj.Transform.Scale = SelectedSetObject.Transform.Scale;
                
                // Copy Params
                foreach (var param in SelectedSetObject.Parameters)
                {
                    var newParam = new SetObjectParam();
                    newParam.Data = param.Data;
                    newParam.DataType = param.DataType;
                    sobj.Parameters.Add(newParam);
                }

                foreach (var param in SelectedSetObject.CustomData)
                {
                    var newParam = new SetObjectParam();
                    newParam.Data = param.Value.Data;
                    newParam.DataType = param.Value.DataType;
                    sobj.CustomData.Add(param.Key, newParam);
                }

                // Copy Transforms
                var transforms = new List<SetObjectTransform>();
                foreach (var transform in SelectedSetObject.Children)
                {
                    var newTransform = new SetObjectTransform();
                    newTransform.Position = transform.Position;
                    newTransform.Rotation = transform.Rotation;
                    newTransform.RotationV3 = transform.RotationV3;
                    newTransform.Scale = transform.Scale;
                    transforms.Add(newTransform);
                }
                sobj.Children = transforms.ToArray();

                SetData.Objects.Add(sobj);
                SelectSetObject(sobj);
                UpdateObjects();
            }
        }

        private void Delete_SOBJ_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedSetObject != null)
            {
                SetData.Objects.Remove(SelectedSetObject);
                SelectSetObject(null);
                UpdateObjects();
            }

        }

        private void RawParameterDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string raw = null;
            foreach (byte paramByte in SelectedSetObject.GetCustomDataValue<byte[]>("RawParamData"))
            {
                if (paramByte.ToString().Length < 2)
                    raw += "0" + paramByte.ToString("X") + " ";
                else
                    raw += paramByte.ToString("X") + " ";
            }
            if (raw != null)
            {
                MessageBox.Show(raw);
                Clipboard.SetText(raw);
            }
            else
            {
                MessageBox.Show("Raw data for this object has not been loaded");
            }
        }

        private void ReloadSetData_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Reloading SetData...");
            if (!string.IsNullOrEmpty(LoadedFilePath))
                OpenSetData(LoadedFilePath);
        }

        private void ReloadTemplates_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Loads the object templates
            ComboBox_ObjectType.Items.Clear();
            TemplatesColors = SetObjectType.LoadObjectTemplates(TemplatesPath, SonicColorsShortName);
            foreach (string objName in TemplatesColors.Keys)
                ComboBox_ObjectType.Items.Add(objName);
            Console.WriteLine("Loaded {0} Templates.", TemplatesColors.Count);
        }

        private void ReassignAllObjects_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SetData != null)
                for (int i = 0; i < SetData.Objects.Count; ++i)
                    SetData.Objects[i].ObjectID = (uint)i;
            UpdateObjects();
        }

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
            SaveSetData();
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSetData(true);
        }

        public void batchConvertToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog()
            {
                Title = "Open SetData",
                Filter = $"{GameName} SetData|*.orc|XML|*.xml",
                Multiselect = true
            };
            var sfd = new SaveFileDialog
            {
                FileName = "Choose output folder and output type",
                Filter = $"{GameName} SetData|*.orc|{GameName} Ultimate SetData|*.orc|Sonic Generations SetData|*.set.xml|XML|*.xml"
            };

            if ((ofd.ShowDialog() == DialogResult.OK)&&(sfd.ShowDialog() == DialogResult.OK))
            {
                string saveDirectory = Path.GetDirectoryName(sfd.FileName);
                string saveExtension = Path.GetExtension(sfd.FileName);
                int saveType = sfd.FilterIndex;
                
                foreach (var item in ofd.FileNames)
                {
                    string filePath = item;
                    OpenSetData(filePath);
                    if (saveType == 1) chkbox_IsUltimate.Checked = false;
                    if (saveType == 2) chkbox_IsUltimate.Checked = true;
                    LoadedFilePath = saveDirectory + "\\" + Path.GetFileNameWithoutExtension(filePath) + saveExtension;
                    SaveSetData(false);
                }
            }
            else
            {
                MessageBox.Show("Files or directory not chosen.", ProgramName, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            }
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
            BuildCPK();
            MessageBox.Show("Done.", ProgramName, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ToolStripMenuItem_SaveAndBuildCPK_Click(object sender, EventArgs e)
        {
            if (BuildCPK() == 0)
                MessageBox.Show("Done.", ProgramName, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                            MessageBox.Show(Resources.MultipleSetDataFoundText, ProgramName,
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
            if (BuildCPK() == 0)
                LaunchDolphin();
        }

        private void ToolStripMenuItem_LaunchSCWithoutSaving_Click(object sender, EventArgs e)
        {
            LaunchDolphin();
        }


        #endregion ToolStripMenuItem

        #region GUIEventsTransforms

        private void Button_TransformApply_Click(object sender, EventArgs e)
        {
            if (ListView_Objects.SelectedItems.Count != 0)
            {
                int transformIndex = (int)NumericUpDown_TransIndex.Value;
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

                objectTransform.RotationV3 = objectTransform.Rotation.ToEulerAngles(true);

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

        #endregion GUIEventsTransforms

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

        #region OtherGUIEvents
        private void ListView_Param_DoubleClick(object sender, EventArgs e)
        {
            if (ListView_Param.SelectedItems.Count != 0)
            {
                var lvi = ListView_Param.SelectedItems[0];
                var param = (SetObjectParam)lvi.Tag;

                new EditParamForm(param, GetSetObjectTypeParam(lvi.Text)).ShowDialog();
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
            if (e.IsSelected)
            {
                var lvi = ListView_Param.SelectedItems[0];
                ToolStrip_Label.Text = lvi.ToolTipText;
            }
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
        #endregion OtherGUIEvents

        // Pinvokes
        [DllImport("user32.dll")]
        public static extern IntPtr GetDlgItem(IntPtr dialogHandle, int controlID);

        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr Handle, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string WindowName);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr Handle);
    }
}
