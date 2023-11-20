using HarmonyLib;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace DevkitServer.API.Abstractions;

/// <summary>
/// Wrapper for <see cref="ILGenerator"/> which provides debug logging while emitting and/or calling the method. Can also be used with Harmony transpilers.
/// </summary>
public class DebuggableEmitter : IOpCodeEmitter
{
    private bool _init;
    private bool _lastWasPrefix;
    private string _prefix = string.Empty;
    private string? _logSource;

    /// <summary>
    /// Actively editing method.
    /// </summary>
    public MethodBase? Method { get; }

    /// <summary>
    /// Underlying <see cref="ILGenerator"/>.
    /// </summary>
    public IOpCodeEmitter Generator { get; }

    /// <inheritdoc />
    public int ILOffset => Generator.ILOffset;

    /// <summary>
    /// Indent level of debug logging.
    /// </summary>
    public int LogIndent { get; private set; }
    
    /// <summary>
    /// Enable debug logging while emitting instructions.
    /// </summary>
    public bool DebugLog { get; set; }

    /// <summary>
    /// Enable debug logging while calling instructions.
    /// </summary>
    /// <remarks>This is done by inserting log calls for each instruction.</remarks>
    public bool Breakpointing { get; set; }

    /// <summary>
    /// Is this emitter acting as a transpiler wrapper instead of <see cref="ILGenerator"/> wrapper.
    /// </summary>
    public bool IsTranspileMode { get; }

    /// <summary>
    /// Instruction index in <see cref="TranspileInstructions"/> when being used in transpile mode.
    /// </summary>
    public int Index { get; private set; }

    public string? LogSource
    {
        get => _logSource;
        set
        {
            _logSource = value;
            _prefix = string.IsNullOrEmpty(value) ? string.Empty : "[" + value + "] ";
        }
    }

    /// <summary>
    /// Instruction list when being used in transpile mode.
    /// </summary>
    public List<CodeInstruction>? TranspileInstructions { get; }
    public DebuggableEmitter(IOpCodeEmitter generator, MethodBase method, List<CodeInstruction> instructions, int index = -1)
    {
        if (generator.GetType() == typeof(DebuggableEmitter))
            generator = ((DebuggableEmitter)generator).Generator;
        Generator = generator;
        Method = method;
        IsTranspileMode = true;
        TranspileInstructions = instructions;
        Index = index == -1 ? instructions.Count : index;
    }
    public DebuggableEmitter(IOpCodeEmitter generator, MethodBase? method)
    {
        if (generator.GetType() == typeof(DebuggableEmitter))
            generator = ((DebuggableEmitter)generator).Generator;
        Generator = generator;
        Method = method;
    }
    public DebuggableEmitter(DynamicMethod method) : this(method.GetILGenerator().AsEmitter(), method) { }

