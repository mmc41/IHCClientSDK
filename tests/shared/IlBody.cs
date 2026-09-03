using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Ihc.Tests
{
    /// <summary>
    /// A method body decoded instruction by instruction, with <c>call</c> targets resolved.
    ///
    /// <para><b>Why the containment gates read IL rather than the ArchUnitNET model.</b> That model carries no
    /// compiler-generated types, so a state machine's or a lambda's calls are attributed to the AUTHORED member
    /// they were written in. That is right for a type-level rule and wrong for a rule about one body: a lambda's
    /// calls merge with its enclosing method's, and both gates below are statements about a single body.</para>
    ///
    /// <para>The walk steps by each opcode's declared operand size. Scanning for call bytes without decoding
    /// lengths would read operand bytes as opcodes and invent instructions that are not there — which for a
    /// containment gate means inventing the very evidence it passes or fails on.</para>
    /// </summary>
    internal static class IlBody
    {
        internal readonly record struct Instruction(OpCode Op, MethodBase? Called);

        private static readonly Dictionary<short, OpCode> ByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => op.Value);

        internal static IEnumerable<Instruction> Instructions(MethodBase method)
        {
            byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
            if (il is null)
            {
                yield break;
            }
            Type[] typeArguments = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericArguments()
                : Type.EmptyTypes;

            int at = 0;
            while (at < il.Length)
            {
                short value = il[at] == 0xFE ? (short)(0xFE00 | il[at + 1]) : il[at];
                at += il[at] == 0xFE ? 2 : 1;
                if (!ByValue.TryGetValue(value, out OpCode op))
                {
                    yield break;   // an opcode this runtime does not know: stop rather than guess at lengths
                }
                MethodBase? called = null;
                if (op.OperandType == OperandType.InlineMethod)
                {
                    try
                    {
                        called = method.Module.ResolveMethod(
                            BitConverter.ToInt32(il, at), typeArguments, Type.EmptyTypes);
                    }
                    catch (ArgumentException)
                    {
                        // A token this module cannot resolve is not a call a rule can judge.
                    }
                }
                yield return new Instruction(op, called);
                at += OperandSize(op, il, at);
            }
        }

        /// <summary>The methods a body calls.</summary>
        internal static IEnumerable<MethodBase> CalledMethods(MethodBase method) =>
            Instructions(method).Select(i => i.Called).OfType<MethodBase>();

        /// <summary>True when the method's declared return type is awaitable in the shape these gates care about.</summary>
        internal static bool ReturnsTask(MethodBase called) =>
            called is MethodInfo { ReturnType: { } returned } &&
            (returned == typeof(System.Threading.Tasks.Task) ||
             returned == typeof(System.Threading.Tasks.ValueTask) ||
             (returned.IsGenericType &&
              (returned.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>) ||
               returned.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>))));

        private static int OperandSize(OpCode op, byte[] il, int at) => op.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, at)),
            _ => 4,
        };
    }
}
