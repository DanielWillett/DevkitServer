using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace DevkitServer.API.Abstractions;

/// <summary>
/// Wraps a <see cref="ILGenerator"/> to implement <see cref="IOpCodeEmitter"/>.
/// </summary>
public class ILGeneratorEmitter(ILGenerator generator) : IOpCodeEmitter
{
    private readonly ILGenerator _generator = generator;

    /// <summary>
    /// Underlying <see cref="ILGenerator"/> emitter.
    /// </summary>
    public ILGenerator Generator => _generator;
    /// <inheritdoc />
    public int ILOffset => _generator.ILOffset;
    /// <inheritdoc />
    public virtual void Comment(string comment) { }
    /// <inheritdoc />
    public virtual void BeginCatchBlock(Type exceptionType) => _generator.BeginCatchBlock(exceptionType);
    /// <inheritdoc />
    public virtual void BeginExceptFilterBlock() => _generator.BeginExceptFilterBlock();
    /// <inheritdoc />
    public virtual void BeginExceptionBlock() => _generator.BeginExceptionBlock();
    /// <inheritdoc />
    public virtual void BeginFaultBlock() => _generator.BeginFaultBlock();
    /// <inheritdoc />
    public virtual void BeginFinallyBlock() => _generator.BeginFinallyBlock();
    /// <inheritdoc />
    public virtual void BeginScope() => _generator.BeginScope();
    /// <inheritdoc />
    public virtual LocalBuilder DeclareLocal(Type localType) => _generator.DeclareLocal(localType);
    /// <inheritdoc />
    public virtual LocalBuilder DeclareLocal(Type localType, bool pinned) => _generator.DeclareLocal(localType, pinned);
    /// <inheritdoc />
    public virtual Label DefineLabel() => _generator.DefineLabel();
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode) => _generator.Emit(opcode);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, byte arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, double arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, float arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, int arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, long arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, sbyte arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, short arg) => _generator.Emit(opcode, arg);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, string str) => _generator.Emit(opcode, str);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, ConstructorInfo con) => _generator.Emit(opcode, con);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, Label label) => _generator.Emit(opcode, label);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, Label[] labels) => _generator.Emit(opcode, labels);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, LocalBuilder local) => _generator.Emit(opcode, local);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, SignatureHelper signature) => _generator.Emit(opcode, signature);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, FieldInfo field) => _generator.Emit(opcode, field);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, MethodInfo meth) => _generator.Emit(opcode, meth);
    /// <inheritdoc />
    public virtual void Emit(OpCode opcode, Type cls) => _generator.Emit(opcode, cls);
    /// <inheritdoc />
    public virtual void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[]? optionalParameterTypes)
        => _generator.EmitCall(opcode, methodInfo, optionalParameterTypes);
    /// <inheritdoc />
    public virtual void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[]? optionalParameterTypes)
        => _generator.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
    /// <inheritdoc />
    public virtual void EmitCalli(OpCode opcode, CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes)
        => _generator.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
    /// <inheritdoc />
    public virtual void EmitWriteLine(string value) => _generator.EmitWriteLine(value);
    /// <inheritdoc />
    public virtual void EmitWriteLine(LocalBuilder localBuilder) => _generator.EmitWriteLine(localBuilder);
    /// <inheritdoc />
    public virtual void EmitWriteLine(FieldInfo fld) => _generator.EmitWriteLine(fld);
    /// <inheritdoc />
    public virtual void EndExceptionBlock() => _generator.EndExceptionBlock();
    /// <inheritdoc />
    public virtual void EndScope() => _generator.EndScope();
    /// <inheritdoc />
    public virtual void MarkLabel(Label loc) => _generator.MarkLabel(loc);

    /// <inheritdoc />
    public virtual void MarkSequencePoint(ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
#if NETFRAMEWORK
        => _generator.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
#else
        => throw new NotSupportedException();
#endif
    /// <inheritdoc />
    public virtual void ThrowException(Type excType) => _generator.ThrowException(excType);
    /// <inheritdoc />
    public virtual void UsingNamespace(string usingNamespace) => _generator.UsingNamespace(usingNamespace);
#if NETFRAMEWORK
    /// <inheritdoc />
    void _ILGenerator.GetIDsOfNames(ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        => ((_ILGenerator)_generator).GetIDsOfNames(ref riid, rgszNames, cNames, lcid, rgDispId);
    /// <inheritdoc />
    void _ILGenerator.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        => ((_ILGenerator)_generator).GetTypeInfo(iTInfo, lcid, ppTInfo);
    /// <inheritdoc />
    void _ILGenerator.GetTypeInfoCount(out uint pcTInfo)
        => ((_ILGenerator)_generator).GetTypeInfoCount(out pcTInfo);
    /// <inheritdoc />
    void _ILGenerator.Invoke(uint dispIdMember, ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr)
        => ((_ILGenerator)_generator).Invoke(dispIdMember, ref riid, lcid, wFlags, pDispParams, pVarResult, pExcepInfo, puArgErr);
#endif
    public static implicit operator ILGeneratorEmitter(ILGenerator generator) => new ILGeneratorEmitter(generator);
    public static implicit operator ILGenerator(ILGeneratorEmitter emitter) => emitter._generator;
}