using HarmonyLib;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using StackCleaner;

namespace DevkitServer.Util;
public class DebuggableEmitter
{
    private bool _init;
    private bool _lastWasPrefix;
    public MethodBase Method { get; }
    public ILGenerator Generator { get; }
    public int ILOffset => Generator.ILOffset;
    public int LogIndent { get; private set; }
    public bool DebugLog { get; set; }
    public bool Breakpointing { get; set; }
    public bool IsTranspileMode { get; set; }
    public int TranspileIndex { get; set; }
    public List<CodeInstruction>? TranspileInstructions { get; }
    public int Index { get; private set; }
    public DebuggableEmitter(ILGenerator generator, MethodBase method, List<CodeInstruction> instructions, int index = -1)
    {
        Generator = generator;
        Method = method;
        IsTranspileMode = true;
        TranspileInstructions = instructions;
        Index = index == -1 ? instructions.Count : index;
    }
    public DebuggableEmitter(ILGenerator generator, MethodBase method)
    {
        Generator = generator;
        Method = method;
    }
    public DebuggableEmitter(DynamicMethod method) : this (method.GetILGenerator(), method) { }
    
    private void CheckInit()
    {
#if DEBUG
        if (_init) return;
        if (DebugLog)
        {
            Logger.LogDebug(".method ".Colorize(ConsoleColor.DarkCyan) + Method.Format());
            if (Breakpointing)
                Logger.LogDebug(" (with breakpointing)", ConsoleColor.DarkRed);
        }
        if (Breakpointing)
        {
            try
            {
                Generator.Emit(OpCodes.Ldstr, ".method " + Method.Format() + Environment.NewLine);
                Generator.Emit(OpCodes.Ldsfld, Accessor.LoggerStackCleanerField);
                Generator.Emit(OpCodes.Ldc_I4_1);
                Generator.Emit(OpCodes.Newobj, Accessor.StackTraceIntConstructor);
                Generator.Emit(OpCodes.Call, Accessor.StackTraceCleanerGetStringMethod);
                Generator.Emit(OpCodes.Call, Accessor.Concat2StringsMethod);
            }
            catch (MemberAccessException)
            {
                Generator.Emit(OpCodes.Ldstr, ".method " + Method.Format());
            }
            PatchUtil.LoadConstantI4(Generator, (int)ConsoleColor.DarkRed);
            Generator.Emit(OpCodes.Call, Accessor.LogDebug);
        }
        _init = true;
#endif
    }

