using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.Util.Encoding;
using SDG.Framework.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DevkitServer.Multiplayer.Actions;
[EarlyTypeInit]
internal static class EditorActionsCodeGeneration
{
    internal static bool Init;
    internal static Dictionary<ActionType, ActionAttribute> Attributes = new Dictionary<ActionType, ActionAttribute>(32);
    internal static HandleWriteSettings? OnWritingAction;
    internal static HandleReadSettings? OnReadingAction;
    internal static HandleCreateNewAction? CreateAction;
    internal static HandleByteWriteSettings? WriteSettingsCollection;
    internal static HandleByteReadSettings? ReadSettingsCollection;
    internal static HandleAppendSettingsCollection? AppendSettingsCollection;
    static EditorActionsCodeGeneration()
    {
        List<Type> types = Accessor.GetTypesSafe(removeIgnored: true);
        int c = 0;
        List<(Type type, ActionSettingAttribute attr, List<PropertyInfo> info)> properties = new List<(Type, ActionSettingAttribute, List<PropertyInfo>)>(48);
        List<(Type type, ActionAttribute attr)> actions = new List<(Type, ActionAttribute)>(32);
        for (int i = 0; i < types.Count; ++i)
        {
            Type t = types[i];
            if (!t.IsInterface)
            {
                if (!t.IsAbstract && typeof(IAction).IsAssignableFrom(t))
                {
                    Attribute[] attrs = Attribute.GetCustomAttributes(t, typeof(ActionAttribute), false);
                    if (attrs.Length > 0)
                    {
                        foreach (ActionAttribute actionAttr in attrs.OfType<ActionAttribute>())
                        {
                            actionAttr.Type = t;
                            if (Attributes.TryGetValue(actionAttr.ActionType, out ActionAttribute attribute))
                            {
                                Logger.LogWarning($"[EDITOR ACTIONS] Duplicate action attribute for type ignored: {actionAttr.ActionType.Format()}, {t.Format()} already overridden by {attribute.Type.Format()}.");
                            }
                            else
                            {
                                Attributes[actionAttr.ActionType] = actionAttr;
                                actions.Add((t, actionAttr));
                            }
                        }
                    }
                }
                continue;
            }
            if (Attribute.GetCustomAttribute(t, typeof(ActionSettingAttribute), false) is ActionSettingAttribute settingAttr)
            {
                PropertyInfo[] props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                properties.Add((t, settingAttr, new List<PropertyInfo>(props)));
            }
        }

        const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
        Logger.LogDebug($"[EDITOR ACTIONS] Found {actions.Count.Format()} action types.");
        Logger.LogDebug($"[EDITOR ACTIONS] Found {properties.Count.Format()} setting interfaces with {c.Format()} total properties.");
        
        MethodInfo? getActionSettings = typeof(IActionListener).GetProperty(nameof(IActionListener.Settings), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
        if (getActionSettings == null)
        {
            Logger.LogWarning($"Failed to find {typeof(IActionListener).Format()}.Settings getter.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }

        MethodInfo? getSettings = typeof(ActionSettings).GetMethod(nameof(ActionSettings.GetSettings), BindingFlags.Instance | BindingFlags.Public);
        if (getSettings == null)
        {
            Logger.LogWarning($"Failed to find {typeof(ActionSettings).Format()}.GetSettings method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        MethodInfo? setSettings = typeof(ActionSettings).GetMethod(nameof(ActionSettings.SetSettings), BindingFlags.Instance | BindingFlags.NonPublic);
        if (setSettings == null)
        {
            Logger.LogWarning($"Failed to find {typeof(ActionSettings).Format()}.SetSettings method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        MethodInfo? nearlyEqual = typeof(MathfEx).GetMethod(nameof(MathfEx.IsNearlyEqual), BindingFlags.Static | BindingFlags.Public,
            null, CallingConventions.Any, new Type[] { typeof(float), typeof(float), typeof(float) }, null);
        if (nearlyEqual == null)
        {
            Logger.LogWarning($"Failed to find {typeof(MathfEx).Format()}.IsNearlyEqual method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        MethodInfo? objEquals = typeof(object).GetMethod(nameof(Equals), BindingFlags.Instance | BindingFlags.Public);
        if (objEquals == null)
        {
            Logger.LogWarning($"Failed to find {typeof(object).Format()}.Equals method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }

        MethodInfo? claimFromPool = typeof(Pool<ActionSettingsCollection>).GetMethod(
            nameof(Pool<ActionSettingsCollection>.claim), BindingFlags.Instance | BindingFlags.Public, null,
            CallingConventions.Any, Array.Empty<Type>(), null);
        if (claimFromPool == null)
        {
            Logger.LogWarning($"Failed to find {typeof(Pool<ActionSettingsCollection>).Format()}.claim method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        FieldInfo? poolField = typeof(ActionSettings).GetField("CollectionPool", BindingFlags.Static | BindingFlags.NonPublic);
        if (poolField == null)
        {
            Logger.LogWarning($"Failed to find {typeof(ActionSettings).Format()}.CollectionPool field.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        PropertyInfo? flagsProperty = typeof(ActionSettingsCollection).GetProperty(nameof(ActionSettingsCollection.Flags), BindingFlags.Instance | BindingFlags.Public);
        if (flagsProperty == null || flagsProperty.GetMethod == null || flagsProperty.GetSetMethod(true) == null)
        {
            Logger.LogWarning($"Failed to find {typeof(ActionSettingsCollection).Format()}.Flags field.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        MethodInfo? reset = typeof(ActionSettingsCollection).GetMethod(nameof(ActionSettingsCollection.Reset), BindingFlags.Instance | BindingFlags.Public);
        if (reset == null)
        {
            Logger.LogWarning($"Failed to find {typeof(ActionSettingsCollection).Format()}.Reset method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }
        MethodInfo? append = typeof(StringBuilder).GetMethod(
            nameof(StringBuilder.Append), BindingFlags.Instance | BindingFlags.Public, null,
            CallingConventions.Any, new Type[] { typeof(object) }, null);
        if (append == null)
        {
            Logger.LogWarning($"Failed to find {typeof(StringBuilder).Format()}.Append method.", method: "EDITOR ACTIONS");
            DevkitServerModule.Fault();
            return;
        }

        DynamicMethod writeMethod = new DynamicMethod("SettingsWriteHandler", attributes, CallingConventions.Standard,
            typeof(void), new Type[] { typeof(IActionListener), typeof(ActionSettingsCollection).MakeByRefType(), typeof(IAction) },
            typeof(EditorActionsCodeGeneration), true);
        IOpCodeEmitter writeGenerator = new DebuggableEmitter(writeMethod) { DebugLog = false };

        DynamicMethod readMethod = new DynamicMethod("SettingsReadHandler", attributes, CallingConventions.Standard,
            typeof(void), new Type[] { typeof(IActionListener), typeof(IAction) },
            typeof(EditorActionsCodeGeneration), true);
        IOpCodeEmitter readGenerator = new DebuggableEmitter(readMethod) { DebugLog = false };

        DynamicMethod createMethod = new DynamicMethod("CreateAction", attributes, CallingConventions.Standard,
            typeof(IAction), new Type[] { typeof(ActionType) },
            typeof(EditorActionsCodeGeneration), true);
        IOpCodeEmitter createGenerator = new DebuggableEmitter(createMethod) { DebugLog = false };

        DynamicMethod byteWriteMethod = new DynamicMethod("ByteWriteHandler", attributes, CallingConventions.Standard,
            typeof(void), new Type[] { typeof(ActionSettingsCollection), typeof(ByteWriter) },
            typeof(EditorActionsCodeGeneration), true);
        IOpCodeEmitter byteWriteGenerator = new DebuggableEmitter(byteWriteMethod) { DebugLog = false };

        DynamicMethod byteReadMethod = new DynamicMethod("ByteReadHandler", attributes, CallingConventions.Standard,
            typeof(void), new Type[] { typeof(ActionSettingsCollection), typeof(ByteReader) },
            typeof(EditorActionsCodeGeneration), true);
        IOpCodeEmitter byteReadGenerator = new DebuggableEmitter(byteReadMethod) { DebugLog = false };

        DynamicMethod toStringMethod = new DynamicMethod("CollectionToStringHandler", attributes, CallingConventions.Standard,
            typeof(void), new Type[] { typeof(ActionSettingsCollection), typeof(StringBuilder) },
            typeof(EditorActionsCodeGeneration), true);
        IOpCodeEmitter toStringGenerator = new DebuggableEmitter(toStringMethod) { DebugLog = false };

        // generate dynamic methods to apply settings from interfaces.

        Label? writeNext = null;
        Label? readNext = null;

        LocalBuilder writeActionSettings = writeGenerator.DeclareLocal(typeof(ActionSettings));
        LocalBuilder writeSettings = writeGenerator.DeclareLocal(typeof(ActionSettingsCollection));

        LocalBuilder readActionSettings = readGenerator.DeclareLocal(typeof(ActionSettings));
        LocalBuilder readSettings = readGenerator.DeclareLocal(typeof(ActionSettingsCollection));

        // ActionSettings settings = actions.Settings;
        writeGenerator.Emit(OpCodes.Ldarg_0);
        writeGenerator.Emit(getActionSettings.GetCallRuntime(), getActionSettings);
        writeGenerator.Emit(OpCodes.Stloc, writeActionSettings);
        readGenerator.Emit(OpCodes.Ldarg_0);
        readGenerator.Emit(getActionSettings.GetCallRuntime(), getActionSettings);
        readGenerator.Emit(OpCodes.Stloc, readActionSettings);

        LocalBuilder anyChanged = writeGenerator.DeclareLocal(typeof(bool));
        const float tol = 0.0001f;
        foreach ((Type type, ActionSettingAttribute attr, List<PropertyInfo> list) in properties)
        {
            writeGenerator.Comment($"Type: {type.Format()}...");
            readGenerator.Comment($"Type: {type.Format()}...");

            if (writeNext.HasValue)
                writeGenerator.MarkLabel(writeNext.Value);
            writeNext = writeGenerator.DefineLabel();
            if (readNext.HasValue)
                readGenerator.MarkLabel(readNext.Value);
            readNext = readGenerator.DefineLabel();

            // anyChanged = false;
            writeGenerator.Emit(OpCodes.Ldc_I4_0);
            writeGenerator.Emit(OpCodes.Stloc, anyChanged);

            // if (action is not <type>) continue;
            writeGenerator.Emit(OpCodes.Ldarg_2);
            writeGenerator.Emit(OpCodes.Isinst, type);
            writeGenerator.Emit(OpCodes.Brfalse, writeNext.Value);
            readGenerator.Emit(OpCodes.Ldarg_1);
            readGenerator.Emit(OpCodes.Isinst, type);
            readGenerator.Emit(OpCodes.Brfalse, readNext.Value);

            Label? readNextProp = null;
            Label? writeCheckNextProp = null;
            List<LocalBuilder> lcls = new List<LocalBuilder>();
            Label updateValuePop = writeGenerator.DefineLabel();
            Label updateValueNoPop = writeGenerator.DefineLabel();
            foreach (PropertyInfo property in list)
            {
                writeGenerator.Comment($"Property: {property.Format()}, {attr.ActionSetting.Format()}.");
                readGenerator.Comment($"Property: {property.Format()}, {attr.ActionSetting.Format()}.");

                PropertyInfo? settingsProperty = null;
                if (type.IsAssignableFrom(typeof(ActionSettingsCollection)))
                    settingsProperty = property;

                if (settingsProperty == null || settingsProperty.GetMethod == null || settingsProperty.SetMethod == null)
                {
                    Logger.LogWarning($"Failed to find {typeof(ActionSettingsCollection).Format()}'s matching {attr.ActionSetting.Format()} property, skipped code generation.", method: "EDITOR ACTIONS");
                    continue;
                }

                if (writeCheckNextProp.HasValue)
                    writeGenerator.MarkLabel(writeCheckNextProp.Value);
                writeCheckNextProp = writeGenerator.DefineLabel();
                if (readNextProp.HasValue)
                    readGenerator.MarkLabel(readNextProp.Value);
                readNextProp = readGenerator.DefineLabel();

                // ActionSettingsCollection? settings = settings.GetSetting(<type>);
                writeGenerator.Emit(OpCodes.Ldloc, writeActionSettings);
                PatchUtil.LoadConstantI4(writeGenerator, (int)attr.ActionSetting);
                writeGenerator.Emit(getSettings.GetCallRuntime(), getSettings);
                readGenerator.Emit(OpCodes.Ldloc, readActionSettings);
                PatchUtil.LoadConstantI4(readGenerator, (int)attr.ActionSetting);
                readGenerator.Emit(getSettings.GetCallRuntime(), getSettings);

                // if (settings == null) goto updateValuePop;
                writeGenerator.Emit(OpCodes.Dup);
                writeGenerator.Emit(OpCodes.Brfalse, updateValuePop);

                // if (settings.GetSettings(<value>) == null) continue;
                readGenerator.Emit(OpCodes.Dup);
                Label continueButPop = readGenerator.DefineLabel();
                readGenerator.Emit(OpCodes.Brfalse, continueButPop);
                Label dontPop = readGenerator.DefineLabel();
                readGenerator.Emit(OpCodes.Br, dontPop);

                readGenerator.MarkLabel(continueButPop);
                readGenerator.Emit(OpCodes.Pop);
                readGenerator.Emit(OpCodes.Br, readNextProp.Value);

                // collection = settings;
                readGenerator.MarkLabel(dontPop);
                readGenerator.Emit(OpCodes.Stloc, readSettings);

                // if (settings.<Property> equals action.<Property>) continue;
                writeGenerator.Emit(settingsProperty.GetMethod.GetCallRuntime(), settingsProperty.GetMethod);
                if (property.PropertyType == typeof(float))
                {
                    writeGenerator.Emit(OpCodes.Ldarg_2);
                    writeGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
                    writeGenerator.Emit(OpCodes.Ldc_R4, tol);
                    writeGenerator.Emit(nearlyEqual.GetCallRuntime(), nearlyEqual);
                    writeGenerator.Emit(OpCodes.Brtrue, writeCheckNextProp.Value);
                }
                else if (property.PropertyType.IsPrimitive || property.PropertyType.IsEnum)
                {
                    writeGenerator.Emit(OpCodes.Ldarg_2);
                    writeGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
                    writeGenerator.Emit(OpCodes.Beq, writeCheckNextProp.Value);
                }
                else
                {
                    if (property.PropertyType.IsValueType)
                    {
                        LocalBuilder? lcl = null;
                        for (int i = 0; i < lcls.Count; ++i)
                        {
                            if (lcls[i].LocalType == property.PropertyType)
                            {
                                lcl = lcls[i];
                                break;
                            }
                        }
                        if (lcl == null)
                        {
                            lcl = writeGenerator.DeclareLocal(property.PropertyType);
                            lcls.Add(lcl);
                        }
                        writeGenerator.Emit(OpCodes.Stloc, lcl);
                        writeGenerator.Emit(OpCodes.Ldloca, lcl);
                        writeGenerator.Emit(OpCodes.Ldarg_2);
                        writeGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
                        writeGenerator.Emit(OpCodes.Box, property.PropertyType);
                        writeGenerator.Emit(OpCodes.Constrained, property.PropertyType);
                    }
                    else
                    {
                        writeGenerator.Emit(OpCodes.Ldarg_2);
                        writeGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
                    }
                    writeGenerator.Emit(objEquals.GetCallRuntime(), objEquals);
                    writeGenerator.Emit(OpCodes.Brtrue, writeCheckNextProp.Value);
                }

                writeGenerator.Emit(OpCodes.Ldc_I4_1);
                writeGenerator.Emit(OpCodes.Stloc, anyChanged);

                // action.<Property> = collection.<Property>
                readGenerator.Emit(OpCodes.Ldarg_1);
                readGenerator.Emit(OpCodes.Ldloc, readSettings);
                readGenerator.Emit(settingsProperty.GetMethod.GetCallRuntime(), settingsProperty.GetMethod);
                readGenerator.Emit(property.SetMethod.GetCallRuntime(), property.SetMethod);
            }

            if (writeCheckNextProp.HasValue)
                writeGenerator.MarkLabel(writeCheckNextProp.Value);
            if (readNextProp.HasValue)
                readGenerator.MarkLabel(readNextProp.Value);

            // if (!anyChanged) continue;
            writeGenerator.Emit(OpCodes.Ldloc, anyChanged);
            writeGenerator.Emit(OpCodes.Brfalse, writeNext.Value);

            writeGenerator.Emit(OpCodes.Br, updateValueNoPop);
            writeGenerator.MarkLabel(updateValuePop);
            writeGenerator.Emit(OpCodes.Pop);
            writeGenerator.MarkLabel(updateValueNoPop);
            
            writeGenerator.Emit(OpCodes.Ldarg_1);
            writeGenerator.Emit(OpCodes.Ldind_Ref);
            writeGenerator.Emit(OpCodes.Stloc, writeSettings);

            writeGenerator.Emit(OpCodes.Ldloc, writeSettings);
            Label writeSetProperties = writeGenerator.DefineLabel();
            // if (collection == null)
            //    collection = ActionSettings.CollectionPool.claim().Reset();
            writeGenerator.Emit(OpCodes.Brtrue, writeSetProperties);
            writeGenerator.Emit(OpCodes.Ldarg_1);

            writeGenerator.Emit(OpCodes.Ldsfld, poolField);
            writeGenerator.Emit(claimFromPool.GetCallRuntime(), claimFromPool);
            writeGenerator.Emit(OpCodes.Stloc, writeSettings);
            writeGenerator.Emit(OpCodes.Ldloc, writeSettings);
            writeGenerator.Emit(OpCodes.Dup);
            writeGenerator.Emit(reset.GetCallRuntime(), reset);
            writeGenerator.Emit(OpCodes.Stind_Ref);

            writeGenerator.MarkLabel(writeSetProperties);

            List<ActionSetting> used = new List<ActionSetting>(1);

            foreach (PropertyInfo property in list)
            {
                PropertyInfo? settingsProperty = null;
                if (type.IsAssignableFrom(typeof(ActionSettingsCollection)))
                    settingsProperty = property;

                if (settingsProperty == null || settingsProperty.GetMethod == null || settingsProperty.SetMethod == null)
                    continue;

                // collection.<Property> = action.<Property>
                writeGenerator.Emit(OpCodes.Ldloc, writeSettings);
                writeGenerator.Emit(OpCodes.Ldarg_2);
                writeGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
                writeGenerator.Emit(settingsProperty.SetMethod.GetCallRuntime(), settingsProperty.SetMethod);

                if (!used.Contains(attr.ActionSetting))
                {
                    writeGenerator.Emit(OpCodes.Ldloc, writeSettings);
                    writeGenerator.Emit(OpCodes.Dup);
                    // collection.Flags |= <Type>
                    writeGenerator.Emit(flagsProperty.GetMethod.GetCallRuntime(), flagsProperty.GetMethod);
                    PatchUtil.LoadConstantI4(writeGenerator, (int)attr.ActionSetting);
                    writeGenerator.Emit(OpCodes.Or);
                    writeGenerator.Emit(flagsProperty.GetSetMethod(true).GetCallRuntime(), flagsProperty.GetSetMethod(true));
                    used.Add(attr.ActionSetting);
                }
            }
        }

        if (writeNext.HasValue)
            writeGenerator.MarkLabel(writeNext.Value);
        
        if (readNext.HasValue)
            readGenerator.MarkLabel(readNext.Value);

        writeGenerator.Emit(OpCodes.Ret);
        readGenerator.Emit(OpCodes.Ret);

        List<(Label lbl, MethodBase method, ActionType actionType)> lbls = new List<(Label, MethodBase, ActionType)>(32);
        int last = -1;
        bool gap = false;
        foreach ((Type type, ActionAttribute attr) in actions.OrderBy(x => (int)x.attr.ActionType))
        {
            if (!gap)
            {
                if (last + 1 != (int)attr.ActionType)
                {
                    for (int i = last + 1; i < (int)attr.ActionType; ++i)
                        Logger.LogWarning($"Missing {((ActionType)i).Format()} action to use switch expression in generated method.", method: "EDITOR ACTIONS");
                    gap = true;
                }
                else last = (int)attr.ActionType;
            }
            MethodBase? method = null;
            if (!string.IsNullOrWhiteSpace(attr.CreateMethod))
            {
                method = type.GetMethod(attr.CreateMethod!, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Array.Empty<Type>(), null);
                if (method == null || !typeof(IAction).IsAssignableFrom(((MethodInfo)method).ReturnType))
                    Logger.LogWarning($"Unable to find method {type.Format()}.{attr.CreateMethod.Format(false)} when creating the action creator method.", method: "EDITOR ACTIONS");
            }
            if (method == null)
            {
                ConstructorInfo[] ctors = type.GetConstructors();
                foreach (ConstructorInfo ctor in ctors)
                {
                    if (ctor.GetParameters().Length == 0)
                        method = ctor;
                }
            }

            if (method != null)
                lbls.Add((createGenerator.DefineLabel(), method, attr.ActionType));
            else
                Logger.LogWarning($"Unable to find a method or constructor for {type.Format()} when creating the action creator method.", method: "EDITOR ACTIONS");
        }
        if (lbls.Count > 0)
        {
            if (!gap)
            {
                Label[] lblArr = new Label[lbls.Count];
                for (int i = 0; i < lbls.Count; ++i)
                    lblArr[i] = lbls[i].lbl;
                createGenerator.Emit(OpCodes.Ldarg_0);
                createGenerator.Emit(OpCodes.Switch, lblArr);
                createGenerator.Emit(OpCodes.Ldnull);
                createGenerator.Emit(OpCodes.Ret);
            }
            for (int i = 0; i < lbls.Count; ++i)
            {
                (Label lbl, MethodBase method, ActionType actionType) = lbls[i];
                createGenerator.MarkLabel(lbl);
                Label? lbl2 = null;
                if (gap)
                {
                    createGenerator.Emit(OpCodes.Ldarg_0);
                    PatchUtil.LoadConstantI4(createGenerator, (int)actionType);
                    if (i < lbls.Count - 1)
                        createGenerator.Emit(OpCodes.Bne_Un, lbls[i + 1].lbl);
                    else
                    {
                        lbl2 = createGenerator.DefineLabel();
                        createGenerator.Emit(OpCodes.Beq, lbl2.Value);
                        createGenerator.Emit(OpCodes.Ldnull);
                        createGenerator.Emit(OpCodes.Ret);
                    }
                }

                if (lbl2.HasValue)
                    createGenerator.MarkLabel(lbl2.Value);

                if (method is ConstructorInfo ctor)
                    createGenerator.Emit(OpCodes.Newobj, ctor);
                else if (method is MethodInfo method2)
                    createGenerator.Emit(method2.GetCallRuntime(), method2);
                else createGenerator.Emit(OpCodes.Ldnull);
                createGenerator.Emit(OpCodes.Ret);
            }
        }
        else
        {
            createGenerator.Emit(OpCodes.Ldnull);
            createGenerator.Emit(OpCodes.Ret);
        }
        
        Type[] interfaces = typeof(ActionSettingsCollection).GetInterfaces();
        IEnumerable<(ActionSetting, PropertyInfo)> settingsProperties = new List<(ActionSetting, PropertyInfo)>(16);
        foreach ((Type type, ActionSettingAttribute attr, List<PropertyInfo> list) in properties)
        {
            if (interfaces.Contains(type))
            {
                foreach (PropertyInfo prop in list)
                    ((List<(ActionSetting, PropertyInfo)>)settingsProperties).Add((attr.ActionSetting, prop));
            }
        }

        settingsProperties = settingsProperties.OrderBy(prop => prop.Item1).ThenBy(prop => prop.Item2.Name);

        foreach ((ActionSetting setting, PropertyInfo property) in settingsProperties)
        {
            if (property.GetMethod == null || property.GetSetMethod(true) == null)
            {
                Logger.LogWarning($"Unable to find a getter for {property.Format()} when creating the read/write methods.", method: "EDITOR ACTIONS");
                return;
            }
            MethodInfo? write = ByteWriter.GetWriteMethod(property.PropertyType);
            MethodInfo? read = ByteReader.GetReadMethod(property.PropertyType);
            if (write == null || read == null || write.GetParameters().Length != 1 || read.GetParameters().Length != 0 || !write.GetParameters()[0].ParameterType.IsAssignableFrom(property.PropertyType) || read.ReturnType == typeof(void) || !property.PropertyType.IsAssignableFrom(read.ReturnType))
            {
                Logger.LogWarning($"Unable to find a read or write method for {property.Format()}'s type: {property.PropertyType.Format()} when creating the read/write methods.", method: "EDITOR ACTIONS");
                return;
            }

            // if ((collection.Flags & <setting flag>) != 0)
            Label nextWrite = byteWriteGenerator.DefineLabel();
            Label nextRead = byteReadGenerator.DefineLabel();
            Label nextToString = toStringGenerator.DefineLabel();
            byteWriteGenerator.Emit(OpCodes.Ldarg_0);
            byteReadGenerator.Emit(OpCodes.Ldarg_0);
            toStringGenerator.Emit(OpCodes.Ldarg_0);
            byteWriteGenerator.Emit(flagsProperty.GetMethod.GetCallRuntime(), flagsProperty.GetMethod);
            byteReadGenerator.Emit(flagsProperty.GetMethod.GetCallRuntime(), flagsProperty.GetMethod);
            toStringGenerator.Emit(flagsProperty.GetMethod.GetCallRuntime(), flagsProperty.GetMethod);
            PatchUtil.LoadConstantI4(byteWriteGenerator, (int)setting);
            PatchUtil.LoadConstantI4(byteReadGenerator, (int)setting);
            PatchUtil.LoadConstantI4(toStringGenerator, (int)setting);
            byteWriteGenerator.Emit(OpCodes.And);
            byteReadGenerator.Emit(OpCodes.And);
            toStringGenerator.Emit(OpCodes.And);
            byteWriteGenerator.Emit(OpCodes.Ldc_I4_0);
            byteReadGenerator.Emit(OpCodes.Ldc_I4_0);
            toStringGenerator.Emit(OpCodes.Ldc_I4_0);
            byteWriteGenerator.Emit(OpCodes.Beq, nextWrite);
            byteReadGenerator.Emit(OpCodes.Beq, nextRead);
            toStringGenerator.Emit(OpCodes.Beq, nextToString);


            // writer.Write(<value>);
            byteWriteGenerator.Emit(OpCodes.Ldarg_1);
            byteWriteGenerator.Emit(OpCodes.Ldarg_0);
            byteWriteGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
            byteWriteGenerator.Emit(write.GetCallRuntime(), write);
            if (write.ReturnType != typeof(void))
                byteWriteGenerator.Emit(OpCodes.Pop);

            byteReadGenerator.Emit(OpCodes.Ldarg_0);
            byteReadGenerator.Emit(OpCodes.Ldarg_1);
            byteReadGenerator.Emit(read.GetCallRuntime(), read);
            byteReadGenerator.Emit(property.GetSetMethod(true).GetCallRuntime(), property.GetSetMethod(true));

            toStringGenerator.Emit(OpCodes.Ldarg_1);
            toStringGenerator.Emit(OpCodes.Ldstr, " " + property.Name + ": ");
            toStringGenerator.Emit(append.GetCallRuntime(), append);
            toStringGenerator.Emit(OpCodes.Ldarg_0);
            toStringGenerator.Emit(property.GetMethod.GetCallRuntime(), property.GetMethod);
            if (property.PropertyType.IsValueType)
                toStringGenerator.Emit(OpCodes.Box, property.PropertyType);
            toStringGenerator.Emit(append.GetCallRuntime(), append);
            if (append.ReturnType != typeof(void))
                toStringGenerator.Emit(OpCodes.Pop);

            byteWriteGenerator.MarkLabel(nextWrite);
            byteReadGenerator.MarkLabel(nextRead);
            toStringGenerator.MarkLabel(nextToString);
        }
        byteWriteGenerator.Emit(OpCodes.Ret);
        byteReadGenerator.Emit(OpCodes.Ret);
        toStringGenerator.Emit(OpCodes.Ret);

        OnWritingAction = (HandleWriteSettings)writeMethod.CreateDelegate(typeof(HandleWriteSettings));
        OnReadingAction = (HandleReadSettings)readMethod.CreateDelegate(typeof(HandleReadSettings));
        CreateAction = (HandleCreateNewAction)createMethod.CreateDelegate(typeof(HandleCreateNewAction));
        WriteSettingsCollection = (HandleByteWriteSettings)byteWriteMethod.CreateDelegate(typeof(HandleByteWriteSettings));
        ReadSettingsCollection = (HandleByteReadSettings)byteReadMethod.CreateDelegate(typeof(HandleByteReadSettings));
        AppendSettingsCollection = (HandleAppendSettingsCollection)toStringMethod.CreateDelegate(typeof(HandleAppendSettingsCollection));
        Init = true;

#if DEBUG
        /*
         * Run little tests for each method to see if they throw InvalidProgramExceptions or any other IL related exceptions.
         *
         * NullReferenceExceptions are desired in this case.
         */
        bool anyFail = false;
        ActionSettingsCollection c2 = null!;
        try
        {
            OnWritingAction(null!, ref c2!, null!);
        }
        catch (NullReferenceException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            anyFail = true;
        }

        try
        {
            OnReadingAction(null!, null!);
        }
        catch (NullReferenceException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            anyFail = true;
        }

        try
        {
            CreateAction(0);
        }
        catch (NullReferenceException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            anyFail = true;
        }

        try
        {
            WriteSettingsCollection(null!, null!);
        }
        catch (NullReferenceException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            anyFail = true;
        }

        try
        {
            ReadSettingsCollection(null!, null!);
        }
        catch (NullReferenceException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            anyFail = true;
        }
        

        try
        {
            AppendSettingsCollection(null!, null!);
        }
        catch (NullReferenceException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            anyFail = true;
        }

        if (anyFail)
        {
            DevkitServerModule.Fault();
            throw new Exception("Failed to create valid ActionSetting dynamic methods.");
        }
#endif
    }
}

internal delegate void HandleWriteSettings(IActionListener actions, ref ActionSettingsCollection? collection, IAction action);
internal delegate void HandleReadSettings(IActionListener actions, IAction action);
internal delegate IAction? HandleCreateNewAction(ActionType type);
internal delegate void HandleByteWriteSettings(ActionSettingsCollection collection, ByteWriter writer);
internal delegate void HandleByteReadSettings(ActionSettingsCollection collection, ByteReader reader);
internal delegate void HandleAppendSettingsCollection(ActionSettingsCollection collection, StringBuilder builder);

[AttributeUsage(AttributeTargets.Interface)]
internal sealed class ActionSettingAttribute : Attribute
{
    public ActionSetting ActionSetting { get; }
    public ActionSettingAttribute(ActionSetting setting)
    {
        ActionSetting = setting;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
internal sealed class ActionAttribute : Attribute
{
    public ActionType ActionType { get; }

    /// <summary>
    /// Name of a static method in this class to use to create the action (instead of default constructor).
    /// </summary>
    public string? CreateMethod { get; set; }

    /// <summary>
    /// Max size in bytes.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Max size in bytes of a full option change.
    /// </summary>
    public int OptionCapacity { get; }

    /// <summary>
    /// Set at runtime.
    /// </summary>
    public Type? Type { get; internal set; }
    public ActionAttribute(ActionType type, int capacity, int optionCapacity)
    {
        ActionType = type;
        Capacity = capacity;
        OptionCapacity = optionCapacity;
    }
}