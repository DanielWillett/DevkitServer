using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Util;
public static class PatchUtil
{
    [Pure]
    public static IEnumerable<CodeInstruction> Throw<TException>(string? message = null) where TException : Exception
    {
        ConstructorInfo[] ctors = typeof(TException).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        
        ConstructorInfo? info = message == null
            ? ctors.FirstOrDefault(x => x.GetParameters().Length == 0)
            : (ctors.FirstOrDefault(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(string)) ??
               ctors.FirstOrDefault(x => x.GetParameters().Length == 0));

        if (info == null)
            throw new MemberAccessException("Unable to find any constructors for that exception.");
        
        if (info.GetParameters().Length == 1)
        {
            return new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldstr, message),
                new CodeInstruction(OpCodes.Newobj, info),
                new CodeInstruction(OpCodes.Throw)
            };
        }

        return new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Newobj, info),
            new CodeInstruction(OpCodes.Throw)
        };
    }
    public static object? Get(this MemberInfo member, object? instance)
    {
        if (member is PropertyInfo property)
        {
            MethodInfo? getter = property.GetGetMethod(true);
            if (getter == null)
                throw new NotSupportedException(property.Name + " does not have a getter.");
            return getter.Invoke(instance, Array.Empty<object>());
        }

        if (member is FieldInfo field)
            return field.GetValue(instance);

        throw new ArgumentException("Member must be a FieldInfo or PropertyInfo.", nameof(member));
    }
    public static void Set(this MemberInfo member, object? instance, object? value)
    {
        if (member is PropertyInfo property)
        {
            MethodInfo? setter = property.GetSetMethod(true);
            if (setter == null)
                throw new NotSupportedException(property.Name + " does not have a setter.");

            setter.Invoke(instance, new object?[] { value });
        }

        if (member is FieldInfo field)
            field.SetValue(instance, value);

        throw new ArgumentException("Member must be a FieldInfo or PropertyInfo.", nameof(member));
    }
    [Pure]
    public static Type? GetMemberType(this MemberInfo member) => member switch
    {
        MethodInfo a => a.ReturnType,
        FieldInfo a => a.FieldType,
        PropertyInfo a => a.PropertyType,
        ConstructorInfo a => a.DeclaringType,
        EventInfo a => a.EventHandlerType,
        _ => throw new ArgumentException($"Member type {member.GetType().Name} does not have a member type.", nameof(member))
    };
    [Pure]
    public static bool GetIsStatic(this MemberInfo member) => member switch
    {
        MethodBase a => a.IsStatic,
        FieldInfo a => a.IsStatic,
        PropertyInfo a => a.GetGetMethod(true) is { } getter ? getter.IsStatic : (a.GetSetMethod(true) is { } setter && setter.IsStatic),
        EventInfo a => a.GetAddMethod(true) is { } adder ? adder.IsStatic : (a.GetRemoveMethod(true) is { } remover ? remover.IsStatic : (a.GetRaiseMethod(true) is { } raiser && raiser.IsStatic)),
        Type a => (a.Attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.Class)) != 0,
        _ => throw new ArgumentException($"Member type {member.GetType().Name} is not static-able.", nameof(member))
    };
    [Pure]
    public static bool FollowPattern(IList<CodeInstruction> instructions, ref int index, params PatternMatch?[] matches)
    {
        if (MatchPattern(instructions, index, matches))
        {
            index += matches.Length;
            return true;
        }
        return false;
    }
    [Pure]
    public static bool RemovePattern(IList<CodeInstruction> instructions, int index, params PatternMatch?[] matches)
    {
        if (MatchPattern(instructions, index, matches))
        {
            if (instructions is List<CodeInstruction> list)
            {
                list.RemoveRange(index, matches.Length);
            }
            else
            {
                for (int i = 0; i < matches.Length; ++i)
                    instructions.RemoveAt(index);
            }
            return true;
        }
        return false;
    }
    [Pure]
    public static bool MatchPattern(IList<CodeInstruction> instructions, int index, params PatternMatch?[] matches)
    {
        int c = matches.Length;
        if (c <= 0 || index >= instructions.Count - matches.Length)
            return false;
        for (int i = index; i < index + matches.Length; ++i)
        {
            PatternMatch? pattern = matches[i - index];
            if (pattern != null && !pattern.Invoke(instructions[i]))
                return false;
        }
        return true;
    }

    public static void ReturnIfFalse(IList<CodeInstruction> instructions, ILGenerator generator, ref int index, Func<bool> checker, Label? @goto = null)
    {
        Label continueLbl = generator.DefineLabel();
        CodeInstruction instruction = new CodeInstruction(OpCodes.Call, checker.Method);
        instruction.labels.AddRange(instructions[index].labels);
        instructions[index].labels.Clear();
        instructions.Insert(index, instruction);

        instructions.Insert(index + 1, new CodeInstruction(OpCodes.Brtrue, continueLbl));
        instructions.Insert(index + 2, @goto.HasValue ? new CodeInstruction(OpCodes.Br, @goto) : new CodeInstruction(OpCodes.Ret));
        index += 3;
        if (instructions.Count > index)
            instructions[index].labels.Add(continueLbl);
    }
    public static int RemoveUntil(IList<CodeInstruction> instructions, int index, PatternMatch match, bool includeMatch = true)
    {
        int amt = 0;
        for (int i = index; i < instructions.Count; ++i)
        {
            if (match(instructions[i]))
            {
                amt = i - index + (includeMatch ? 1 : 0);
                if (instructions is List<CodeInstruction> list)
                {
                    list.RemoveRange(index, amt);
                }
                else
                {
                    for (int j = 0; j < amt; ++j)
                        instructions.RemoveAt(index);
                }

                break;
            }
        }
        return amt;
    }
    [Pure]
    public static int ContinueUntil(IList<CodeInstruction> instructions, ref int index, PatternMatch match, bool includeMatch = true)
    {
        int amt = 0;
        for (int i = index; i < instructions.Count; ++i)
        {
            ++amt;
            if (match(instructions[i]))
            {
                index = includeMatch ? i : i + 1;
                if (includeMatch)
                    --amt;
                break;
            }
        }
        return amt;
    }
    [Pure]
    public static int ContinueWhile(IList<CodeInstruction> instructions, ref int index, PatternMatch match, bool includeNext = true)
    {
        int amt = 0;
        for (int i = index; i < instructions.Count; ++i)
        {
            ++amt;
            if (!match(instructions[i]))
            {
                index = includeNext ? i : i - 1;
                if (!includeNext)
                    --amt;
                break;
            }
        }
        return amt;
    }
    [Pure]
    public static bool LabelNext(IList<CodeInstruction> instructions, int index, Label label, PatternMatch match, int shift = 0, bool labelRtnIfFailure = false)
    {
        for (int i = index; i < instructions.Count; ++i)
        {
            if (match(instructions[i]))
            {
                int newIndex = i + shift;
                if (instructions.Count > i)
                {
                    instructions[newIndex].labels.Add(label);
                    return true;
                }
            }
        }
        if (labelRtnIfFailure)
        {
            if (instructions[instructions.Count - 1].opcode == OpCodes.Ret)
                instructions[instructions.Count - 1].labels.Add(label);
            else
            {
                CodeInstruction instruction = new CodeInstruction(OpCodes.Ret);
                instruction.labels.Add(label);
                instructions.Add(instruction);
            }
        }
        return false;
    }
    [Pure]
    public static Label? LabelNext(IList<CodeInstruction> instructions, ILGenerator generator, int index, PatternMatch match, int shift = 0)
    {
        for (int i = index; i < instructions.Count; ++i)
        {
            if (match(instructions[i]))
            {
                int newIndex = i + shift;
                if (instructions.Count > i)
                {
                    Label label = generator.DefineLabel();
                    instructions[newIndex].labels.Add(label);
                    return label;
                }
            }
        }
        return null;
    }
    [Pure]
    public static Label LabelNextOrReturn(IList<CodeInstruction> instructions, ILGenerator generator, int index, PatternMatch? match, int shift = 0, bool allowUseExisting = true)
    {
        CodeInstruction instruction;
        for (int i = index; i < instructions.Count; ++i)
        {
            if (match == null || match(instructions[i]))
            {
                int newIndex = i + shift;
                if (instructions.Count > i)
                {
                    instruction = instructions[newIndex];
                    if (allowUseExisting && instruction.labels.Count > 0)
                        return instruction.labels[instruction.labels.Count - 1];
                    Label label = generator.DefineLabel();
                    instruction.labels.Add(label);
                    return label;
                }
            }
        }

        instruction = instructions[instructions.Count - 1];
        if (instruction.opcode == OpCodes.Ret)
        {
            if (allowUseExisting && instruction.labels.Count > 0)
                return instruction.labels[instruction.labels.Count - 1];
            Label label = generator.DefineLabel();
            instruction.labels.Add(label);
            return label;
        }
        else
        {
            Label label = generator.DefineLabel();
            instruction = new CodeInstruction(OpCodes.Ret);
            instruction.labels.Add(label);
            instructions.Add(instruction);
            return label;
        }
    }
    [Pure]
    public static Label? GetNextBranchTarget(IList<CodeInstruction> instructions, int index)
    {
        if (index < 0)
            index = 0;
        for (int i = index; i < instructions.Count; ++i)
        {
            if (instructions[i].Branches(out Label? label) && label.HasValue)
                return label;
        }

        return null;
    }
    [Pure]
    public static int FindLabelDestinationIndex(IList<CodeInstruction> instructions, Label label, int startIndex = 0)
    {
        if (startIndex < 0)
            startIndex = 0;
        for (int i = startIndex; i < instructions.Count; ++i)
        {
            List<Label>? labels = instructions[i].labels;
            if (labels != null)
            {
                for (int j = 0; j < labels.Count; ++j)
                {
                    if (labels[j] == label)
                        return i;
                }
            }
        }

        return -1;
    }
    [Pure]
    public static CodeInstruction GetLocalCodeInstruction(LocalBuilder? builder, int index, bool set, bool byref = false)
    {
        return new CodeInstruction(GetLocalCode(builder != null ? builder.LocalIndex : index, set, byref), builder);
    }