    public void GotoIndex(int index)
    {
        if (!IsTranspileMode)
            throw new InvalidOperationException("Not in transpile mode");

        if (index > TranspileInstructions!.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Index is higher than instruction count.");

        if (index < 0)
            index = TranspileInstructions.Count;
        Index = index;
    }
    public CodeInstruction GetCurrentCodeInstruction()
    {
        if (Index >= TranspileInstructions!.Count)
            throw new InvalidOperationException("Index is too high to add a label or block instruction.");
        return TranspileInstructions[Index];
    }
    
    [Conditional("DEBUG")]
    public void Comment(string comment)
    {
        CheckInit();
        if (DebugLog)
            Logger.LogDebug(new string(' ', LogIndent + ILOffset.ToString("X5").Length + 3) + "// " + comment.Colorize(ConsoleColor.DarkGray));
    }
    public void BeginCatchBlock(Type type)
    {
        --LogIndent;
        Log("}");
        Log(".catch (" + type.Format() + ") {");
        ++LogIndent;
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, type));
        }
        else
        {
            Generator.BeginCatchBlock(type);
        }
    }
    public void BeginExceptFilterBlock()
    {
        Log(".try (filter) {");
        ++LogIndent;
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptFilterBlock));
        }
        else
        {
            Generator.BeginExceptFilterBlock();
        }
    }
    public void BeginExceptionBlock()
    {
        Log(".try {");
        ++LogIndent;
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        }
        else
        {
            Generator.BeginExceptionBlock();
        }
    }
    public void BeginFaultBlock()
    {
        Log(".fault {");
        ++LogIndent;
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFaultBlock));
        }
        else
        {
            Generator.BeginFaultBlock();
        }
    }
    public void BeginFinallyBlock()
    {
        --LogIndent;
        Log("}");
        Log(".finally {");
        ++LogIndent;
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
        }
        else
        {
            Generator.BeginFinallyBlock();
        }
    }
    public void BeginScope()
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("Scope blocks are not supported in Transpile mode.");
        }
        Log(".scope {");
        ++LogIndent;

        Generator.BeginScope();
    }
    public LocalBuilder DeclareLocal(Type type) => DeclareLocal(type, false);
    public LocalBuilder DeclareLocal(Type type, bool pinned)
    {
        LocalBuilder lcl = Generator.DeclareLocal(type, pinned);
        Log("// Declared local: # " + lcl.LocalIndex.Format() + " " + (lcl.LocalType ?? type).Format() + " (Pinned: " + lcl.IsPinned.Format() + ")");
        return lcl;
    }
    public Label DefineLabel()
    {
        Label lbl = Generator.DefineLabel();
        Log("// Defined label: " + lbl.Format());
        return lbl;
    }
    public void Emit(OpCode opcode)
    {
        Log(opcode, null);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode);
        }
    }
    public void Emit(OpCode opcode, byte arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, double arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, float arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, int arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, long arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, sbyte arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, short arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, string arg)
    {
        Log(opcode, arg);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, arg));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, arg);
        }
    }
    public void Emit(OpCode opcode, ConstructorInfo con)
    {
        Log(opcode, con);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, con));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, con);
        }
    }
    public void Emit(OpCode opcode, Label label)
    {
        Log(opcode, label);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, label));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, label);
        }
    }
    public void Emit(OpCode opcode, Label[] labels)
    {
        Log(opcode, labels);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, labels));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, labels);
        }
    }
    public void Emit(OpCode opcode, LocalBuilder local)
    {
        Log(opcode, local);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, local));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, local);
        }
    }
    public void Emit(OpCode opcode, SignatureHelper signature)
    {
        Log(opcode, signature);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, signature));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, signature);
        }
    }
    public void Emit(OpCode opcode, FieldInfo field)
    {
        Log(opcode, field);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, field));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, field);
        }
    }
    public void Emit(OpCode opcode, MethodInfo meth)
    {
        Log(opcode, meth);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, meth));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, meth);
        }
    }
    public void Emit(OpCode opcode, Type cls)
    {
        Log(opcode, cls);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, cls));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, cls);
        }
    }
    public void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitCall is not supported in Transpile mode.");
        }
        Log(opcode, methodInfo);

        Generator.EmitCall(opcode, methodInfo, optionalParameterTypes);
    }
    public void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitCalli is not supported in Transpile mode.");
        }
        Log(opcode, null);

        Generator.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
    }
    public void EmitCalli(OpCode opcode, System.Runtime.InteropServices.CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitCalli is not supported in Transpile mode.");
        }
        Log(opcode, null);

        Generator.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
    }
    public void EmitWriteLine(string str)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitWriteLine is not supported in Transpile mode.");
        }
        Log("// Write Line: " + str.Format(true));

        Generator.EmitWriteLine(str);
    }
    public void EmitWriteLine(LocalBuilder local)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitWriteLine is not supported in Transpile mode.");
        }
        Log("// Write Line: Local #" + local.LocalIndex.Format());

        Generator.EmitWriteLine(local);
    }
    public void EmitWriteLine(FieldInfo field)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitWriteLine is not supported in Transpile mode.");
        }
        Log("// Write Line: Field " + field.Format());

        Generator.EmitWriteLine(field);
    }
    public void EndExceptionBlock()
    {
        --LogIndent;
        Log("}");
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
        }
        else
        {
            Generator.EndExceptionBlock();
        }
    }
    public void EndScope()
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("Scope blocks are not supported in Transpile mode.");
        }
        --LogIndent;
        Log("}");

        Generator.EndScope();
    }
    public void MarkLabel(Label label)
    {
        Log(".label " + label.Format() + ": @ IL" + ILOffset.ToString("X").Format(false) + ".");
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            if (!ins.labels.Contains(label))
                ins.labels.Add(label);
        }
        else
        {
            Generator.MarkLabel(label);
        }
    }
    public void MarkSequencePoint(ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("Sequence points are not supported in Transpile mode.");
        }
        Log($"// Sequence Point: (line, column) Start: {startLine.Format()}, {startColumn.Format()}, End: {endLine.Format()}, {endColumn.Format()}.");
        Generator.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
    }
    public void ThrowException(Type type)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("ThrowException is not supported in Transpile mode.");
        }
        Log($"// Throw Exception {type.Format()}.");
        Generator.ThrowException(type);
    }
    public void UsingNamespace(string usingNamespace)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("UsingNamespace is not supported in Transpile mode.");
        }
        Log($".using {usingNamespace.Format(false)};");
        Generator.UsingNamespace(usingNamespace);
    }

    [Conditional("DEBUG")]
    private void Log(string txt)
    {
        if (!DebugLog && !Breakpointing) return;
        CheckInit();
        string msg;
        if (txt.StartsWith("//"))
            msg = new string(' ', LogIndent + ILOffset.ToString("X5").Length + 3) + txt.Colorize(ConsoleColor.DarkGreen);
        else
            msg = "IL" + ILOffset.ToString("X5") + " " + (LogIndent <= 0 ? string.Empty : new string(' ', LogIndent)) + txt.Colorize(ConsoleColor.DarkCyan);
        if (DebugLog)
            Logger.LogDebug(msg);
        if (Breakpointing)
        {
            Generator.Emit(OpCodes.Ldstr, msg);
            PatchUtil.LoadConstantI4(Generator, (int)ConsoleColor.DarkRed);
            Generator.Emit(OpCodes.Call, Accessor.LogDebug);
        }
    }
    [Conditional("DEBUG")]
    private void Log(OpCode code, object? operand)
    {
        if (!DebugLog && !Breakpointing) return;
        CheckInit();
        string msg = "IL" + ILOffset.ToString("X5") + " " + (LogIndent <= 0 ? string.Empty : new string(' ', LogIndent)) + new CodeInstruction(code, operand).Format();
        if (DebugLog)
            Logger.LogDebug(msg);
        if (Breakpointing && !_lastWasPrefix)
        {
            Generator.Emit(OpCodes.Ldstr, msg);
            PatchUtil.LoadConstantI4(Generator, (int)ConsoleColor.DarkRed);
            Generator.Emit(OpCodes.Call, Accessor.LogDebug);
        }
        _lastWasPrefix = code.OpCodeType == OpCodeType.Prefix;
    }
}
