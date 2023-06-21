using JetBrains.Annotations;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Util;
public class CachedMulticastEvent<TDelegate> where TDelegate : MulticastDelegate
{
    [UsedImplicitly]
    private TDelegate[] _delegates;
    private TDelegate? _multicast;
    public string ErrorMessage { get; set; }
    public string Name { get; }
    public Type DeclaringType { get; }
    public bool IsEmpty => Invocations.Length == 0;
    public TDelegate TryInvoke { get; }
    public bool IsCancellable { get; private set; }
    public bool DefaultShouldAllow { get; }
    public TDelegate[] Invocations
    {
        get => _delegates;
        private set => _delegates = value;
    }
    public CachedMulticastEvent(Type declaringType, string name, bool shouldAllowDefault = true)
    {
        Name = name;
        _delegates = Array.Empty<TDelegate>();
        DeclaringType = declaringType;
        ErrorMessage = "Caller threw an error in " + DeclaringType.Format() + "." + Name.Colorize(ConsoleColor.White) + ".";
        DefaultShouldAllow = shouldAllowDefault;
        TryInvoke = GetInvokeMethod(this);
    }

    public void Add(TDelegate @delegate)
    {
        if (@delegate == null) return;
        lock (this)
        {
            _multicast = (TDelegate)Delegate.Combine(_multicast, @delegate);
            Delegate[] dele = _multicast.GetInvocationList();
            Invocations = new TDelegate[dele.Length];
            for (int i = 0; i < dele.Length; ++i)
                Invocations[i] = (TDelegate)dele[i];
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
                Invocations = new TDelegate[dele.Length];
                for (int i = 0; i < dele.Length; ++i)
                    Invocations[i] = (TDelegate)dele[i];
            }
            else Invocations = Array.Empty<TDelegate>();
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
        Logger.LogDebug($"Created cached event try-invoker for {wrapper.DeclaringType.Format()}.{wrapper.Name.Colorize(ConsoleColor.White)} ({@delegate.Method.Format()}).");
        return @delegate;
    }
    private static DynamicMethod GenerateDynamicMethod(Type wrapperType, bool defaultShouldAllowValue, out bool cancellable)
    {
        cancellable = false;
        Type type = typeof(TDelegate);
        MethodInfo invoke = type.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        if (invoke.ReturnType != typeof(void))
            throw new InvalidOperationException("Can't make a multicast event out of a non-void-returning delegate.");
        MethodInfo logErrorText = typeof(Logger).GetMethod(nameof(Logger.LogError), new Type[] { typeof(string), typeof(ConsoleColor), typeof(string) })!;
        MethodInfo logErrorEx = typeof(Logger).GetMethod(nameof(Logger.LogError), new Type[] { typeof(Exception), typeof(bool), typeof(string) })!;

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
        ILGenerator il = method.GetILGenerator();
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
        
        // string method = this.DeclaringType.Name.ToUpperInvariant()
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, getDeclaringType);
        il.Emit(OpCodes.Callvirt, getTypeName);
        il.Emit(OpCodes.Call, toUpperInvariant);
        il.Emit(OpCodes.Stloc_2);

        // Logger.LogError(this.ErrorMessage, method: method);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, getErrorMessage);
        PatchUtil.LoadConstantI4(il, (int)ConsoleColor.Red);
        il.Emit(OpCodes.Ldloc_2);
        il.Emit(OpCodes.Call, logErrorText);

        // Logger.LogError(ex, method: method);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ldloc_2);
        il.Emit(OpCodes.Call, logErrorEx);

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