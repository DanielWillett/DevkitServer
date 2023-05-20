using HarmonyLib;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Util;
public class DebuggableEmitter
{
#if DEBUG
    private bool _init;
    public MethodBase Method { get; }
#endif
    public ILGenerator Generator { get; }
    public int ILOffset => Generator.ILOffset;
    public int LogIndent { get; private set; }
    public bool DebugLog { get; set; }
    public bool Breakpointing { get; set; }
    private bool _lastWasPrefix;
    public DebuggableEmitter(ILGenerator generator, MethodBase method)
    {
        Generator = generator;
#if DEBUG
        Method = method;
#endif
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
        _init = true;
#endif
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
        Log(".catch (" + type.Format() + ") {");
        ++LogIndent;
        Generator.BeginCatchBlock(type);
    }
    public void BeginExceptFilterBlock()
    {
        Log(".exception filter {");
        ++LogIndent;
        Generator.BeginExceptFilterBlock();
    }
    public void BeginExceptionBlock()
    {
        Log(".exception {");
        ++LogIndent;
        Generator.BeginExceptionBlock();
    }
    public void BeginFaultBlock()
    {
        Log(".fault {");
        ++LogIndent;
        Generator.BeginFaultBlock();
    }
    public void BeginFinallyBlock()
    {
        Log(".finally {");
        ++LogIndent;
        Generator.BeginFinallyBlock();
    }
    public void BeginScope()
    {
        Log(".scope {");
        ++LogIndent;
        Generator.BeginScope();
    }
    public LocalBuilder DeclareLocal(Type type) => DeclareLocal(type, false);
    public LocalBuilder DeclareLocal(Type type, bool pinned)
    {
        LocalBuilder lcl = Generator.DeclareLocal(type, pinned);
        Log("// Declared local: " + (lcl.LocalType ?? type).Format() + " # " + lcl.LocalIndex.Format() + " (Pinned: " + lcl.IsPinned.Format() + ")");
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
        Generator.Emit(opcode);
    }
    public void Emit(OpCode opcode, byte arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, double arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, float arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, int arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, long arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, sbyte arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, short arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, string arg)
    {
        Log(opcode, arg);
        Generator.Emit(opcode, arg);
    }
    public void Emit(OpCode opcode, ConstructorInfo con)
    {
        Log(opcode, con);
        Generator.Emit(opcode, con);
    }
    public void Emit(OpCode opcode, Label label)
    {
        Log(opcode, label);
        Generator.Emit(opcode, label);
    }
    public void Emit(OpCode opcode, Label[] labels)
    {
        Log(opcode, labels);
        Generator.Emit(opcode, labels);
    }
    public void Emit(OpCode opcode, LocalBuilder local)
    {
        Log(opcode, local);
        Generator.Emit(opcode, local);
    }
    public void Emit(OpCode opcode, SignatureHelper signature)
    {
        Log(opcode, signature);
        Generator.Emit(opcode, signature);
    }
    public void Emit(OpCode opcode, FieldInfo field)
    {
        Log(opcode, field);
        Generator.Emit(opcode, field);
    }
    public void Emit(OpCode opcode, MethodInfo meth)
    {
        Log(opcode, meth);
        Generator.Emit(opcode, meth);
    }
    public void Emit(OpCode opcode, Type cls)
    {
        Log(opcode, cls);
        Generator.Emit(opcode, cls);
    }
    public void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes)
    {
        Log(opcode, methodInfo);
        Generator.EmitCall(opcode, methodInfo, optionalParameterTypes);
    }
    public void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes)
    {
        Log(opcode, null);
        Generator.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
    }
    public void EmitCalli(OpCode opcode, System.Runtime.InteropServices.CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes)
    {
        Log(opcode, null);
        Generator.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
    }
    public void EmitWriteLine(string str)
    {
        Log("// Write Line: " + str.Format(true));
        Generator.EmitWriteLine(str);
    }
    public void EmitWriteLine(LocalBuilder local)
    {
        Log("// Write Line: Local #" + local.LocalIndex.Format());
        Generator.EmitWriteLine(local);
    }
    public void EmitWriteLine(FieldInfo field)
    {
        Log("// Write Line: Field " + field.Format());
        Generator.EmitWriteLine(field);
    }
    public void EndExceptionBlock()
    {
        Log("}");
        --LogIndent;
        Generator.EndExceptionBlock();
    }
    public void EndScope()
    {
        Log("}");
        --LogIndent;
        Generator.EndScope();
    }
    public void MarkLabel(Label label)
    {
        Log(".label " + label.Format() + ": @ IL" + ILOffset.ToString("X").Format(false) + ".");
        Generator.MarkLabel(label);
    }
    public void MarkSequencePoint(ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
    {
        Log($"// Sequence Point: (line, column) Start: {startLine.Format()}, {startColumn.Format()}, End: {endLine.Format()}, {endColumn.Format()}.");
        Generator.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
    }
    public void ThrowException(Type type)
    {
        Log($"// Throw Exception {type.Format()}.");
        Generator.ThrowException(type);
    }
    public void UsingNamespace(string usingNamespace)
    {
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