    private void CheckInit()
    {
#if DEBUG
        if (_init || Method is null) return;
        if (DebugLog)
        {
            Logger.LogDebug(_prefix + ".method ".Colorize(ConsoleColor.DarkCyan) + Method.Format());
            if (Breakpointing)
                Logger.LogDebug(_prefix + " (with breakpointing)", ConsoleColor.DarkRed);
        }
        if (Breakpointing)
        {
            try
            {
                Generator.Emit(OpCodes.Ldstr, _prefix + ".method " + Method.Format() + Environment.NewLine);
                Generator.Emit(OpCodes.Ldsfld, Accessor.LoggerStackCleanerField);
                Generator.Emit(OpCodes.Ldc_I4_1);
                Generator.Emit(OpCodes.Newobj, Accessor.StackTraceIntConstructor);
                Generator.Emit(Accessor.StackTraceCleanerGetStringMethod.GetCallRuntime(), Accessor.StackTraceCleanerGetStringMethod);
                Generator.Emit(Accessor.Concat2StringsMethod.GetCallRuntime(), Accessor.Concat2StringsMethod);
            }
            catch (MemberAccessException)
            {
                Generator.Emit(OpCodes.Ldstr, _prefix + ".method " + Method.Format());
            }
            PatchUtil.LoadConstantI4(this, (int)ConsoleColor.DarkRed);
            Generator.Emit(Accessor.LogDebug.GetCallRuntime(), Accessor.LogDebug);
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
    public void Comment(string comment)
    {
        CheckInit();
        if (DebugLog)
            Logger.LogDebug(new string(' ', LogIndent + ILOffset.ToString("X5").Length + 3) + "// " + comment.Colorize(ConsoleColor.DarkGray));
    }
    /// <inheritdoc />
    public void BeginCatchBlock(Type exceptionType)
    {
        --LogIndent;
        Log("}");
        Log(".catch (" + exceptionType.Format() + ") {");
        ++LogIndent;
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            ins.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, exceptionType));
        }
        else
        {
            Generator.BeginCatchBlock(exceptionType);
        }
    }
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
    public LocalBuilder DeclareLocal(Type localType) => DeclareLocal(localType, false);
    /// <inheritdoc />
    public LocalBuilder DeclareLocal(Type localType, bool pinned)
    {
        LocalBuilder lcl = Generator.DeclareLocal(localType, pinned);
        Log("// Declared local: # " + lcl.LocalIndex.Format() + " " + (lcl.LocalType ?? localType).Format() + " (Pinned: " + lcl.IsPinned.Format() + ")");
        return lcl;
    }
    /// <inheritdoc />
    public Label DefineLabel()
    {
        Label lbl = Generator.DefineLabel();
        Log("// Defined label: " + lbl.Format());
        return lbl;
    }
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
    public void Emit(OpCode opcode, string str)
    {
        Log(opcode, str);
        if (IsTranspileMode)
        {
            TranspileInstructions!.Insert(Index, new CodeInstruction(opcode, str));
            ++Index;
        }
        else
        {
            Generator.Emit(opcode, str);
        }
    }
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
    public void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[]? optionalParameterTypes)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitCall is not supported in Transpile mode.");
        }
        Log(opcode, methodInfo);

        Generator.EmitCall(opcode, methodInfo, optionalParameterTypes);
    }
    /// <inheritdoc />
    public void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[]? optionalParameterTypes)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitCalli is not supported in Transpile mode.");
        }
        Log(opcode, null);

        Generator.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
    }
    /// <inheritdoc />
    public void EmitCalli(OpCode opcode, CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitCalli is not supported in Transpile mode.");
        }
        Log(opcode, null);

        Generator.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
    }
    /// <inheritdoc />
    public void EmitWriteLine(string value)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitWriteLine is not supported in Transpile mode.");
        }
        Log("// Write Line: " + value.Format(true));

        Generator.EmitWriteLine(value);
    }
    /// <inheritdoc />
    public void EmitWriteLine(LocalBuilder localBuilder)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitWriteLine is not supported in Transpile mode.");
        }
        Log("// Write Line: Local #" + localBuilder.LocalIndex.Format());

        Generator.EmitWriteLine(localBuilder);
    }
    /// <inheritdoc />
    public void EmitWriteLine(FieldInfo fld)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("EmitWriteLine is not supported in Transpile mode.");
        }
        Log("// Write Line: Field " + fld.Format());

        Generator.EmitWriteLine(fld);
    }
    /// <inheritdoc />
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
    /// <inheritdoc />
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
    /// <inheritdoc />
    public void MarkLabel(Label loc)
    {
        Log(".label " + loc.Format() + ": @ IL" + ILOffset.ToString("X").Format(false) + ".");
        if (IsTranspileMode)
        {
            CodeInstruction ins = GetCurrentCodeInstruction();
            if (!ins.labels.Contains(loc))
                ins.labels.Add(loc);
        }
        else
        {
            Generator.MarkLabel(loc);
        }
    }
    /// <inheritdoc />
    public void MarkSequencePoint(ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("Sequence points are not supported in Transpile mode.");
        }
        Log($"// Sequence Point: (line, column) Start: {startLine.Format()}, {startColumn.Format()}, End: {endLine.Format()}, {endColumn.Format()}.");
        Generator.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
    }
    /// <inheritdoc />
    public void ThrowException(Type excType)
    {
        if (IsTranspileMode)
        {
            throw new NotSupportedException("ThrowException is not supported in Transpile mode.");
        }
        Log($"// Throw Exception {excType.Format()}.");
        Generator.ThrowException(excType);
    }
    /// <inheritdoc />
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
            Logger.LogDebug(_prefix + msg);
        if (Breakpointing)
        {
            Generator.Emit(OpCodes.Ldstr, _prefix + msg);
            PatchUtil.LoadConstantI4(this, (int)ConsoleColor.DarkRed);
            Generator.Emit(Accessor.LogDebug.GetCallRuntime(), Accessor.LogDebug);
        }
    }
    [Conditional("DEBUG")]
    private void Log(OpCode code, object? operand)
    {
        if (!DebugLog && !Breakpointing) return;
        CheckInit();
        string msg = "IL" + ILOffset.ToString("X5") + " " + (LogIndent <= 0 ? string.Empty : new string(' ', LogIndent)) + new CodeInstruction(code, operand).Format();
        if (DebugLog)
            Logger.LogDebug(_prefix + msg);
        if (Breakpointing && !_lastWasPrefix)
        {
            Generator.Emit(OpCodes.Ldstr, _prefix + msg);
            PatchUtil.LoadConstantI4(this, (int)ConsoleColor.DarkRed);
            Generator.Emit(Accessor.LogDebug.GetCallRuntime(), Accessor.LogDebug);
        }
        _lastWasPrefix = code.OpCodeType == OpCodeType.Prefix;
    }
#if NETFRAMEWORK
    void _ILGenerator.GetIDsOfNames(ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        => Generator.GetIDsOfNames(ref riid, rgszNames, cNames, lcid, rgDispId);
    void _ILGenerator.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        => Generator.GetTypeInfo(iTInfo, lcid, ppTInfo);
    void _ILGenerator.GetTypeInfoCount(out uint pcTInfo)
        => Generator.GetTypeInfoCount(out pcTInfo);
    void _ILGenerator.Invoke(uint dispIdMember, ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr)
        => Generator.Invoke(dispIdMember, ref riid, lcid, wFlags, pDispParams, pVarResult, pExcepInfo, puArgErr);
#endif
}
