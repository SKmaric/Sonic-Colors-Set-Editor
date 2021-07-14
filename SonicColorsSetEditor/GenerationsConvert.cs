using HedgeLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace HedgeLib.Sets
{
    public class ColorstoGensSetData : ColorsSetData
    {
        public Dictionary<string, SetObjectType> ObjectTemplates = null;
        public Dictionary<string, SetObjectType> TargetTemplates = null;
        Dictionary<string, string> RenameDict = null;
        Dictionary<string, string> ObjPhysDict = null;
        Dictionary<string, string> RemovalDict = null;
        Dictionary<string, Vector3> PositionOffsets = null;
        Dictionary<string, Vector3> RotationOffsets = null;
        Dictionary<string, List<ParamMods>> ParamMods = null;

        public void GensExportXML(string filePath, List<SetObject> sourceObjects, XDocument doc)
        {
            Objects = new List<SetObject>();
            Objects = DeepCopy(sourceObjects);

            LoadExportConfig(doc);

            using (var fileStream = File.OpenWrite(filePath))
            {
                GensExportXML(fileStream);
            }
        }

        public void GensExportXML(Stream fileStream)
        {
            // Convert to XML file and save
            var rootElem = new XElement("SetObject");

            foreach (var obj in Objects)
            {
                // Skip objects set to be removed
                if (RemovalDict.ContainsKey(obj.ObjectType)) continue;

                // Skip objects with no template.
                if (!ObjectTemplates.ContainsKey(obj.ObjectType)) continue;

                // Generate Object Element
                string GensObjName = obj.ObjectType;
                // Rename if applicable
                foreach (var node in RenameDict)
                {
                    if (obj.ObjectType == node.Key)
                    {
                        GensObjName = node.Value;
                    }
                }

                // Change to ObjectPhysics if necessary
                foreach (var node in ObjPhysDict)
                {
                    if (obj.ObjectType == node.Key)
                    {
                        //var param = new SetObjectParam();
                        //param.DataType = typeof(string);
                        //param.Data = obj.ObjectType;
                        //objElem.Add(GenerateParamElementGens(param, "Type"));
                        GensObjName = "ObjectPhysics";
                    }
                }

                var objElem = new XElement(GensObjName);

                // Generate CustomData Element
                // Messy use RangeOut value as Range value.
                foreach (var customData in obj.CustomData)
                {
                    // Experimental - use RangeIn as Range for SoundPoint
                    if (customData.Key == "RangeIn")
                    {
                        if (obj.ObjectType == "EnvSound")
                        {
                            objElem.Add(GenerateParamElementGens(
                                customData.Value, "Range"));
                        }
                    }
                    else if (customData.Key == "RangeOut")
                    {
                        if (obj.ObjectType == "EnvSound")
                        {
                            objElem.Add(GenerateParamElementGens(
                                customData.Value, "Radius"));
                        }
                        else
                        {
                            objElem.Add(GenerateParamElementGens(
                                customData.Value, "Range"));
                        }
                    }
                }

                // Generate Parameters Element
                var template = ObjectTemplates?[obj.ObjectType];
                var targetTemplate = TargetTemplates?[GensObjName];

                for (int i = 0; i < targetTemplate.Parameters.Count; ++i)
                {
                    string name = targetTemplate?.Parameters[i].Name;

                    Predicate<SetObjectTypeParam> nameCheck = (SetObjectTypeParam p) => { return p.Name == name; };

                    int paramIndex = template.Parameters.FindIndex(nameCheck);

                    if (paramIndex >= 0)
                        objElem.Add(GenerateParamElementGens(obj.Parameters[paramIndex], name, false,
                            ParamMods.ContainsKey(obj.ObjectType) ? ParamMods[obj.ObjectType] : null ));
                    else
                    {
                        if (GensObjName == "ObjectPhysics" && name == "Type")
                        {
                            //default value
                            var param = new SetObjectParam();
                            param.DataType = typeof(string);
                            param.Data = obj.ObjectType.ToString();
                            objElem.Add(GenerateParamElementGens(param, name));
                        }
                        else
                        {
                            //default value
                            var param = new SetObjectParam();
                            param.DataType = targetTemplate?.Parameters[i].DataType;
                            param.Data = targetTemplate?.Parameters[i].DefaultValue;
                            objElem.Add(GenerateParamElementGens(param, name));
                        }
                    }
                }

                // Generate Transforms Elements
                // Apply position to objects that need it
                Vector3 PositionOffset = new Vector3();
                if (PositionOffsets.ContainsKey(obj.ObjectType))
                {
                    PositionOffset = PositionOffsets[obj.ObjectType];
                }
                objElem.Add(GeneratePositionElement(obj.Transform, obj.ObjectType, PositionOffset));

                // Apply rotation to objects that need it
                Vector3 RotationOffset = new Vector3();
                if (RotationOffsets.ContainsKey(obj.ObjectType))
                {
                    RotationOffset = RotationOffsets[obj.ObjectType];
                }
                objElem.Add(GenerateRotationElement(obj.Transform, obj.ObjectType, RotationOffset));

                // Generate ID Element
                var objIDAttr = new XElement("SetObjectID", obj.ObjectID);
                objElem.Add(objIDAttr);

                // Generate MultiSet Elements
                if (obj.Children.Length > 0)
                {
                    var multiElem = new XElement("MultiSetParam");

                    for (int i = 0; i < obj.Children.Length; ++i)
                    {
                        var childElem = new XElement("Element");
                        childElem.Add(new XElement("Index", i + 1));
                        childElem.Add(GeneratePositionElement(obj.Children[i], obj.ObjectType, 
                            PositionOffset));
                        childElem.Add(GenerateRotationElement(obj.Children[i], obj.ObjectType,
                            RotationOffset));
                        multiElem.Add(childElem);
                    }
                    multiElem.Add(new XElement("BaseLine", 1));
                    multiElem.Add(new XElement("Direction", 0));
                    multiElem.Add(new XElement("Interval", 1));
                    multiElem.Add(new XElement("IntervalBase", 0));
                    multiElem.Add(new XElement("PositionBase", 0));
                    multiElem.Add(new XElement("RotationBase", 0));
                    objElem.Add(multiElem);
                }

                //Sort element
                var sortedObjElem = new XElement(objElem.Name,
                    from el in objElem.Elements()
                    orderby el.Name.ToString()
                    select el);

                // Add all of this to the XDocument
                rootElem.Add(sortedObjElem);
            }

            var xml = new XDocument(rootElem);
            xml.Save(fileStream);

            // Sub-Methods
            XElement GenerateParamElementGens(
                SetObjectParam param, string name, bool ignoreMods = true, List<ParamMods> modifiers = null)
            {
                var dataType = param.DataType;
                var elem = new XElement((string.IsNullOrEmpty(name)) ?
                    "Parameter" : name);

                if (dataType == typeof(string))
                {
                    elem.Value = param.Data.ToString();
                }
                else if (dataType == typeof(Vector3))
                {
                    // Scale
                    var tempVector3 = new Vector3();
                    tempVector3 = (Vector3)param.Data;
                    tempVector3.X = (tempVector3.X * 0.1f);
                    tempVector3.Y = (tempVector3.Y * 0.1f);
                    tempVector3.Z = (tempVector3.Z * 0.1f);
                    param.Data = tempVector3;

                    Helpers.XMLWriteVector3(elem, (Vector3)param.Data);
                }
                else if (dataType == typeof(Vector4) || dataType == typeof(Quaternion))
                {
                    Helpers.XMLWriteVector4(elem, (Vector4)param.Data);
                }
                else if (dataType == typeof(Single))
                {
                    var singleValue = new Single();
                    singleValue = float.Parse(param.Data.ToString());

                    modifiers = ParamMods["all"];

                    // Parameter scaling
                    if (modifiers != null && ignoreMods != true)
                    {
                        foreach (var node in modifiers)
                        {
                            if (name.Contains(node.Name))
                            {
                                singleValue = singleValue * node.Factor;
                                break;
                            }
                        }
                    }

                    if (System.Math.Abs(singleValue) < 1)
                    {
                        elem.Value = singleValue.ToString("0.########################"); 
                        // Prevent scientific notation
                    }
                    else
                    {
                        elem.Value = singleValue.ToString(
                            "#################################.########################"); 
                        // Prevent scientific notation
                    }
                }
                else if ((name == "ACameraID") || (name == "BCameraID") 
                    || (name == "ALinkObjID") || (name == "BLinkObjID"))
                {
                    var targetIDAttr = new XElement("SetObjectID", param.Data.ToString());
                    elem.Add(targetIDAttr);
                }
                else if(param.DataType == typeof(Boolean))
                {
                    // Boolean caps
                    elem.Value = param.Data.ToString();
                    if (param.Data.ToString() == "True")
                    {
                        elem.Value = "true";
                    }
                    else if (param.Data.ToString() == "False")
                    {
                        elem.Value = "false";
                    }
                }
                else if (param.Data != null)
                {
                    elem.Value = param.Data.ToString();
                }

                return elem;
            }

            XElement GeneratePositionElement(
                SetObjectTransform transform, string name = "Transform", Vector3 positionOffset = new Vector3())
            {
                // Convert Position into elements.
                var posElem = new XElement("Position");

                //Scaling
                transform.Position = transform.Position * 0.1f;

                //Offset
                transform.Position += positionOffset;

                Helpers.XMLWriteVector3(posElem, transform.Position);

                // Add elements to new position element and return it.
                return new XElement(posElem);
            }

            XElement GenerateRotationElement(
                SetObjectTransform transform, string name = "Transform", Vector3 rotationOffset = new Vector3())
            {
                // Convert Rotation into elements.
                var rotElem = new XElement("Rotation");

                // Rotate objects that need it
                if (rotationOffset.X != 0 || rotationOffset.Y != 0)
                {
                    var temp = transform.Rotation.ToEulerAngles();
                    // X
                    if (rotationOffset.X != 0)
                    {
                        
                        if ((temp.Y == 0) && (temp.Z == 0))
                        {
                            temp.X = temp.X + rotationOffset.X;
                            transform.Rotation = new Quaternion(temp);
                        }
                        else if ((temp.Y == 0) && (System.Math.Abs(rotationOffset.X) == 90))
                        {
                            temp.X = -90 + System.Math.Abs(temp.Z);
                            temp.Y = rotationOffset.X * -1;
                            temp.Z = -90;
                            Console.WriteLine("X rotation");
                            // This is necessary since conversion between
                            // Vector 3 and Quaternion is wonky
                            var Rotation = new Quaternion(temp);
                            float temptemp = Rotation.Y;
                            Rotation.Y = Rotation.W;
                            Rotation.W = temptemp;
                            transform.Rotation = new Quaternion(Rotation);
                        }
                        else Console.WriteLine("Unsupported X rotation modification detected");

                    }
                    // Y
                    if (rotationOffset.Y != 0)
                    {
                        temp.Y = temp.Y + rotationOffset.Y;
                        if ((rotationOffset.Y == 180) || (rotationOffset.Y == -180))
                        {
                            temp.X = temp.X * -1;
                        }
                        else if ((rotationOffset.Y == 90) || (rotationOffset.Y == -90))
                        {
                            float temptemp = temp.X;
                            temp.X = temp.Z;
                            temp.Z = temptemp;

                            if (rotationOffset.Y == 90)
                            {
                                temp.X = temp.X * -1;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Y Rotation currently only supports 90, 180 or -90 degrees on Y axis.");
                        }
                        transform.Rotation = new Quaternion(temp);
                    }
                }

                Helpers.XMLWriteVector4(rotElem, transform.Rotation);

                // Add elements to new rotation element and return it.
                return new XElement(rotElem);
            }
        }

        private void LoadExportConfig(XDocument doc)
        {
            var renameNodes = doc.Root.Element("Rename").Nodes().OfType<XElement>();
            var objPhysNodes = doc.Root.Element("MakeObjectPhysics").Nodes().OfType<XElement>();
            var removalNodes = doc.Root.Element("RemoveObject").Nodes().OfType<XElement>();
            var positionNodes = doc.Root.Element("PositionOffset").Nodes().OfType<XElement>();
            var rotationNodes = doc.Root.Element("RotationOffset").Nodes().OfType<XElement>();
            var paramNodes = doc.Root.Element("ParamModify").Nodes().OfType<XElement>();

            RenameDict = renameNodes.ToDictionary(n => n.Attribute("Value").Value, n => n.Name.ToString());
            ObjPhysDict = objPhysNodes.ToDictionary(n => n.Name.ToString(), n => n.Value);
            RemovalDict = removalNodes.ToDictionary(n => n.Name.ToString(), n => n.Value);
            PositionOffsets = positionNodes.ToDictionary(n => n.Name.ToString(), n => new Vector3(
                float.Parse(n.Attribute("X").Value),
                float.Parse(n.Attribute("Y").Value),
                float.Parse(n.Attribute("Z").Value)
                ));

            RotationOffsets = rotationNodes.ToDictionary(n => n.Name.ToString(), n => new Vector3(
                float.Parse(n.Attribute("X").Value),
                float.Parse(n.Attribute("Y").Value),
                float.Parse(n.Attribute("Z").Value)
                ));
            //ParamMods = paramNodes.ToDictionary(n => n.Name.ToString(), n => n.Attribute("Value").Value);

            ParamMods = new Dictionary<string, List<ParamMods>>();
            foreach (var item in paramNodes)
            {
                var parameters = new List<ParamMods>();
                foreach (var param in item.Elements())
                {
                    parameters.Add(new ParamMods(param.Name.ToString(),
                        param.Attribute("Rename") == null ? null : param.Attribute("Rename").Value,
                        param.Attribute("Override") == null ? null : param.Attribute("Override").Value,
                        param.Attribute("Factor") == null ? 1 : float.Parse(param.Attribute("Factor").Value),
                        param.Attribute("Offset") == null ? 0 : float.Parse(param.Attribute("Offset").Value),
                        param.Attribute("BoolFlip") == null ? false : bool.Parse(param.Attribute("BoolFlip").Value),
                        param.Attribute("EnumString") == null ? false : bool.Parse(param.Attribute("EnumString").Value),
                        param.Attribute("Condition") == null ? null : param.Attribute("Condition").Value
                        ));
                }
                ParamMods.Add(item.Name.ToString(), parameters);
            }
        }

        public static T DeepCopy<T>(T item)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, item);
            stream.Seek(0, SeekOrigin.Begin);
            T result = (T)formatter.Deserialize(stream);
            stream.Close();
            return result;
        }
    }

    public class ParamMods
    {
        // Variables/Constants
        public string Name;
        public string Rename;
        public string ValueOverride;
        public float Factor;
        public float Offset;
        public bool BoolFlip;
        public bool EnumString;
        public string Condition;

        public ParamMods(string name, string rename, string valueoverride, float factor,
            float offset, bool boolflip, bool enumstring, string condition)
        {
            Name = name;
            Rename = rename;
            ValueOverride = valueoverride;
            Factor = factor;
            Offset = offset;
            BoolFlip = boolflip;
            EnumString = enumstring;
            Condition = condition;
        }
    }
}
