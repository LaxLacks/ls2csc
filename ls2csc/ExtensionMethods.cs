using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LS2IL;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace LS2IL
{
    static class ExtensionMethods
    {
        /// <summary>
        /// Get the LS2IL Fully-Qualified Name of a Method
        /// </summary>
        /// <param name="obj">a Method</param>
        /// <returns>the LS2IL Fully-Qualified Name</returns>
        public static string GetFullyQualifiedName(this MethodSymbol obj)
        {
            if (string.IsNullOrEmpty(obj.Name))
                throw new NotImplementedException("method has no name");

            return obj.Name + obj.Parameters.ToString();
        }

        /// <summary>
        /// Get the LS2IL Fully-Qualified Name of a Namespace
        /// Well not really, they don't exist in LS2IL-land, but if they did, this would be their FQN. We need this to build Type FQNs.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetFullyQualifiedName(this NamespaceSymbol obj)
        {
            string parentname = string.Empty;

            if (obj.ContainingType != null)
                parentname = GetFullyQualifiedName(obj.ContainingType);
            else if (obj.ContainingNamespace != null)
                parentname = GetFullyQualifiedName(obj.ContainingNamespace);
           
            if (!string.IsNullOrEmpty(parentname))
                return parentname + "." + obj.Name;
            return obj.Name;
        }

        /// <summary>
        /// Get the LS2IL Fully-Qualfiied Name of a Type
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetFullyQualifiedName(this TypeSymbol obj)
        {
            string objName = obj.Name;
            if (string.IsNullOrEmpty(objName))
            {
                if (obj.TypeKind == TypeKind.ArrayType)
                {
                    return "System.Array";
                }
                else
                    throw new NotImplementedException("un-named TypeSymbol");
            }
            string parentname = string.Empty;

            if (obj.ContainingType != null)
                parentname = GetFullyQualifiedName(obj.ContainingType);
            else if (obj.ContainingNamespace!=null)
                parentname = GetFullyQualifiedName(obj.ContainingNamespace);

            if (!string.IsNullOrEmpty(parentname))
                return parentname + "." + obj.Name;

            return "LazyProgrammer." + obj.Name;
        }

        /// <summary>
        /// If the Method is declared as having the LS2Intrinsic Attribute, this provides the Name. Intrinsics are built into the compiler, and probably resolve to something other than a method call.
        /// </summary>
        /// <param name="obj">the Method to check for LS2Intrinsic</param>
        /// <param name="intrinsic">if the Method is intrinsic, returns the intrinsic Name for the intrinsics table</param>
        /// <returns>true if the Method is intrinsic</returns>
        public static bool GetIntrinsic(this MethodSymbol obj, out string intrinsic)
        {
            intrinsic = null;
            ReadOnlyArray<AttributeData> attribs = obj.GetAttributes();
            foreach (AttributeData ad in attribs)
            {
                NamedTypeSymbol ac = ad.AttributeClass;
                if (ad.AttributeClass.Name == "LS2Intrinsic")
                {
                    TypedConstant tc = ad.ConstructorArguments.Single();
                    intrinsic = tc.Value as string;
                    return true;
                }
                ad.ToString();
            }

            return false;
        }

        /// <summary>
        /// If the Property is declared as having the LS2Intrinsic Attribute, this provides the Name. Intrinsics are built into the compiler, and probably resolve to something other than a property get.
        /// </summary>
        /// <param name="obj">the Property to check for LS2Intrinsic</param>
        /// <param name="intrinsic">if the Property is intrinsic, returns the intrinsic Name for the intrinsics table</param>
        /// <returns>true if the Property is intrinsic</returns>
        public static bool GetIntrinsic(this PropertySymbol obj, out string intrinsic)
        {
            intrinsic = null;
            ReadOnlyArray<AttributeData> attribs = obj.GetAttributes();
            foreach (AttributeData ad in attribs)
            {
                NamedTypeSymbol ac = ad.AttributeClass;
                if (ad.AttributeClass.Name == "LS2Intrinsic")
                {
                    TypedConstant tc = ad.ConstructorArguments.Single();
                    intrinsic = tc.Value as string;
                    return true;
                }
                ad.ToString();
            }

            return false;
        }

        /// <summary>
        /// Determines if the binary expression is a kind of Assignment
        /// </summary>
        /// <param name="bes"></param>
        /// <returns>true if the binary expression is a kind of Assignment</returns>
        public static bool IsAssignment(this BinaryExpressionSyntax bes)
        {
            switch (bes.Kind)
            {
                case SyntaxKind.AssignExpression:
                case SyntaxKind.AddAssignExpression:
                case SyntaxKind.AndAssignExpression:
                case SyntaxKind.DivideAssignExpression:
                case SyntaxKind.ExclusiveOrAssignExpression:
                case SyntaxKind.LeftShiftAssignExpression:
                case SyntaxKind.ModuloAssignExpression:
                case SyntaxKind.MultiplyAssignExpression:
                case SyntaxKind.OrAssignExpression:
                case SyntaxKind.RightShiftAssignExpression:
                case SyntaxKind.SubtractAssignExpression:
                    return true;
            }
            return false;
        }

        public static bool IsBasicBlockExit(this Instruction instr)
        {
            switch (instr)
            {
                case Instruction.JE:
                case Instruction.JG:
                case Instruction.JGE:
                case Instruction.JL:
                case Instruction.JLE:
                case Instruction.JMP:
                case Instruction.JNE:
                case Instruction.JNZ:
                case Instruction.JZ:
                case Instruction.THROW:
                case Instruction.SWITCH:
                case Instruction.RETURN:
                case Instruction.LEAVE:
                case Instruction.ENDFINALLY:
                    return true;
            }

            return false;
        }

        public static bool HasLValue(this Instruction instr)
        {
            switch (instr)
            {
                case Instruction.NULLIFY: // technically it has two.
                    return true;
                case Instruction.DUPLICATE:
                case Instruction.REFERENCE:
                case Instruction.REREFERENCE:
                case Instruction.DEREFERENCE:
                    return true;
                case Instruction.NOP:
                case Instruction.JE:
                case Instruction.JNE:
                case Instruction.JZ:
                case Instruction.JNZ:
                case Instruction.JL:
                case Instruction.JLE:
                case Instruction.JG:
                case Instruction.JGE:
                case Instruction.JMP:
                case Instruction.SWITCH:
                    return false;
                case Instruction.RETURN:
                case Instruction.CALL:
                case Instruction.FASTCALL:
                case Instruction.CALLMETHOD:
                case Instruction.CALLSTATICMETHOD:
                case Instruction.FASTCALLMETHOD:
                case Instruction.FASTCALLSTATICMETHOD:
                    return false;
                case Instruction.SETPROPERTY:
                case Instruction.ARRAYSET:
                case Instruction.TABLESET:
                case Instruction.SETFIELD:
                case Instruction.SETSTATICFIELD:
                case Instruction.SETSTATICPROPERTY:
                    return false;
                case Instruction.YIELD:
                case Instruction.ATOMIZE:
                case Instruction.DEATOMIZE:
                case Instruction.THROW:
                case Instruction.UNTHROW:
                case Instruction.TRY:
                case Instruction.LEAVE:
                case Instruction.ENDFINALLY:
                case Instruction.meta_LABEL:
                    return false;
            }
            return true;
        }
    }
}
