using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Util;
public class StackTracker
{
    private static readonly InstanceGetter<SignatureHelper, Type[]>? GetArguments = Accessor.GenerateInstanceGetter<SignatureHelper, Type[]>("arguments");
    private static readonly InstanceGetter<SignatureHelper, Type>? GetReturnType = Accessor.GenerateInstanceGetter<SignatureHelper, Type>("returnType");
    private readonly List<CodeInstruction> _instructions;
    private int _lastStackSizeIs0;
    public StackTracker(List<CodeInstruction> instructions)
    {
        _instructions = instructions;
    }
    public static int GetStackChange(OpCode code, object? operand, MethodBase owningMethod)
    {
        int pop;
        if (code.StackBehaviourPop == StackBehaviour.Varpop)
        {
            switch (operand)
            {
                case MethodBase method when code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Newobj:
                    pop = method.GetParameters().Length;
                    if (!method.IsStatic && method is MethodInfo)
                        ++pop;
                    break;
                case SignatureHelper method when code == OpCodes.Calli && GetArguments != null:
                    pop = GetArguments(method).Length;
                    break;
                default:
                    if (code == OpCodes.Ret && owningMethod is MethodInfo m && m.ReturnType != typeof(void))
                        pop = 1;
                    else pop = 0;
                    break;
            }
        }
        else pop = Get(code.StackBehaviourPop);

        int push;
        if (code.StackBehaviourPush == StackBehaviour.Varpush)
        {
            switch (operand)
            {
                case MethodBase method when code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Newobj:
                    if (method is ConstructorInfo || method is MethodInfo m2 && m2.ReturnType != typeof(void))
                        push = 1;
                    else push = 0;
                    break;
                case SignatureHelper method when code == OpCodes.Calli && GetReturnType != null:
                    if (GetReturnType(method) != typeof(void))
                        push = 1;
                    else push = 0;
                    break;
                default:
                    push = 0;
                    break;
            }
        }
        else push = Get(code.StackBehaviourPush);

        return push - pop;

        int Get(StackBehaviour b)
        {
            switch (b)
            {
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Popref:
                case StackBehaviour.Pushref:
                    return 1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                case StackBehaviour.Push1_push1:
                    return 2;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;
                default: return 0;
            }
        }
    }
    public int GetLastUnconsumedIndex(int startIndex, OpCode code, MethodBase method, object? operand = null)
    {
        int stackSize = 0;
        int lastStack = _lastStackSizeIs0;
        if (_lastStackSizeIs0 >= startIndex)
            lastStack = 0;

        int last = -1;
        for (int i = lastStack; i < _instructions.Count; ++i)
        {
            CodeInstruction current = _instructions[i];
            if ((current.opcode == OpCodes.Br || current.opcode == OpCodes.Br_S) && stackSize != 0 && current.operand is Label lbl)
            {
                int index = _instructions.FindIndex(i, x => x.labels.Contains(lbl));
                if (index != -1)
                {
                    i = index - 1;
                    continue;
                }
            }
            if (stackSize == 0)
            {
                lastStack = _lastStackSizeIs0;
                _lastStackSizeIs0 = i;
                if (current.opcode == code && (operand == null || OperandsEqual(current.operand, operand)))
                {
                    if (i >= startIndex)
                    {
                        _lastStackSizeIs0 = lastStack;
                        return last;
                    }
                    last = i;
                }
            }
            stackSize += GetStackChange(current.opcode, current.operand, method);
        }
        return last;
    }
    public int GetLastUnconsumedIndex(int startIndex, Predicate<OpCode> codeFilter, MethodBase method, object? operand = null)
    {
        int stackSize = 0;
        int lastStack = _lastStackSizeIs0;
        if (_lastStackSizeIs0 >= startIndex)
            lastStack = 0;

        int last = -1;
        for (int i = lastStack; i < _instructions.Count; ++i)
        {
            CodeInstruction current = _instructions[i];
            if ((current.opcode == OpCodes.Br || current.opcode == OpCodes.Br_S) && stackSize != 0 && current.operand is Label lbl)
            {
                int index = _instructions.FindIndex(i, x => x.labels.Contains(lbl));
                if (index != -1)
                {
                    i = index - 1;
                    continue;
                }
            }
            if (stackSize == 0)
            {
                lastStack = _lastStackSizeIs0;
                _lastStackSizeIs0 = i;
                if (codeFilter(current.opcode) && (operand == null || OperandsEqual(current.operand, operand)))
                {
                    if (i >= startIndex)
                    {
                        _lastStackSizeIs0 = lastStack;
                        return last;
                    }
                    last = i;
                }
            }
            stackSize += GetStackChange(current.opcode, current.operand, method);
        }
        return last;
    }

    private static bool OperandsEqual(object? left, object? right)
    {
        if (left == null)
            return right == null;
        return right != null && left.Equals(right);
    }
}
