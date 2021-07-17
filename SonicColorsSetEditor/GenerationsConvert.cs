using HedgeLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Numerics;

namespace HedgeLib.Sets
{
    public class ColorstoGensSetData : ColorsSetData
    {
        public Dictionary<string, SetObjectType> ObjectTemplates = null;
        public Dictionary<string, SetObjectType> TargetTemplates = null;
        Dictionary<string, string[]> RenameDict = null;
        Dictionary<string, string> ObjPhysDict = null;
        Dictionary<string, string> RemovalDict = null;
        Dictionary<string, Vector3> PositionOffsets = null;
        Dictionary<string, Vector3> RotationOffsets = null;
        Dictionary<string, List<ParamMods>> ParamMods = null;
        private int offsetIDs = 0;

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

                // Retrieve original template before any changes are made
                var sourcetemplate = ObjectTemplates?[obj.ObjectType];

                // Rename if applicable
                if (RenameDict.ContainsKey(obj.ObjectType))
                {
                    if (HandleParamModCond(RenameDict[obj.ObjectType][1], obj, sourcetemplate))
                        GensObjName = RenameDict[obj.ObjectType][0];
                }

                // Change to ObjectPhysics if necessary
                if (ObjPhysDict.ContainsKey(obj.ObjectType))
                    GensObjName = "ObjectPhysics";

                // Generate new element + template
                var objElem = new XElement(GensObjName);
                var template = new SetObjectType();