#if CLIENT
    [Obsolete("Fix this it messes up OnKeyUp calls.")]
    /// <summary>Limits to 60 actions per second.</summary>
    public static void InsertActionRateLimiter(ref int offset, Label beginLabel, IList<CodeInstruction> ins)
    {
    }
#endif
    [Pure]
    public static unsafe int GetLabelId(this Label label) => *(int*)&label;
    [Pure]
    public static OpCode GetLocalCode(int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!set && byref)
        {
            return index > byte.MaxValue ? OpCodes.Ldloca : OpCodes.Ldloca_S;
        }
        return index switch
        {
            0 => set ? OpCodes.Stloc_0 : OpCodes.Ldloc_0,
            1 => set ? OpCodes.Stloc_1 : OpCodes.Ldloc_1,
            2 => set ? OpCodes.Stloc_2 : OpCodes.Ldloc_2,
            3 => set ? OpCodes.Stloc_3 : OpCodes.Ldloc_3,
            _ => set ? (index > byte.MaxValue ? OpCodes.Stloc : OpCodes.Stloc_S) : (index > byte.MaxValue ? OpCodes.Ldloc : OpCodes.Ldloc_S)
        };
    }
    [Pure]
    public static OpCode GetArgumentCode(int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (set)
            return index > byte.MaxValue ? OpCodes.Starg : OpCodes.Starg_S;
        if (byref)
            return index > byte.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S;
        
        return index switch
        {
            0 => OpCodes.Ldarg_0,
            1 => OpCodes.Ldarg_1,
            2 => OpCodes.Ldarg_2,
            3 => OpCodes.Ldarg_3,
            _ => index > byte.MaxValue ? OpCodes.Ldarg : OpCodes.Ldarg_S
        };
    }
    public static void EmitArgument(ILGenerator il, int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (set)
        {
            il.Emit(index > byte.MaxValue ? OpCodes.Starg : OpCodes.Starg_S, index);
            return;
        }
        if (byref)
        {
            il.Emit(index > byte.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S, index);
            return;
        }
        
        if (index is < 4 and > -1)
        {
            il.Emit(index switch
            {
                0 => OpCodes.Ldarg_0,
                1 => OpCodes.Ldarg_1,
                2 => OpCodes.Ldarg_2,
                _ => OpCodes.Ldarg_3
            });
            return;
        }

        il.Emit(index > byte.MaxValue ? OpCodes.Ldarg : OpCodes.Ldarg_S, index);
    }
    public static void EmitArgument(DebuggableEmitter il, int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (set)
        {
            il.Emit(index > byte.MaxValue ? OpCodes.Starg : OpCodes.Starg_S, index);
            return;
        }
        if (byref)
        {
            il.Emit(index > byte.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S, index);
            return;
        }
        
        if (index is < 4 and > -1)
        {
            il.Emit(index switch
            {
                0 => OpCodes.Ldarg_0,
                1 => OpCodes.Ldarg_1,
                2 => OpCodes.Ldarg_2,
                _ => OpCodes.Ldarg_3
            });
            return;
        }

        il.Emit(index > byte.MaxValue ? OpCodes.Ldarg : OpCodes.Ldarg_S, index);
    }
    [Pure]
    public static int GetLocalIndex(CodeInstruction code, bool set)
    {
        if (code.opcode.OperandType == OperandType.ShortInlineVar &&
            (set && code.opcode == OpCodes.Stloc_S ||
             !set && code.opcode == OpCodes.Ldloc_S || !set && code.opcode == OpCodes.Ldloca_S))
            return ((LocalBuilder)code.operand).LocalIndex;
        if (code.opcode.OperandType == OperandType.InlineVar &&
            (set && code.opcode == OpCodes.Stloc ||
             !set && code.opcode == OpCodes.Ldloc || !set && code.opcode == OpCodes.Ldloca))
            return ((LocalBuilder)code.operand).LocalIndex;
        if (set)
        {
            if (code.opcode == OpCodes.Stloc_0)
                return 0;
            if (code.opcode == OpCodes.Stloc_1)
                return 1;
            if (code.opcode == OpCodes.Stloc_2)
                return 2;
            if (code.opcode == OpCodes.Stloc_3)
                return 3;
        }
        else
        {
            if (code.opcode == OpCodes.Ldloc_0)
                return 0;
            if (code.opcode == OpCodes.Ldloc_1)
                return 1;
            if (code.opcode == OpCodes.Ldloc_2)
                return 2;
            if (code.opcode == OpCodes.Ldloc_3)
                return 3;
        }

        return -1;
    }
    [Pure]
    public static CodeInstruction LoadConstantI4(int number)
    {
        return number switch
        {
            -1 => new CodeInstruction(OpCodes.Ldc_I4_M1),
            0 => new CodeInstruction(OpCodes.Ldc_I4_0),
            1 => new CodeInstruction(OpCodes.Ldc_I4_1),
            2 => new CodeInstruction(OpCodes.Ldc_I4_2),
            3 => new CodeInstruction(OpCodes.Ldc_I4_3),
            4 => new CodeInstruction(OpCodes.Ldc_I4_4),
            5 => new CodeInstruction(OpCodes.Ldc_I4_5),
            6 => new CodeInstruction(OpCodes.Ldc_I4_6),
            7 => new CodeInstruction(OpCodes.Ldc_I4_7),
            8 => new CodeInstruction(OpCodes.Ldc_I4_8),
            _ => new CodeInstruction(OpCodes.Ldc_I4, number),
        };
    }
    public static void LoadConstantI4(ILGenerator generator, int number)
    {
        OpCode code = number switch
        {
            -1 => OpCodes.Ldc_I4_M1,
            0 => OpCodes.Ldc_I4_0,
            1 => OpCodes.Ldc_I4_1,
            2 => OpCodes.Ldc_I4_2,
            3 => OpCodes.Ldc_I4_3,
            4 => OpCodes.Ldc_I4_4,
            5 => OpCodes.Ldc_I4_5,
            6 => OpCodes.Ldc_I4_6,
            7 => OpCodes.Ldc_I4_7,
            8 => OpCodes.Ldc_I4_8,
            _ => OpCodes.Ldc_I4
        };
        if (number is < -1 or > 8)
            generator.Emit(code, number);
        else
            generator.Emit(code);
    }
    public static void LoadConstantI4(DebuggableEmitter generator, int number)
    {
        OpCode code = number switch
        {
            -1 => OpCodes.Ldc_I4_M1,
            0 => OpCodes.Ldc_I4_0,
            1 => OpCodes.Ldc_I4_1,
            2 => OpCodes.Ldc_I4_2,
            3 => OpCodes.Ldc_I4_3,
            4 => OpCodes.Ldc_I4_4,
            5 => OpCodes.Ldc_I4_5,
            6 => OpCodes.Ldc_I4_6,
            7 => OpCodes.Ldc_I4_7,
            8 => OpCodes.Ldc_I4_8,
            _ => OpCodes.Ldc_I4
        };
        if (number is < -1 or > 8)
            generator.Emit(code, number);
        else
            generator.Emit(code);
    }
    [Pure]
    public static CodeInstruction LoadParameter(int index)
    {
        return index switch
        {
            0 => new CodeInstruction(OpCodes.Ldarg_0),
            1 => new CodeInstruction(OpCodes.Ldarg_1),
            2 => new CodeInstruction(OpCodes.Ldarg_2),
            3 => new CodeInstruction(OpCodes.Ldarg_3),
            < ushort.MaxValue => new CodeInstruction(OpCodes.Ldarg_S, index),
            _ => new CodeInstruction(OpCodes.Ldarg, index)
        };
    }
    public static void LoadParameter(this DebuggableEmitter generator, int index, bool byref = false, Type? type = null, Type? targetType = null)
    {
        if (byref)
        {
            generator.Emit(index > ushort.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S, index);
            return;
        }
        OpCode code = index switch
        {
            0 => OpCodes.Ldarg_0,
            1 => OpCodes.Ldarg_1,
            2 => OpCodes.Ldarg_2,
            3 => OpCodes.Ldarg_3,
            <= ushort.MaxValue => OpCodes.Ldarg_S,
            _ => OpCodes.Ldarg
        };
        if (index > 3)
            generator.Emit(code, index);
        else
            generator.Emit(code);
        if (type != null && targetType != null && type != typeof(void) && targetType != typeof(void))
        {
            if (type.IsValueType && !targetType.IsValueType)
                generator.Emit(OpCodes.Box, type);
            else if (!type.IsValueType && targetType.IsValueType)
                generator.Emit(OpCodes.Unbox_Any, targetType);
        }
    }
    public static void LoadParameter(this ILGenerator generator, int index, bool byref = false, Type? type = null, Type? targetType = null)
    {
        if (byref)
        {
            generator.Emit(index > ushort.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S, index);
            return;
        }
        OpCode code = index switch
        {
            0 => OpCodes.Ldarg_0,
            1 => OpCodes.Ldarg_1,
            2 => OpCodes.Ldarg_2,
            3 => OpCodes.Ldarg_3,
            <= ushort.MaxValue => OpCodes.Ldarg_S,
            _ => OpCodes.Ldarg
        };
        if (index > 3)
            generator.Emit(code, index);
        else
            generator.Emit(code);
        if (type != null && targetType != null && type != typeof(void) && targetType != typeof(void))
        {
            if (type.IsValueType && !targetType.IsValueType)
                generator.Emit(OpCodes.Box, type);
            else if (!type.IsValueType && targetType.IsValueType)
                generator.Emit(OpCodes.Unbox_Any, targetType);
        }
    }
    [Pure]
    public static LocalBuilder? GetLocal(CodeInstruction code, out int index, bool set)
    {
        if (code.opcode.OperandType == OperandType.ShortInlineVar &&
            (set && code.opcode == OpCodes.Stloc_S ||
             !set && code.opcode == OpCodes.Ldloc_S || !set && code.opcode == OpCodes.Ldloca_S))
        {
            LocalBuilder bld = (LocalBuilder)code.operand;
            index = bld.LocalIndex;
            return bld;
        }
        if (code.opcode.OperandType == OperandType.InlineVar &&
            (set && code.opcode == OpCodes.Stloc ||
             !set && code.opcode == OpCodes.Ldloc || !set && code.opcode == OpCodes.Ldloca))
        {
            LocalBuilder bld = (LocalBuilder)code.operand;
            index = bld.LocalIndex;
            return bld;
        }
        if (set)
        {
            if (code.opcode == OpCodes.Stloc_0)
            {
                index = 0;
                return null;
            }
            if (code.opcode == OpCodes.Stloc_1)
            {
                index = 1;
                return null;
            }
            if (code.opcode == OpCodes.Stloc_2)
            {
                index = 2;
                return null;
            }
            if (code.opcode == OpCodes.Stloc_3)
            {
                index = 3;
                return null;
            }
        }
        else
        {
            if (code.opcode == OpCodes.Ldloc_0)
            {
                index = 0;
                return null;
            }
            if (code.opcode == OpCodes.Ldloc_1)
            {
                index = 1;
                return null;
            }
            if (code.opcode == OpCodes.Ldloc_2)
            {
                index = 2;
                return null;
            }
            if (code.opcode == OpCodes.Ldloc_3)
            {
                index = 3;
                return null;
            }
        }

        index = -1;
        return null;
    }
    [Pure]
    public static CodeInstruction CopyWithoutSpecial(this CodeInstruction instruction) => new CodeInstruction(instruction.opcode, instruction.operand);
    public static void TransferEndingInstructionNeeds(CodeInstruction originalEnd, CodeInstruction newEnd)
    {
        newEnd.blocks.AddRange(originalEnd.blocks.Where(x => x.blockType.IsEndBlockType()));
        originalEnd.blocks.RemoveAll(x => x.blockType.IsEndBlockType());
    }
    public static void TransferStartingInstructionNeeds(CodeInstruction originalStart, CodeInstruction newStart)
    {
        newStart.labels.AddRange(originalStart.labels);
        originalStart.labels.Clear();
        newStart.blocks.AddRange(originalStart.blocks.Where(x => x.blockType.IsBeginBlockType()));
        originalStart.blocks.RemoveAll(x => x.blockType.IsBeginBlockType());
    }
    public static void MoveBlocksAndLabels(this CodeInstruction from, CodeInstruction to)
    {
        to.labels.AddRange(from.labels);
        from.labels.Clear();
        to.blocks.AddRange(from.blocks);
        from.blocks.Clear();
    }

    [Pure]
    public static bool IsBeginBlockType(this ExceptionBlockType type) => type is ExceptionBlockType.BeginCatchBlock
        or ExceptionBlockType.BeginExceptFilterBlock or ExceptionBlockType.BeginExceptionBlock
        or ExceptionBlockType.BeginFaultBlock or ExceptionBlockType.BeginFinallyBlock;

    [Pure]
    public static bool IsEndBlockType(this ExceptionBlockType type) => type == ExceptionBlockType.EndExceptionBlock;

    [Pure]
    public static bool IsOfType(this OpCode opcode, OpCode comparand, bool fuzzy = false)
    {
        if (opcode == comparand)
            return true;
        if (opcode.IsStArg())
            return comparand.IsStArg();
        if (opcode.IsStLoc())
            return comparand.IsStLoc();
        if (!fuzzy)
        {
            if (opcode.IsLdArg())
                return comparand.IsLdArg();
            if (opcode.IsLdArg(true))
                return comparand.IsLdArg(true);
            if (opcode.IsLdLoc())
                return comparand.IsLdLoc();
            if (opcode.IsLdLoc(true))
                return comparand.IsLdLoc(true);
            if (opcode.IsLdc())
                return comparand.IsLdc();
            if (opcode.IsLdc(false, true))
                return comparand.IsLdc(false, true);
            if (opcode.IsLdc(false, false, true))
                return comparand.IsLdc(false, false, true);
            if (opcode.IsLdc(false, false, false, true))
                return comparand.IsLdc(false, false, false, true);
            if (opcode.IsLdc(false, false, false, false, true))
                return comparand.IsLdc(false, false, false, false, true);
            if (opcode.IsLdc(false, false, false, false, false, true))
                return comparand.IsLdc(false, false, false, false, false, true);
            if (opcode.IsBr(true))
                return comparand.IsBr(true);
            if (opcode.IsBr(false, true))
                return comparand.IsBr(false, true);
            if (opcode.IsBr(false, false, true))
                return comparand.IsBr(false, false, true);
            if (opcode.IsBr(false, false, false, true))
                return comparand.IsBr(false, false, false, true);
            if (opcode.IsBr(false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, false, false, false, true);
        }
        else
        {
            if (opcode.IsLdArg(true, true))
                return comparand.IsLdArg(true, true);
            if (opcode.IsLdLoc(true, true))
                return comparand.IsLdLoc(true, true);
            if (opcode.IsLdc(true, true, true, true, true, true))
                return comparand.IsLdc(true, true, true, true, true, true);
            if (opcode.IsBr(true, true, true, true, true, true, true, true, true))
                return comparand.IsBr(true, true, true, true, true, true, true, true, true);
            if (opcode.IsConv(true, false, false, false, false, false, false))
                return comparand.IsConv(true, false, false, false, false, false, false);
            if (opcode.IsConv(false, true, false, false, false, false, false))
                return comparand.IsConv(false, true, false, false, false, false, false);
            if (opcode.IsConv(false, false, true, false, false, false, false))
                return comparand.IsConv(false, false, true, false, false, false, false);
            if (opcode.IsConv(false, false, false, true, false, false, false))
                return comparand.IsConv(false, false, false, true, false, false, false);
            if (opcode.IsConv(false, false, false, false, true, false, false))
                return comparand.IsConv(false, false, false, false, true, false, false);
            if (opcode.IsConv(false, false, false, false, false, true, false))
                return comparand.IsConv(false, false, false, false, false, true, false);
            if (opcode.IsConv(false, false, false, false, false, false, true))
                return comparand.IsConv(false, false, false, false, false, false, true);
        }

        return false;
    }

    [Pure]
    public static bool IsStLoc(this OpCode opcode)
    {
        return opcode == OpCodes.Stloc || opcode == OpCodes.Stloc_S || opcode == OpCodes.Stloc_0 || opcode == OpCodes.Stloc_1 || opcode == OpCodes.Stloc_2 || opcode == OpCodes.Stloc_3;
    }
    [Pure]
    public static bool IsLdLoc(this OpCode opcode, bool address = false, bool either = false)
    {
        if (opcode == OpCodes.Ldloc_S || opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1 || opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3 || opcode == OpCodes.Ldloc)
            return !address || either;
        if (opcode == OpCodes.Ldloca_S || opcode == OpCodes.Ldloca)
            return address || either;

        return false;
    }
    [Pure]
    public static bool IsStArg(this OpCode opcode)
    {
        return opcode == OpCodes.Starg || opcode == OpCodes.Starg_S;
    }
    [Pure]
    public static bool IsLdArg(this OpCode opcode, bool address = false, bool either = false)
    {
        if (opcode == OpCodes.Ldarg_S || opcode == OpCodes.Ldarg_0 || opcode == OpCodes.Ldarg_1 || opcode == OpCodes.Ldarg_2 || opcode == OpCodes.Ldarg_3 || opcode == OpCodes.Ldarg)
            return !address || either;
        if (opcode == OpCodes.Ldarga_S || opcode == OpCodes.Ldarga)
            return address || either;

        return false;
    }

    [Pure]
    public static bool IsBrAny(this OpCode opcode, bool br = true, bool brtrue = true, bool brfalse = true,
        bool beq = true, bool bne = true, bool bge = true, bool ble = true, bool bgt = true, bool blt = true)
        => opcode.IsBr(br, brtrue, brfalse, beq, bne, bge, ble, bgt, blt);
    [Pure]
    public static bool IsBr(this OpCode opcode, bool br = false, bool brtrue = false, bool brfalse = false, bool beq = false, bool bne = false, bool bge = false, bool ble = false, bool bgt = false, bool blt = false)
    {
        if (opcode == OpCodes.Br_S || opcode == OpCodes.Br)
            return br;
        if (opcode == OpCodes.Brtrue_S || opcode == OpCodes.Brtrue)
            return brtrue;
        if (opcode == OpCodes.Brfalse_S || opcode == OpCodes.Brfalse)
            return brfalse;
        if (opcode == OpCodes.Beq_S || opcode == OpCodes.Beq)
            return beq;
        if (opcode == OpCodes.Bne_Un_S || opcode == OpCodes.Bne_Un)
            return bne;
        if (opcode == OpCodes.Bge_S || opcode == OpCodes.Bge || opcode == OpCodes.Bge_Un_S || opcode == OpCodes.Bge_Un)
            return bge;
        if (opcode == OpCodes.Ble_S || opcode == OpCodes.Ble || opcode == OpCodes.Ble_Un_S || opcode == OpCodes.Ble_Un)
            return ble;
        if (opcode == OpCodes.Bgt_S || opcode == OpCodes.Bgt || opcode == OpCodes.Bgt_Un_S || opcode == OpCodes.Bgt_Un)
            return bgt;
        if (opcode == OpCodes.Blt_S || opcode == OpCodes.Blt || opcode == OpCodes.Blt_Un_S || opcode == OpCodes.Blt_Un)
            return blt;

        return false;
    }
    [Pure]
    public static bool IsLdc(this OpCode opcode, bool @int = true, bool @long = false, bool @float = false, bool @double = false, bool @string = false, bool @null = false)
    {
        if (opcode == OpCodes.Ldc_I4_0 || opcode == OpCodes.Ldc_I4_1 || opcode == OpCodes.Ldc_I4_S ||
            opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_2 || opcode == OpCodes.Ldc_I4_3 ||
            opcode == OpCodes.Ldc_I4_4 || opcode == OpCodes.Ldc_I4_5 || opcode == OpCodes.Ldc_I4_6 ||
            opcode == OpCodes.Ldc_I4_7 || opcode == OpCodes.Ldc_I4_8 || opcode == OpCodes.Ldc_I4_M1)
            return @int;
        if (opcode == OpCodes.Ldc_R4)
            return @float;
        if (opcode == OpCodes.Ldc_R8)
            return @double;
        if (opcode == OpCodes.Ldc_I8)
            return @long;
        if (opcode == OpCodes.Ldstr)
            return @string;
        if (opcode == OpCodes.Ldnull)
            return @null;

        return false;
    }
    [Pure]
    public static bool IsConv(this OpCode opcode, bool @nint = true, bool @byte = true, bool @short = true, bool @int = true, bool @long = true, bool @float = true, bool @double = true, bool fromUnsigned = true, bool toUnsigned = true, bool signed = true, bool overflowCheck = true, bool noOverflowCheck = true)
    {
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I || toUnsigned && opcode == OpCodes.Conv_U) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I || fromUnsigned && opcode == OpCodes.Conv_Ovf_I_Un))
            return @nint;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I1 || toUnsigned && opcode == OpCodes.Conv_U1) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I1 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I1_Un))
            return @byte;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I2 || toUnsigned && opcode == OpCodes.Conv_U2) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I2 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I2_Un))
            return @short;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I4 || toUnsigned && opcode == OpCodes.Conv_U4) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I4 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I4_Un))
            return @int;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I8 || toUnsigned && opcode == OpCodes.Conv_U8) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I8 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I8_Un))
            return @long;
        if (noOverflowCheck && (opcode == OpCodes.Conv_R4 || fromUnsigned && opcode == OpCodes.Conv_R_Un))
            return @float;
        if (noOverflowCheck && opcode == OpCodes.Conv_R8)
            return @double;

        return false;
    }
    [Pure]
    public static bool ShouldCallvirt(this MethodBase method)
    {
        return method is { IsStatic: false, DeclaringType: not { IsValueType: true }, IsFinal: false } && (method.DeclaringType is { IsInterface: true } || method.IsVirtual || method.IsAbstract);
    }
    [Pure]
    public static OpCode GetCall(this MethodBase method)
    {
        return method.ShouldCallvirt() ? OpCodes.Callvirt : OpCodes.Call;
    }
    public static void CheckCopiedMethodPatchOutOfDate(ref MethodInfo original, MethodBase invoker)
    {
        if (original == null)
            return;
        if (invoker == null)
        {
            Logger.LogWarning("Method invoker not found: " + original.Format() + ".", method: "CLIENT EVENTS");
            original = null!;
            return;
        }
        ParameterInfo[] p = original.GetParameters();
        ParameterInfo[] p2 = invoker.GetParameters();
        bool instanceMatches = !original.IsStatic && original.DeclaringType != null && p2.Length > 0 &&
                               (p2[0].ParameterType.IsAssignableFrom(original.DeclaringType) ||
                                p2[0].ParameterType.IsByRef && p2[0].ParameterType.GetElementType()!
                                    .IsAssignableFrom(original.DeclaringType));
        if (p.Length != p2.Length)
        {
            if (!instanceMatches || p.Length != p2.Length - 1)
            {
                Logger.LogWarning("Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".", method: "CLIENT EVENTS");

                original = null!;
                return;
            }
        }

        int invOffset = instanceMatches ? 1 : 0;

        for (int i = 0; i < p.Length; ++i)
        {
            if (!p2[i + invOffset].ParameterType.IsAssignableFrom(p[i].ParameterType))
            {
                if (p[i].ParameterType.IsByRef &&
                    p2[i + invOffset].ParameterType.IsAssignableFrom(p[i].ParameterType.GetElementType()))
                    continue;
                if (p2[i + invOffset].ParameterType.IsByRef &&
                    p2[i + invOffset].ParameterType.GetElementType()!.IsAssignableFrom(p[i].ParameterType))
                    continue;
                Logger.LogWarning("Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".", method: "CLIENT EVENTS");
                original = null!;
                return;
            }
        }
    }
}
public delegate bool PatternMatch(CodeInstruction instruction);