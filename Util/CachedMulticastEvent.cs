using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.API;
using DevkitServer.API.Abstractions;

namespace DevkitServer.Util;
public class CachedMulticastEvent<TDelegate> where TDelegate : MulticastDelegate
{
    [UsedImplicitly]
    private TDelegate[] _delegates;
    private TDelegate? _multicast;
    public string ErrorMessage { get; set; }
    public string Name { get; }
    public Type DeclaringType { get; }
    public bool IsEmpty
    {
        get
        {
            lock (this)
                return _delegates.Length == 0;
        }
    }

    public TDelegate TryInvoke { get; }
    public bool IsCancellable { get; private set; }
    public bool DefaultShouldAllow { get; }
    public CachedMulticastEvent(Type declaringType, string name, bool shouldAllowDefault = true)
    {
        Name = name;
        _delegates = Array.Empty<TDelegate>();
        DeclaringType = declaringType;
        ErrorMessage = "Caller threw an error in " + DeclaringType.Format() + "." + Name.Colorize(ConsoleColor.White) + ".";
        DefaultShouldAllow = shouldAllowDefault;
        TryInvoke = GetInvokeMethod(this);
    }
    public void TransferTo(CachedMulticastEvent<TDelegate> other, bool union = false)
    {
        lock (this)
        {
            lock (other)
            {
                if (!union)
                {
                    other._multicast = null;
                    other._delegates = Array.Empty<TDelegate>();
                }

                foreach (TDelegate @delegate in _delegates)
                {
                    if (union)
                        other._multicast = (TDelegate?)Delegate.Remove(other._multicast, @delegate);
                    other._multicast = (TDelegate)Delegate.Combine(other._multicast, @delegate);
                }

                Delegate[]? dele = other._multicast?.GetInvocationList();
                if (dele != null)
                {
                    if (other._delegates.Length != dele.Length)
                        other._delegates = new TDelegate[dele.Length];
                    for (int i = 0; i < dele.Length; ++i)
                        other._delegates[i] = (TDelegate)dele[i];
                }
                else other._delegates = Array.Empty<TDelegate>();
            }
        }
    }
    public TDelegate[] GetInvocationList()
    {
        lock (this)
        {
            TDelegate[] newArr = _delegates.Length == 0 ? Array.Empty<TDelegate>() : new TDelegate[_delegates.Length];
            for (int i = 0; i < newArr.Length; ++i)
                newArr[i] = _delegates[i];
            return newArr;
        }
    }
    public void Add(TDelegate @delegate)
    {
        if (@delegate == null) return;
        lock (this)
        {
            _multicast = (TDelegate)Delegate.Combine(_multicast, @delegate);
            Delegate[] dele = _multicast.GetInvocationList();
            if (_delegates.Length != dele.Length)
                _delegates = new TDelegate[dele.Length];
            for (int i = 0; i < dele.Length; ++i)
                _delegates[i] = (TDelegate)dele[i];
        }
    }
    public void Remove(TDelegate @delegate)
    {
        lock (this)
        {
            _multicast = (TDelegate?)Delegate.Remove(_multicast, @delegate);
            Delegate[]? dele = _multicast?.GetInvocationList();
            if (dele != null)
            {
                if (_delegates.Length != dele.Length)
                    _delegates = new TDelegate[dele.Length];
                for (int i = 0; i < dele.Length; ++i)
                    _delegates[i] = (TDelegate)dele[i];
            }
            else _delegates = Array.Empty<TDelegate>();
        }
    }
    public static TDelegate GetInvokeMethod(CachedMulticastEvent<TDelegate> wrapper)
    {
        TDelegate @delegate;
        if (wrapper.DefaultShouldAllow)
        {
            wrapper.IsCancellable = DynamicCancellableTrueMethodProvider.Cancellable;
            @delegate = (TDelegate)DynamicCancellableTrueMethodProvider.Method.CreateDelegate(typeof(TDelegate), wrapper);
        }
        else
        {
            wrapper.IsCancellable = DynamicCancellableFalseMethodProvider.Cancellable;
            @delegate = (TDelegate)DynamicCancellableFalseMethodProvider.Method.CreateDelegate(typeof(TDelegate), wrapper);
        }
        Logger.DevkitServer.LogDebug("CachedMulticastEvent", $"Created cached event try-invoker for {wrapper.DeclaringType.Format()}.{wrapper.Name.Colorize(ConsoleColor.White)} ({@delegate.Method.Format()}).");
        return @delegate;
    }
    private static DynamicMethod GenerateDynamicMethod(Type wrapperType, bool defaultShouldAllowValue, out bool cancellable)
    {
        cancellable = false;
        Type type = typeof(TDelegate);
        MethodInfo invoke = type.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        if (invoke.ReturnType != typeof(void))
            throw new InvalidOperationException("Can't make a multicast event out of a non-void-returning delegate.");
        MethodInfo logError = Accessor.LogErrorException;

        MethodInfo getDeclaringType = wrapperType.GetProperty(nameof(DeclaringType), BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)!.GetMethod!;
        MethodInfo getTypeName = typeof(Type).GetProperty(nameof(Type.Name), BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.GetMethod!;
        MethodInfo getErrorMessage = wrapperType.GetProperty(nameof(ErrorMessage), BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)!.GetMethod!;
        MethodInfo toUpperInvariant = typeof(string).GetMethod(nameof(string.ToUpperInvariant), BindingFlags.Public | BindingFlags.Instance)!;

        ParameterInfo[] expectedParameters = invoke.GetParameters();
        Type[] types = new Type[expectedParameters.Length + 1];
        for (int i = 0; i < expectedParameters.Length; ++i)
            types[i + 1] = expectedParameters[i].ParameterType;
        types[0] = wrapperType;

        int shouldAllowIndex = -1;
        for (int i = expectedParameters.Length - 1; i >= 0; --i)
        {
            ParameterInfo info = expectedParameters[i];
            Type paramType = info.ParameterType;
            if (info.Name.IndexOf("allow", StringComparison.InvariantCultureIgnoreCase) != -1 && paramType.IsByRef && paramType.GetElementType() == typeof(bool))
            {
                shouldAllowIndex = i;
                cancellable = true;
                break;
            }
        }

        FieldInfo field = wrapperType.GetField(nameof(_delegates), BindingFlags.NonPublic | BindingFlags.Instance)!;

        DynamicMethod method = new DynamicMethod("TryInvoke" + type.Name, MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void), types, wrapperType, true);
        IOpCodeEmitter il = method.GetILGenerator().AsEmitter();
        for (int i = 0; i < expectedParameters.Length; ++i)
            method.DefineParameter(i + 2, expectedParameters[i].Attributes, expectedParameters[i].Name);
        method.DefineParameter(1, ParameterAttributes.None, "this");
        il.DeclareLocal(typeof(int));
        il.DeclareLocal(type.MakeArrayType());
        il.DeclareLocal(typeof(string));
        Label loopStartLabel = il.DefineLabel();
        Label incrLbl = il.DefineLabel();
        Label checkLbl = il.DefineLabel();

        // TDelegate[] delegates = this._delegates;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Stloc_1);

        // goto check out of bounds;
        il.Emit(OpCodes.Br, checkLbl);

        il.MarkLabel(loopStartLabel);

        il.BeginExceptionBlock();

        // delegates[i].Invoke(...);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldelem_Ref);
        for (int i = 1; i < types.Length; ++i)
            PatchUtil.EmitArgument(il, i, false, false);

        il.Emit(OpCodes.Callvirt, invoke);

        if (shouldAllowIndex > -1)
        {
            // if (!shouldAllow) return;
            PatchUtil.EmitArgument(il, shouldAllowIndex + 1, false, true);
            il.Emit(defaultShouldAllowValue ? OpCodes.Brtrue_S : OpCodes.Brfalse_S, incrLbl);
            il.Emit(OpCodes.Ret);
        }

        if (!DevkitServerModule.MonoLoaded)
            il.Emit(OpCodes.Leave_S, incrLbl);

        il.BeginCatchBlock(typeof(Exception));

        // LogErrorException(ex, this.ErrorMessage, ConsoleColor.Red, method: method);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(getErrorMessage.GetCallRuntime(), getErrorMessage);

        PatchUtil.LoadConstantI4(il, (int)ConsoleColor.Red);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(getDeclaringType.GetCallRuntime(), getDeclaringType);
        il.Emit(OpCodes.Callvirt, getTypeName);
        il.Emit(toUpperInvariant.GetCallRuntime(), toUpperInvariant);

        il.Emit(logError.GetCallRuntime(), logError);

        if (!DevkitServerModule.MonoLoaded)
            il.Emit(OpCodes.Leave_S, incrLbl);

        il.EndExceptionBlock();

        // ++i
        il.MarkLabel(incrLbl);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc_0);

        // if (i < delegates.Length) return;
        il.MarkLabel(checkLbl);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Blt, loopStartLabel);
        il.Emit(OpCodes.Ret);

        return method;
    }
    private static class DynamicCancellableTrueMethodProvider
    {
        public static DynamicMethod Method { get; }
        public static bool Cancellable { get; }
        static DynamicCancellableTrueMethodProvider()
        {
            Method = GenerateDynamicMethod(typeof(CachedMulticastEvent<TDelegate>), true, out bool c);
            Cancellable = c;
        }
    }
    private static class DynamicCancellableFalseMethodProvider
    {
        public static DynamicMethod Method { get; }
        public static bool Cancellable { get; }
        static DynamicCancellableFalseMethodProvider()
        {
            Method = GenerateDynamicMethod(typeof(CachedMulticastEvent<TDelegate>), false, out bool c);
            Cancellable = c;
        }
    }
}