                // Apply modifiers
                // Backup original object
                var origobj = DeepCopy(obj);
                bool ignoreModifiers = false;
                if (!ignoreModifiers)
                {
                    List<ParamMods> objmodifiers = ParamMods.ContainsKey(GensObjName) ? ParamMods[GensObjName] : null;
                    List<ParamMods> generalmodifiers = ParamMods.ContainsKey("all") ? ParamMods["all"] : null;

                    // Deep copy template parameters
                    template.Name = sourcetemplate.Name;
                    template.Category = sourcetemplate.Category;
                    template.Extras = sourcetemplate.Extras;
                    template.Parameters = new List<SetObjectTypeParam>();
                    foreach (SetObjectTypeParam n in sourcetemplate.Parameters)
                    {
                        var p = new SetObjectTypeParam();
                        p.Name = String.Copy(n.Name);
                        p.DataType = n.DataType;
                        p.DefaultValue = n.DataType;
                        p.Description = n.Description;
                        p.Enums = n.Enums;

                        template.Parameters.Add(p);
                    }

                    // Add customdata to parameters
                    // RangeIn 
                    foreach (var customData in obj.CustomData)
                    {
                        if (customData.Key == "RangeIn" || customData.Key == "RangeOut")
                        {
                            var p = new SetObjectTypeParam();
                            var o = new SetObjectParam();

                            p.Name = String.Copy(customData.Key);
                            p.DataType = customData.Value.DataType;
                            o.DataType = customData.Value.DataType;
                            o.Data = customData.Value.Data;

                            template.Parameters.Add(p);
                            obj.Parameters.Add(o);
                        }
                    }

                    // Apply modifiers to parameters
                    for (int i = 0; i < obj.Parameters.Count; ++i)
                    {
                        string name = template?.Parameters[i].Name; // Store original parameter name in case of renames
                        SetObjectParam param = obj.Parameters[i];
                        var dataType = param.DataType;

                        List<ParamMods> modifiers = new List<ParamMods>();

                        if (objmodifiers != null)
                        {
                            foreach (var node in objmodifiers)
                            {
                                if (node.Name == name)
                                {
                                    modifiers.Add(node);
                                }
                            }
                        }

                        if (modifiers.Count > 0)
                        {
                            foreach (var mods in modifiers)
                            {
                                if (HandleParamModCond(mods.Condition, origobj, template))
                                {
                                    // Rename
                                    if (mods.Rename != null)
                                        template.Parameters[i].Name = mods.Rename;

                                    param = ApplyParamMods(param, mods, template.Parameters[i].Enums);
                                }
                            }
                        }
                        else if (generalmodifiers != null)
                        {
                            // Use "all" parameter mods only if no mods for this specific object's parameter exist
                            // todo: more mods
                            foreach (var mods in generalmodifiers)
                            {
                                if (name.Contains(mods.Name))
                                {
                                    if (HandleParamModCond(mods.Condition, origobj, template))
                                    {
                                        // Rename
                                        if (mods.Rename != null)
                                            template.Parameters[i].Name = mods.Rename;

                                        param = ApplyParamMods(param, mods, template.Parameters[i].Enums);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    template = sourcetemplate;
                }

                var targetTemplate = TargetTemplates.ContainsKey(GensObjName) ? TargetTemplates?[GensObjName] : template;

                for (int i = 0; i < targetTemplate.Parameters.Count; ++i)
                {
                    string name = targetTemplate?.Parameters[i].Name;

                    Predicate<SetObjectTypeParam> nameCheck = (SetObjectTypeParam p) => { return p.Name == name; };

                    int paramIndex = template.Parameters.FindIndex(nameCheck);

                    if (paramIndex >= 0)
                        objElem.Add(GenerateParamElementGens(obj.Parameters[paramIndex], name));
                    else
                    {
                        if (GensObjName == "ObjectPhysics" && name == "Type")
                        {
                            //ObjectPhysics type name
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

                // Turn BoxNums into multiset objects
                if (obj.ObjectType == "IronBox")
                {
                    // Define length of things
                    var childnum = obj.Children.Length;
                    uint BoxNumX = (uint)obj.Parameters[0].Data;
                    uint BoxNumY = (uint)obj.Parameters[1].Data;
                    uint BoxNumZ = (uint)obj.Parameters[2].Data;
                    var totalchildren = (BoxNumX * BoxNumY * BoxNumZ * (childnum + 1)) - 1;

                    int curr = childnum;

                    if (totalchildren > childnum)
                    {
                        var largerChildrenArray = new SetObjectTransform[totalchildren];
                        obj.Children.CopyTo(largerChildrenArray, 0);

                        // Loop through existing children
                        SetObjectTransform[] workingtransforms = new SetObjectTransform[obj.Children.Length + 1];
                        workingtransforms[0] = obj.Transform;
                        obj.Children.CopyTo(workingtransforms, 1);

                        foreach (SetObjectTransform transform in workingtransforms)
                        {
                            bool first = true;
                            for (int x = 0; x < BoxNumX; ++x)
                            {
                                for (int y = 0; y < BoxNumY; ++y)
                                {
                                    for (int z = 0; z < BoxNumZ; ++z)
                                    {
                                        if (first)
                                        {
                                            first = false;
                                            continue;
                                        }
                                        SetObjectTransform child = new SetObjectTransform();
                                        child.Position.X = transform.Position.X;
                                        child.Position.Y = transform.Position.Y;
                                        child.Position.Z = transform.Position.Z;
                                        child.Rotation = transform.Rotation;

                                        child.Position = OffsetPosition(child, new Vector3(x * 20, y * 20, z * 20));

                                        largerChildrenArray[curr] = child;
                                        curr++;
                                    }
                                }
                            }
                        }
                        obj.Children = largerChildrenArray;
                    }
                }

                if (obj.ObjectType == "MapPartsBox")
                {
                    // Define length of things
                    var childnum = obj.Children.Length;
                    uint modelType = (byte)obj.Parameters[2].Data;
                    uint BoxNumX = (byte)obj.Parameters[3].Data;
                    uint BoxNumY = (byte)obj.Parameters[4].Data;
                    uint BoxNumZ = (byte)obj.Parameters[5].Data;

                    // Get size of actual box model
                    uint boxTypeSize = 1;
                    if (modelType == 1)
                        boxTypeSize = 2;
                    else if (modelType == 2)
                        boxTypeSize = 4;

                    // Get needed number of boxes
                    BoxNumX = (uint)System.Math.Ceiling((float)BoxNumX / boxTypeSize);
                    BoxNumY = (uint)System.Math.Ceiling((float)BoxNumY / boxTypeSize);
                    BoxNumZ = (uint)System.Math.Ceiling((float)BoxNumZ / boxTypeSize);

                    var totalchildren = (BoxNumX * BoxNumY * BoxNumZ * (childnum + 1)) - 1;

                    int curr = childnum;

                    if (totalchildren > childnum)
                    {
                        var largerChildrenArray = new SetObjectTransform[totalchildren];
                        obj.Children.CopyTo(largerChildrenArray, 0);

                        // Loop through existing children
                        SetObjectTransform[] workingtransforms = new SetObjectTransform[obj.Children.Length + 1];
                        workingtransforms[0] = obj.Transform;
                        obj.Children.CopyTo(workingtransforms, 1);

                        foreach (SetObjectTransform transform in workingtransforms)
                        {
                            float xOffset = (((byte)obj.Parameters[3].Data) / 2) - (boxTypeSize / 2);

                            float zOffset = 0;
                            if (boxTypeSize > 1)
                                zOffset = boxTypeSize / 2;

                            transform.Position = OffsetPosition(transform, new Vector3(-xOffset * 10, 0, -zOffset * 10));

                            bool first = true;
                            for (int x = 0; x < BoxNumX; ++x)
                            {
                                for (int y = 0; y < BoxNumY; ++y)
                                {
                                    for (int z = 0; z < BoxNumZ; ++z)
                                    {
                                        if (first)
                                        {
                                            first = false;
                                            continue;
                                        }
                                        SetObjectTransform child = new SetObjectTransform();
                                        child.Position.X = transform.Position.X;
                                        child.Position.Y = transform.Position.Y;
                                        child.Position.Z = transform.Position.Z;
                                        child.Rotation = transform.Rotation;

                                        child.Position = OffsetPosition(child, new Vector3(x * (boxTypeSize * 10), y * (boxTypeSize * 10), z * (boxTypeSize * -10)));

                                        largerChildrenArray[curr] = child;
                                        curr++;
                                    }
                                }
                            }
                        }
                        obj.Children = largerChildrenArray;
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
                var objIDAttr = new XElement("SetObjectID", obj.ObjectID + offsetIDs);
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
            bool HandleParamModCond(string condition, SetObject obj, SetObjectType template)
            {
                // Return success if no condition set
                if (condition == null)
                    return true;

                bool result = false;

                char separatorType = ';';

                // : equal, ! not equal, < lesser, > greater
                foreach (char type in new List<char> { ':','!','<','>' })
                {
                    int test = condition.IndexOf(type);
                    if (test >= 0)
                    {
                        separatorType = type;
                        break;
                    }
                }

                if (separatorType == ';')
                {
                    Console.Write("Invalid condition type key / condition type key not found.");
                    return true;
                }

                int separatorIndex = condition.IndexOf(separatorType);
                string key = condition.Substring(0, separatorIndex);
                string value = condition.Substring(separatorIndex + 1, condition.Length - (separatorIndex + 1));

                if (key == "OriginalName")
                {
                    if (separatorType == ':')
                    {
                        if (value == template.Name)
                            result = true;
                    }
                    else if (separatorType == '!')
                    {
                        if (value != template.Name)
                            result = true;
                    }
                    else
                    {
                        Console.Write("Invalid type key for this condition. (" + condition + ")");
                        return true;
                    }
                }
                else
                {
                    for (int i = 0; i < template.Parameters.Count; ++i)
                    {
                        if (key == template.Parameters[i].Name)
                        {
                            if (separatorType == ':')
                            {
                                if (value == obj.Parameters[i].Data.ToString())
                                    result = true;
                            }
                            else if (separatorType == '!')
                            {
                                if (value != obj.Parameters[i].Data.ToString())
                                    result = true;
                            }
                            else
                            {
                                Single compvalue;

                                if (Single.TryParse(value, out compvalue))
                                {
                                    if (obj.Parameters[i].DataType == typeof(byte) ||
                                        obj.Parameters[i].DataType == typeof(sbyte) ||
                                        obj.Parameters[i].DataType == typeof(short) ||
                                        obj.Parameters[i].DataType == typeof(ushort) ||
                                        obj.Parameters[i].DataType == typeof(int) || 
                                        obj.Parameters[i].DataType == typeof(uint) || 
                                        obj.Parameters[i].DataType == typeof(float) ||
                                        obj.Parameters[i].DataType == typeof(double))
                                    {
                                        var paramvalue = float.Parse(obj.Parameters[i].Data.ToString());

                                        if (separatorType == '<')
                                        {
                                            if (paramvalue < compvalue)
                                                result = true;
                                        }
                                        else if (separatorType == '>')
                                        {
                                            if (paramvalue > compvalue)
                                                result = true;
                                        }
                                    }
                                    else
                                    {
                                        Console.Write("Invalid type key for this datatype. (" + obj.Parameters[i].DataType + ")");
                                        return true;
                                    }
                                }
                                else
                                {
                                    Console.Write("Invalid value for this condition. (" + condition + ")");
                                    return true;
                                }
                            }
                        }
                    }
                }
                return result;
            }

            SetObjectParam ApplyParamMods(SetObjectParam param, ParamMods mods, List<SetObjectTypeParamEnum> enums)
            {
                var dataType = param.DataType;

                // Override
                if (mods.ValueOverride != null)
                {
                    param.DataType = typeof(string);
                    param.Data = mods.ValueOverride;
                }

                // Factor/multiplication
                if (mods.Factor != 1)
                {
                    if (dataType == typeof(float))
                    {
                        float temp = (float)param.Data;
                        temp = temp * mods.Factor;
                        param.Data = temp;
                    }
                    else if (dataType == typeof(double))
                    {
                        double temp = (double)param.Data;
                        temp = temp * mods.Factor;
                        param.Data = temp;
                    }
                    else if (dataType == typeof(Vector3))
                    {
                        Vector3 temp = (Vector3)param.Data;
                        temp = temp * mods.Factor;
                        param.Data = temp;
                    }
                    else
                    {
                        Console.Write("Invalid type key for this datatype. (" + dataType + ")");
                    }
                }

                // Offset
                if (mods.Offset != 0)
                {
                    if (dataType == typeof(byte))
                    {
                        int temp = (byte)param.Data;
                        temp = temp + (int)mods.Offset;
                        param.Data = (byte)temp;
                    }
                    else if (dataType == typeof(sbyte))
                    {
                        int temp = (sbyte)param.Data;
                        temp = temp + (int)mods.Offset;
                        param.Data = (sbyte)temp;
                    }
                    else if (dataType == typeof(short))
                    {
                        int temp = (short)param.Data;
                        temp = temp + (int)mods.Offset;
                        param.Data = (short)temp;
                    }
                    else if (dataType == typeof(ushort))
                    {
                        int temp = (ushort)param.Data;
                        temp = temp + (int)mods.Offset;
                        param.Data = (ushort)temp;
                    }
                    else if (dataType == typeof(int))
                    {
                        int temp = (int)param.Data;
                        temp = temp + (int)mods.Offset;
                        param.Data = temp;
                    }
                    else if (dataType == typeof(uint))
                    {
                        uint temp = (uint)param.Data;
                        temp = temp + (uint)mods.Offset;
                        param.Data = temp;
                    }
                    else if (dataType == typeof(float))
                    {
                        var temp = (float)param.Data;
                        temp = temp + mods.Offset;
                        param.Data = temp;
                    }
                    else if (dataType == typeof(double))
                    {
                        var temp = (double)param.Data;
                        temp = temp + mods.Offset;
                        param.Data = temp;
                    }
                    else
                    {
                        Console.Write("Invalid type key for this datatype. (" + dataType + ")");
                    }
                }

                // Invert boolean
                if (mods.BoolFlip)
                {
                    if (dataType == typeof(bool))
                    {
                        bool temp = (bool)param.Data;
                        temp = !temp;
                        param.Data = temp;
                    }
                    else
                    {
                        Console.Write("Invalid type key for this datatype. (" + dataType + ")");
                    }
                }

                // Swap to enum string value
                if (mods.EnumString)
                {
                    if (dataType == typeof(byte) ||
                        dataType == typeof(sbyte) ||
                        dataType == typeof(short) ||
                        dataType == typeof(ushort) ||
                        dataType == typeof(int) ||
                        dataType == typeof(uint))
                    {
                        var temp = param.Data.ToString();
                        object value = temp;
                        foreach (var item in enums)
                        {
                            if (item.Value.ToString() == temp)
                            {
                                value = item.Description;
                            }
                        }
                        param.DataType = typeof(string);
                        param.Data = value;
                    }
                    else
                    {
                        Console.Write("Invalid type key for this datatype. (" + dataType + ")");
                    }
                }

                return param;
            }

            XElement GenerateParamElementGens(
                SetObjectParam param, string name)
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
                    elem.AddElem((Vector3)param.Data);
                }
                else if (dataType == typeof(Vector4) || dataType == typeof(Quaternion))
                {
                    elem.AddElem((Vector4)param.Data);
                }
                else if (dataType == typeof(Single))
                {
                    float singleValue = (float)param.Data;
                    elem.Value = singleValue.ToString("0.#######");// Prevent scientific notation

                }
                else if (param.DataType == typeof(Boolean))
                {
                    // Boolean caps
                    elem.Value = param.Data.ToString().ToLowerInvariant();
                }
                else if (new string[] { "Target", "ACameraID", "BCameraID", "ALinkObjID", "BLinkObjID" }.Contains(name))
                {
                    // Workaround for target obj id parameters
                    // Offset if necessary
                    int temp = (int)(uint)param.Data;
                    temp += offsetIDs;

                    var targetIDAttr = new XElement("SetObjectID", temp.ToString());
                    elem.Add(targetIDAttr);
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

                // Scaling
                transform.Position = transform.Position * 0.1f;

                // Offset
                transform.Position = OffsetPosition(transform, positionOffset);

                // Add elements to new position element and return it.
                posElem.AddElem(transform.Position);
                return new XElement(posElem);
            }

            XElement GenerateRotationElement(
                SetObjectTransform transform, string name = "Transform", Vector3 rotationOffset = new Vector3())
            {
                // Convert Rotation into elements.
                var rotElem = new XElement("Rotation");

                // Rotate objects that need it
                transform.Rotation = OffsetRotation(transform.Rotation, rotationOffset);

                // Add elements to new rotation element and return it.
                rotElem.AddElem(transform.Rotation);
                return new XElement(rotElem);
            }

            Vector3 OffsetPosition(SetObjectTransform transform, Vector3 positionOffset)
            {
                var magnitude = new System.Numerics.Vector3(positionOffset.X, positionOffset.Y, positionOffset.Z);
                var quaternion = new System.Numerics.Quaternion(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W);
                magnitude = System.Numerics.Vector3.Transform(magnitude, quaternion);
                positionOffset = new Vector3(magnitude.X, magnitude.Y, magnitude.Z);

                transform.Position += positionOffset;

                return transform.Position;
            }

            Quaternion OffsetRotation(Quaternion rotation, Vector3 rotationOffset)
            {
                //todo: implement properly instead of being limited by 90 degrees
                if (rotationOffset.X != 0 || rotationOffset.Y != 0)
                {
                    var temp = rotation.ToEulerAngles();
                    // X
                    if (rotationOffset.X != 0)
                    {

                        if ((temp.Y == 0) && (temp.Z == 0))
                        {
                            temp.X = temp.X + rotationOffset.X;
                            rotation = new Quaternion(temp);
                        }
                        else if ((temp.Y == 0) && (System.Math.Abs(rotationOffset.X) == 90))
                        {
                            temp.X = -90 + System.Math.Abs(temp.Z);
                            temp.Y = rotationOffset.X * -1;
                            temp.Z = -90;
                            // This is necessary since conversion between
                            // Vector 3 and Quaternion is wonky
                            var Rotation = new Quaternion(temp);
                            float temptemp = Rotation.Y;
                            Rotation.Y = Rotation.W;
                            Rotation.W = temptemp;
                            rotation = new Quaternion(Rotation);
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
                        rotation = new Quaternion(temp);
                    }
                }

                return rotation;
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

            RenameDict = renameNodes.ToDictionary(n => n.Attribute("Value").Value, n => new string[]{
                n.Name.ToString(),
                n.Attribute("Condition") == null ? null : n.Attribute("Condition").Value
            }); // Invert to get around xml limitations

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
