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
        public static string GetFullyQualifiedName(this MethodSymbol obj)
        {
            if (string.IsNullOrEmpty(obj.Name))
                throw new NotImplementedException("method has no name");

            return obj.Name + obj.Parameters.ToString();
        }

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
    }
}
