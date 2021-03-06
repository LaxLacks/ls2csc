﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LS2IL;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ls2csc
{
    abstract class IntrinsicMethod
    {
        public abstract bool IsStatic { get; }

        /*
        public virtual FlatOperand Resolve(InvocationExpressionSyntax node, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions)
        {
            return Resolve(node.Expression, node.ArgumentList, result_type, si, into_lvalue, function, instructions);
        }
        /**/

        public abstract FlatOperand Resolve(ExpressionSyntax expression, ArgumentListSyntax argumentList, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions);
    }

    abstract class IntrinsicProperty
    {
        public abstract bool IsStatic { get; }

        public abstract FlatOperand Resolve(MemberAccessExpressionSyntax node, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions);
    }

    static class Intrinsics
    {
        public static IntrinsicMethod ResolveMethod(string name)
        {
            if (Methods == null)
            {
                Initialize();
            }

            IntrinsicMethod retval;
            if (Methods.TryGetValue(name, out retval))
                return retval;

            return null;
        }

        public static IntrinsicProperty ResolveProperty(string name)
        {
            if (Properties == null)
            {
                Initialize();
            }

            IntrinsicProperty retval;
            if (Properties.TryGetValue(name, out retval))
                return retval;

            return null;
        }

        static void Initialize()
        {
            Methods = new Dictionary<string, IntrinsicMethod>();

            Methods.Add("Object.GetType", new IntrinsicMethod_GetType());
            Methods.Add("Object.ToString", new IntrinsicMethod_ToString());
            Methods.Add("Object.GetMetaTable", new IntrinsicMethod_GetMetaTable());

            Properties = new Dictionary<string, IntrinsicProperty>();
            Properties.Add("Array.Length", new IntrinsicProperty_Length());
            Properties.Add("Length", new IntrinsicProperty_Length());
        }

        static Dictionary<string, IntrinsicMethod> Methods { get; set; }
        static Dictionary<string, IntrinsicProperty> Properties { get; set; }

    }

    class IntrinsicProperty_Length : IntrinsicProperty
    {

        public override bool IsStatic
        {
            get { return false; }
        }

        public override FlatOperand Resolve(MemberAccessExpressionSyntax node, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions)
        {
            FlatOperand fop_subject = function.ResolveExpression(node.Expression, null, instructions);

            if (into_lvalue == null)
            {
                FlatOperand fop_register = function.AllocateRegister("");
                into_lvalue = fop_register.GetLValue(function, instructions);
            }
            instructions.Add(FlatStatement.LEN(into_lvalue, fop_subject));
            return into_lvalue.AsRValue(FlatValue.FromType(result_type.ConvertedType));
        }
    }

    class IntrinsicMethod_GetType : IntrinsicMethod
    {

        public override bool IsStatic
        {
            get { return false; }
        }


        public override FlatOperand Resolve(ExpressionSyntax expression, ArgumentListSyntax argumentList, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions)
        {
            FlatOperand fop_subject;
            if (expression is IdentifierNameSyntax)
            {
                // typeof this
                fop_subject = FlatOperand.ThisRef(FlatValue.FromType(result_type.ConvertedType));
            }
            else if (expression is MemberAccessExpressionSyntax)
            {
                MemberAccessExpressionSyntax meas = (MemberAccessExpressionSyntax)expression;

                fop_subject = function.ResolveExpression(meas.Expression, null, instructions);
            }
            else
            {
                throw new NotImplementedException("GetType on expression type "+expression.GetType().ToString());
            }


            if (into_lvalue == null)
            {
                FlatOperand fop_register = function.AllocateRegister("");
                into_lvalue = fop_register.GetLValue(function, instructions);
            }
            instructions.Add(FlatStatement.TYPEOF(into_lvalue, fop_subject));
            return into_lvalue.AsRValue(FlatValue.Type(si.Symbol.ContainingType));
        }

    }

    class IntrinsicMethod_ToString : IntrinsicMethod
    {

        public override bool IsStatic
        {
            get { return false; }
        }

        public override FlatOperand Resolve(ExpressionSyntax expression, ArgumentListSyntax argumentList, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions)
        {
            FlatOperand fop_subject;
            if (expression is IdentifierNameSyntax)
            {
                // typeof this
                fop_subject = FlatOperand.ThisRef(FlatValue.FromType(result_type.ConvertedType));
            }
            else if (expression is MemberAccessExpressionSyntax)
            {
                MemberAccessExpressionSyntax meas = (MemberAccessExpressionSyntax)expression;

                fop_subject = function.ResolveExpression(meas.Expression, null, instructions);
            }
            else
            {
                throw new NotImplementedException("ToString on expression type " + expression.GetType().ToString());
            }


            if (into_lvalue == null)
            {
                FlatOperand fop_register = function.AllocateRegister("");
                into_lvalue = fop_register.GetLValue(function, instructions);
            }
            instructions.Add(FlatStatement.STRINGVAL(into_lvalue, fop_subject));
            return into_lvalue.AsRValue(FlatValue.String(string.Empty));
        }

    }

    class IntrinsicMethod_GetMetaTable : IntrinsicMethod
    {

        public override bool IsStatic
        {
            get { return false; }
        }

        public override FlatOperand Resolve(ExpressionSyntax expression, ArgumentListSyntax argumentList, TypeInfo result_type, SymbolInfo si, FlatOperand into_lvalue, Function function, List<FlatStatement> instructions)
        {
            FlatOperand fop_subject;
            if (expression is IdentifierNameSyntax)
            {
                // typeof this
                fop_subject = FlatOperand.ThisRef(FlatValue.FromType(result_type.ConvertedType));
            }
            else if (expression is MemberAccessExpressionSyntax)
            {
                MemberAccessExpressionSyntax meas = (MemberAccessExpressionSyntax)expression;

                fop_subject = function.ResolveExpression(meas.Expression, null, instructions);
            }
            else
            {
                throw new NotImplementedException("GetMetaTable on expression type " + expression.GetType().ToString());
            }


            if (into_lvalue == null)
            {
                FlatOperand fop_register = function.AllocateRegister("");
                into_lvalue = fop_register.GetLValue(function, instructions);
            }
            instructions.Add(FlatStatement.GETMETATABLE(into_lvalue, fop_subject));
            return into_lvalue.AsRValue(FlatValue.Table());
        }
    }
}
