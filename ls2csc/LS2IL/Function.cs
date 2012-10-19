﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace LS2IL
{
    class Function
    {
        public Function(Chunk chunk, int nFunction, SemanticModel model, MethodSymbol sym)
        {
            Chunk = chunk;
            Model = model;
            MethodSymbol = sym;
            NumFunction = nFunction;

            EmittedInstructions = new List<string>();
            EmittedLabels = new Dictionary<string, int>();
            Registers = new List<FlatValue>();
            FunctionValues = new List<FlatValue>();
            NamedRegisters = new Dictionary<string, int>();
            MetaValues = new Dictionary<string, FlatValue>();

            InitializeMetadata();
        }

        public void InitializeMetadata()
        {
            MetaValues.Add("Name", FlatValue.String(MethodSymbol.Name));
            MetaValues.Add("Fully Qualified Name", FlatValue.String(MethodSymbol.GetFullyQualifiedName()));
            string typeFQN = MethodSymbol.ContainingType.GetFullyQualifiedName();
            MetaValues.Add("ContainingType", FlatValue.String(typeFQN));
            MetaValues.Add("DisplayString", FlatValue.String(MethodSymbol.ToDisplayString()));

            string paramString = string.Empty;
            foreach(ParameterSymbol ps in MethodSymbol.Parameters)
            {
                string thisParam = string.Empty;

                thisParam = "{ \"" + ps.Type.ToString() + "\", \"" + ps.Name+"\" }";

                if (!string.IsNullOrEmpty(paramString))
                {
                    paramString += ", ";
                }
                paramString += thisParam;
            }
            MetaValues.Add("Parameters", new FlatValue( FlatValueType.VT_Array, "{ "+paramString+" }", null));
            
        }

        public Chunk Chunk { get; private set; }
        public SemanticModel Model { get; private set; }
        public MethodSymbol MethodSymbol { get; private set; }

        public int NumFunction { get; private set; }
        List<string> EmittedInstructions;
        public Dictionary<string, int> EmittedLabels { get; private set; }
        public Dictionary<string, LS2IL.FlatValue> MetaValues { get; private set; }

        public string CurrentBreakLabel { get; private set; }
        public string CurrentContinueLabel { get; private set; }

        public List<FlatValue> Registers { get; private set; }
        public Dictionary<string, int> NamedRegisters { get; private set; }
        public VariableScope CurrentVariableScope { get; private set; }

        public class VariableScope
        {
            public VariableScope(VariableScope parent)
            {
                Parent = parent;
                NamedRegisters = new Dictionary<string, int>();
            }

            public void Add(string name, int value)
            {
                NamedRegisters.Add(name, value);
            }

            public bool Resolve(string name, out int value)
            {
                if (NamedRegisters.TryGetValue(name, out value))
                    return true;

                return Parent.Resolve(name, out value);
            }

            public void Clear(List<FlatStatement> instructions)
            {
                foreach (KeyValuePair<string, int> kvp in NamedRegisters)
                {
                    
                    instructions.Add(FlatStatement.NULLIFY(FlatOperand.Immediate(FlatValue.Int32(kvp.Value))));
                }
            }

            public VariableScope Parent { get; private set; }
            protected Dictionary<string, int> NamedRegisters { get; private set; }
        }

        public string SetContinueLabel(string newLabel)
        {
            string wasLabel = CurrentContinueLabel;
            CurrentContinueLabel = newLabel;
            return wasLabel;
        }

        public string SetBreakLabel(string newLabel)
        {
            string wasLabel = CurrentBreakLabel;
            CurrentBreakLabel = newLabel;
            return wasLabel;
        }

        public void PushVariableScope(List<FlatStatement> instructions)
        {
            
            VariableScope vs = new VariableScope(CurrentVariableScope);
            CurrentVariableScope = vs;
        }

        public void PopVariableScope(List<FlatStatement> instructions)
        {
            CurrentVariableScope.Clear(instructions);
            CurrentVariableScope = CurrentVariableScope.Parent;
        }


        public List<FlatValue> FunctionValues { get; private set; }

        public int uniqueLabels;
        public string MakeUniqueLabelPrefix(string text)
        {
            uniqueLabels++;
            return "__" + text + uniqueLabels.ToString() + "_";
        }

        public FlatOperand AllocateRegister(string register_name_or_empty_string)
        {
            int nRegister;
            FlatValue reg = null;
            if (NamedRegisters.TryGetValue(register_name_or_empty_string, out nRegister))
            {
                return FlatOperand.RegisterRef(nRegister, reg);
            }
            
            nRegister = Registers.Count;
            

            Registers.Add(reg);
            if (string.IsNullOrEmpty(register_name_or_empty_string))
            {
                return FlatOperand.RegisterRef(nRegister, reg);
            }

            NamedRegisters[register_name_or_empty_string] = nRegister;
            return FlatOperand.RegisterRef(nRegister, reg);
        }

        public void InjectLeaveStatements(List<FlatStatement> instructions)
        {
            ls2csc.FinallyInjectLeaves fil = new ls2csc.FinallyInjectLeaves(this);
            fil.InjectLeaveStatements(instructions);
        }

        public void FlattenLabels(List<FlatStatement> instructions, bool filterUnusedInstructions)
        {
            {
                int nInstructions = 0;
                bool bSpaceBetween = false;

                foreach (FlatStatement fs in instructions)
                {
                    if (fs.Instruction == Instruction.meta_LABEL)
                    {
                        bSpaceBetween = false;
                        string s = fs.Operands[0].ImmediateValue.ValueText;
                        EmittedLabels.Add(s, nInstructions);
                        //fs.Emit();
                    }
                    else
                    {
                        if (bSpaceBetween)
                        {
                            if (filterUnusedInstructions)
                                continue;
                        }

                        nInstructions++;

                        switch (fs.Instruction)
                        {
                            case Instruction.JMP:
                            case Instruction.RETURN:
                            case Instruction.THROW:
                                {
                                    bSpaceBetween = true;
                                }
                                break;
                        }
                    }
                }
            }

            {
                int nInstructions = 0;
                bool bSpaceBetween = false;
                foreach (FlatStatement fs in instructions)
                {
                    if (fs.Instruction == Instruction.meta_LABEL)
                    {
                        bSpaceBetween = false;
                        continue;
                    }

                    if (bSpaceBetween)
                    {
                        if (filterUnusedInstructions)
                            continue;
                    }

                    nInstructions++;

                    switch (fs.Instruction)
                    {
                        case Instruction.JMP:
                        case Instruction.RETURN:
                        case Instruction.THROW:
                            {
                                bSpaceBetween = true;
                            }
                            break;
                    }

                    if (fs.Operands == null)
                        continue;

                    int nOperands = fs.Operands.Count;

                    for (int i = 0; i < nOperands; i++)
                    {


                        FlatOperand fop = fs.Operands[i];

                        if (fop.ImmediateValue != null && fop.ImmediateValue.ValueType == FlatValueType.VT_Label)
                        {
                            int labelValue;
                            if (!EmittedLabels.TryGetValue(fop.ImmediateValue.ValueText, out labelValue))
                            {
                                throw new NotImplementedException("Unresolved label " + fop.ImmediateValue.ValueText);
                            }

                            switch (fs.Instruction)
                            {
                                case Instruction.TRY:
                                    fs.Operands[i] = fop.WithImmediateValue(FlatValue.Int32(labelValue));
                                    break;
                                case Instruction.JE:
                                case Instruction.JG:
                                case Instruction.JGE:
                                case Instruction.JL:
                                case Instruction.JLE:
                                case Instruction.JMP:
                                case Instruction.JNE:
                                case Instruction.JNZ:
                                case Instruction.JZ:
                                    fs.Operands[i] = fop.WithImmediateValue(FlatValue.Int32(labelValue - nInstructions));
                                    break;
                                default:
                                    throw new NotImplementedException("Label reference in instruction " + fs.Instruction.ToString());
                                    break;
                            }

                        }
                    }
                }
            }
        }

        public void FlattenToInstructions(bool flattenLabels, bool bFilterUnusedInstructions)
        {
            List<FlatStatement> list = Flatten();

            InjectLeaveStatements(list);

            bool spaceBetween = false;
            if (flattenLabels)
            {
                FlattenLabels(list, true);




                foreach (FlatStatement fs in list)
                {
                    if (fs.Instruction == Instruction.meta_LABEL)
                    {
                        spaceBetween = false;
                    }
                    else
                    {
                        if (spaceBetween)
                        {
                            if (!bFilterUnusedInstructions)
                            {
                                EmitInstruction(fs.Emit() + " ; UNUSED");
                            }
                        }
                        else
                            EmitInstruction(fs.Emit());
                    }
                    if (fs.Instruction == Instruction.RETURN || fs.Instruction == Instruction.JMP || fs.Instruction == Instruction.THROW)
                    {
                        spaceBetween = true;
                    }
                }

                foreach (FlatValue fv in FunctionValues)
                {
                    if (fv.ValueType == FlatValueType.VT_Label)
                    {
                        // get label target
                        int labelValue;
                        if (!EmittedLabels.TryGetValue(fv.ValueText, out labelValue))
                        {
                            throw new NotImplementedException("Unresolved label " + fv.ValueText);
                        }

                        fv.ValueType = FlatValueType.VT_Integer;
                        fv.ValueText = labelValue.ToString();
                        fv.Object = labelValue;
                    }
                    else if (fv.Object is FlatArrayBuilder)
                    {
                        FlatArrayBuilder fab = (FlatArrayBuilder)fv.Object;
                        fab.FlattenLabels(this);
                    }
                    else if (fv.Object is FlatTableBuilder)
                    {
                        FlatTableBuilder fab = (FlatTableBuilder)fv.Object;
                        fab.FlattenLabels(this);
                    }
                }
            }
            else
            {
                foreach (FlatStatement fs in list)
                {
                    if (fs.Instruction == Instruction.meta_LABEL)
                    {
                        spaceBetween = false;
                    }
                    //else// this else is only for flattening labels
                    {
                        if (spaceBetween)
                        {
                            EmitInstruction(fs.Emit() + " ; UNUSED");
                        }
                        else
                            EmitInstruction(fs.Emit());
                    }
                    if (fs.Instruction == Instruction.RETURN || fs.Instruction == Instruction.JMP || fs.Instruction == Instruction.THROW)
                    {
                        spaceBetween = true;
                    }
                }
            }

        }

        void EmitInstruction(string s)
        {
            //System.Console.WriteLine(s);
            EmittedInstructions.Add(s);
        }

        public void EmitInstructions()
        {
            System.Console.WriteLine("; - begin instructions -");
            foreach (string s in EmittedInstructions)
            {
                if (s.StartsWith(";"))
                    System.Console.WriteLine(s);
                else if (s.EndsWith(":"))
                    System.Console.WriteLine(s);
                else
                    System.Console.WriteLine("\t" + s);
            }
            System.Console.WriteLine("; - end instructions -");
        }
        public void EmitValues()
        {
            System.Console.WriteLine("; - begin function values -");
            foreach (FlatValue fv in FunctionValues)
            {
                System.Console.WriteLine(".value " + fv.ToString());
            }
            System.Console.WriteLine("; - end function values -");
        }
        public void EmitMetaTable()
        {
            System.Console.WriteLine("; - begin meta table -");
            foreach (KeyValuePair<string, FlatValue> kvp in MetaValues)
            {
                System.Console.WriteLine(".meta \"" + kvp.Key + "\" " + kvp.Value.ToString());
            }
            System.Console.WriteLine("; - end meta table -");
        }



        public void Emit()
        {
            System.Console.WriteLine("");
            System.Console.WriteLine("; function[" + this.NumFunction.ToString() + "]");
            System.Console.WriteLine("function");
            System.Console.WriteLine(";.inputs " + (this.MethodSymbol.Parameters.Count + (MethodSymbol.ReturnsVoid?0:1)).ToString());

            EmitMetaTable();
            EmitValues();

            System.Console.WriteLine(".registers " + this.Registers.Count.ToString());
            System.Console.WriteLine("");

            EmitInstructions();
            System.Console.WriteLine("");

            System.Console.WriteLine("endfunction");
            System.Console.WriteLine("");
        }

        List<FlatStatement> Flatten()
        {
            List<FlatStatement> list = new List<FlatStatement>();

            ReadOnlyArray<SyntaxNode> roa = MethodSymbol.DeclaringSyntaxNodes;
            if (roa == null || roa.Count==0)
                return list;

            SyntaxNode sn = roa.Single();

            BlockSyntax block;
            switch (sn.Kind)
            {
                case SyntaxKind.MethodDeclaration:
                    block = ((MethodDeclarationSyntax)sn).Body;
                    break;
                case SyntaxKind.AddAccessorDeclaration:
                case SyntaxKind.GetAccessorDeclaration:
                case SyntaxKind.SetAccessorDeclaration:
                case SyntaxKind.RemoveAccessorDeclaration:
                case SyntaxKind.UnknownAccessorDeclaration:
                    block = ((AccessorDeclarationSyntax)sn).Body;
                    break;
                case SyntaxKind.ConstructorDeclaration:
                    block = ((ConstructorDeclarationSyntax)sn).Body;
                    break;
                case SyntaxKind.DestructorDeclaration:
                    block = ((DestructorDeclarationSyntax)sn).Body;
                    break;
                default:
                    throw new NotImplementedException("function kind "+sn.Kind.ToString());
                    break;
            }

            if (block == null)
            {
                throw new NotImplementedException("no body");
            }

            PushVariableScope(list);

            // import inputs into the variable scope


            Flatten(block, list);
            // don't CLEAR variable scope, just reset it.
            CurrentVariableScope = CurrentVariableScope.Parent;
            return list;        
        }

        public FlatOperand TypeOf(FlatOperand subject, TypeSymbol known_type_or_null, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("");
                into_lvalue = register_fop.GetLValue(this, instructions);                
            }

            instructions.Add(FlatStatement.TYPEOF(into_lvalue, subject));
            return into_lvalue.AsRValue(FlatValue.Type(known_type_or_null));
        }

        public FlatOperand Resolve(TypeSymbol type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            string type_fqn = type.GetFullyQualifiedName();
            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("__resolvetype" + type_fqn);
                into_lvalue = register_fop.GetLValue(this, instructions);
            }

            FlatOperand fop_typefqn = FlatOperand.Immediate(FlatValue.String(type_fqn));
            instructions.Add(FlatStatement.RESOLVETYPE(into_lvalue, fop_typefqn));

            return into_lvalue.AsRValue(FlatValue.Type(type));
        }

        public FlatOperand Resolve(IdentifierNameSyntax ins, TypeInfo result_type, List<FlatStatement> instructions)
        {
            SymbolInfo si = Model.GetSymbolInfo(ins);

            string name = ins.Identifier.ToString();
            switch (si.Symbol.Kind)
            {
                case SymbolKind.Local:
                    {
                        int nRegister;
                        if (!CurrentVariableScope.Resolve(name, out nRegister))
                        {
                            throw new NotImplementedException("Unresolved local symbol " + name);
                        }
                        FlatValue retval = FlatValue.Null();
                        return FlatOperand.RegisterRef(nRegister, retval);
                    }
                    break;
                case SymbolKind.Parameter:
                    {
                        int nParameter = 0;
                        if (!MethodSymbol.ReturnsVoid)
                            nParameter++;

                        foreach (ParameterSymbol ps in MethodSymbol.Parameters)
                        {
                            if (name == ps.Name)
                            {
                                FlatValue retval = FlatValue.FromType(ps.Type);
                                return FlatOperand.InputRef(nParameter, retval);
                            }
                            nParameter++;
                        }

                        throw new NotImplementedException("parameter '"+name+"' not found");
                    }
                    break;
                case SymbolKind.Field:
                    {
                        if (si.Symbol.IsStatic)
                        {
                            throw new NotImplementedException("static field");
                        }

                        // implied "this"
                        FlatOperand fop_field = Resolve((FieldSymbol)si.Symbol, null, instructions);

                        return fop_field;
                    }
                    break;
                case SymbolKind.Property:
                    {
                        if (si.Symbol.IsStatic)
                        {
                            FlatOperand fop_type = Resolve(si.Symbol.ContainingType, null, instructions);
                            FlatOperand fop_property = Resolve((PropertySymbol)si.Symbol, fop_type, null, instructions);


                            FlatOperand into_lvalue;
                            {
                                FlatOperand register_fop = AllocateRegister("");
                                into_lvalue = register_fop.GetLValue(this, instructions);
                            }

                            instructions.Add(FlatStatement.GETSTATICPROPERTY(into_lvalue, fop_property));
                            return into_lvalue.AsRValue(FlatValue.FromType(result_type.ConvertedType));
                        }
                        else
                        {
                            // implied "this"
                            FlatValue thisValue = FlatValue.ObjectRef(si.Symbol.ContainingType);
                            FlatOperand fop_type = TypeOf(FlatOperand.ThisRef(thisValue), si.Symbol.ContainingType, null, instructions);
                            FlatOperand fop_property = Resolve((PropertySymbol)si.Symbol, fop_type, null, instructions);


                            FlatOperand into_lvalue;
                            {
                                FlatOperand register_fop = AllocateRegister("");
                                into_lvalue = register_fop.GetLValue(this, instructions);
                            }

                            instructions.Add(FlatStatement.GETPROPERTY(into_lvalue, fop_property, FlatOperand.ThisRef(thisValue)));
                            return into_lvalue.AsRValue(FlatValue.FromType(result_type.ConvertedType));
                        }
                    }
                    break;
            }

            throw new NotImplementedException(si.Symbol.Kind.ToString());
        }

        public FlatOperand Resolve(MethodSymbol method, FlatOperand fop_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            string method_name = method.GetFullyQualifiedName();

            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("");
                into_lvalue = register_fop.GetLValue(this, instructions);
            }

            FlatOperand fop_methodname = FlatOperand.Immediate(FlatValue.String(method_name));

            if (method.IsStatic)
            {
                instructions.Add(FlatStatement.RESOLVESTATICMETHOD(into_lvalue,fop_type,fop_methodname));
                return into_lvalue.AsRValue(FlatValue.StaticMethod(method));
            }

            instructions.Add(FlatStatement.RESOLVEMETHOD(into_lvalue, fop_type, fop_methodname));
            return into_lvalue.AsRValue(FlatValue.Method(method));
        }


        public FlatOperand Resolve(FieldSymbol field, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            string field_name = field.Name;

            TypeExtraInfo tei = Chunk.GetTypeExtraInfo(field.ContainingType);
            if (tei == null)
            {
                throw new NotImplementedException("field of type without fields declarations? " + field.ContainingType.GetFullyQualifiedName());
            }
            
            int nField;
            if (tei.FieldNames.TryGetValue(field_name, out nField))
            {
                FieldExtraInfo fei = tei.Fields[nField];

                // non-static field
                return FlatOperand.FieldRef(nField, FlatValue.FromType(fei.Type));
            }
            if (tei.StaticFieldNames.TryGetValue(field_name, out nField))
            {
                // static field

                throw new NotImplementedException("static field from type " + field.ContainingType.GetFullyQualifiedName());
            }

            throw new NotImplementedException("missing field from type " + field.ContainingType.GetFullyQualifiedName());
        }

        public FlatOperand Resolve(PropertySymbol property, FlatOperand fop_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            string property_name = property.Name;

            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("");
                into_lvalue = register_fop.GetLValue(this, instructions);
            }

            FlatOperand fop_methodname = FlatOperand.Immediate(FlatValue.String(property_name));

            if (property.IsStatic)
            {
                instructions.Add(FlatStatement.RESOLVESTATICPROPERTY(into_lvalue, fop_type, fop_methodname));
                return into_lvalue.AsRValue(FlatValue.StaticProperty(property));
            }

            instructions.Add(FlatStatement.RESOLVEPROPERTY(into_lvalue, fop_type, fop_methodname));
            return into_lvalue.AsRValue(FlatValue.Property(property));
        }


        public FlatOperand ResolveArgument(ArgumentSyntax arg, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            /*
        // Summary:
        //     ExpressionSyntax node representing the argument.
        public ExpressionSyntax Expression { get; }
        //
        // Summary:
        //     NameColonSyntax node representing the optional name arguments.
        public NameColonSyntax NameColon { get; }
        //
        // Summary:
        //     SyntaxToken representing the optional ref or out keyword.
        public SyntaxToken RefOrOutKeyword { get; }             
             */

            if (arg.NameColon != null)
            {
                throw new NotImplementedException("name : value");
            }
            if (arg.RefOrOutKeyword.Kind != SyntaxKind.None)
            {
                throw new NotImplementedException("ref/out keyword");
            }

            FlatOperand opnd = ResolveExpression(arg.Expression, into_lvalue, instructions);

            return opnd;
        }

        public List<FlatOperand> ResolveArguments(BracketedArgumentListSyntax args, List<FlatStatement> instructions)
        {
            List<FlatOperand> list = new List<FlatOperand>();

            if (args == null || args.Arguments.Count == 0)
                return list;

            foreach (ArgumentSyntax arg in args.Arguments)
            {
                list.Add(ResolveArgument(arg, null, instructions));
            }

            return list;
        }

        public List<FlatOperand> ResolveArguments(ArgumentListSyntax args, List<FlatStatement> instructions)
        {
            List<FlatOperand> list = new List<FlatOperand>();

            if (args == null || args.Arguments.Count == 0)
                return list;

            foreach (ArgumentSyntax arg in args.Arguments)
            {
                list.Add(ResolveArgument(arg,null, instructions));
            }

            return list;
        }

        public FlatOperand ResolveArgumentsToArray(ArgumentListSyntax args, FlatOperand return_reference, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("");
                into_lvalue = register_fop.GetLValue(this, instructions);
            }

            FlatValue literalArray;
            FlatOperand fop_array;

            bool allLiteral = (return_reference==null);
            if (allLiteral)
            {
                foreach (ArgumentSyntax ars in args.Arguments)
                {
                    /*
                            // Summary:
                            //     ExpressionSyntax node representing the argument.
                            public ExpressionSyntax Expression { get; }
                            //
                            // Summary:
                            //     NameColonSyntax node representing the optional name arguments.
                            public NameColonSyntax NameColon { get; }
                            //
                            // Summary:
                            //     SyntaxToken representing the optional ref or out keyword.
                            public SyntaxToken RefOrOutKeyword { get; }
                    /**/
                    if (ars.NameColon != null)
                    {
                        throw new NotImplementedException("name : value");
                    }
                    if (ars.RefOrOutKeyword.Kind != SyntaxKind.None)
                    {
                        throw new NotImplementedException("ref/out keywords");
                    }

                    if (!(ars.Expression is LiteralExpressionSyntax))
                    {
                        allLiteral = false;
                    }
                }


                if (allLiteral)
                {
                    FlatArrayBuilder fab = new FlatArrayBuilder();

                    foreach (ArgumentSyntax ars in args.Arguments)
                    {
                        LiteralExpressionSyntax les = (LiteralExpressionSyntax)ars.Expression;
                        // get the type
                        TypeInfo ti = Model.GetTypeInfo(les);
                        FlatOperand fop_element = ResolveExpression(les, ti.ConvertedType);
                        fab.Add(fop_element.ImmediateValue);
                    }

                    literalArray = fab.GetFlatValue();
                    instructions.Add(FlatStatement.DUPLICATE(into_lvalue, FlatOperand.Immediate(literalArray)));
                    fop_array = into_lvalue.AsRValue(literalArray);
                    return fop_array;
                }
            }

            // generate a new array
            literalArray = new FlatValue(FlatValueType.VT_Array, "{ }", null);
            instructions.Add(FlatStatement.DUPLICATE(into_lvalue, FlatOperand.Immediate(literalArray)));

            fop_array = into_lvalue.AsRValue(literalArray);

            if (return_reference != null)
            {
                instructions.Add(FlatStatement.ADD(into_lvalue, fop_array, return_reference));
            }

            foreach(ArgumentSyntax ars in args.Arguments)
            {
                /*
                        // Summary:
                        //     ExpressionSyntax node representing the argument.
                        public ExpressionSyntax Expression { get; }
                        //
                        // Summary:
                        //     NameColonSyntax node representing the optional name arguments.
                        public NameColonSyntax NameColon { get; }
                        //
                        // Summary:
                        //     SyntaxToken representing the optional ref or out keyword.
                        public SyntaxToken RefOrOutKeyword { get; }
                /**/
                FlatOperand fop_element = ResolveExpression(ars.Expression, null, instructions);
                instructions.Add(FlatStatement.ADD(into_lvalue,fop_array,fop_element));
            }

            return fop_array;
        }


        public FlatOperand ArrayIndexFrom(List<FlatOperand> list, List<FlatStatement> instructions)
        {
            if (list.Count > 1)
            {
                throw new NotImplementedException("Multi-dimensional Arrays");
            }

            return list[0];
            //            throw new NotImplementedException("convert argument list to array index number");
        }


        public FlatOperand Resolve(ArrayCreationExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)        
        {
            /*        // Summary:
        //     InitializerExpressionSyntax node representing the initializer of the array
        //     creation expression.
        public InitializerExpressionSyntax Initializer { get; }
        //
        // Summary:
        //     SyntaxToken representing the new keyword.
        public SyntaxToken NewKeyword { get; }
        //
        // Summary:
        //     ArrayTypeSyntax node representing the type of the array.
        public ArrayTypeSyntax Type { get; }
             */
            if (node.Initializer != null)
            {
                throw new NotImplementedException("new array with initializer");
            }

            /*
        // Summary:
        //     TypeSyntax node representing the type of the element of the array.
        public TypeSyntax ElementType { get; }
        //
        // Summary:
        //     SyntaxList of ArrayRankSpecifierSyntax nodes representing the list of rank
        //     specifiers for the array.
        public SyntaxList<ArrayRankSpecifierSyntax> RankSpecifiers { get; }
/**/
            /*
            if (node.Type.RankSpecifiers.Count > 1)
            {
                throw new NotImplementedException("new array with multiple rank specifiers");
            }

            /*
        public int Rank { get; }
        public SeparatedSyntaxList<ExpressionSyntax> Sizes { get; }
             */

            // get total number of elements
            FlatOperand fop_total = null;

            foreach(ArrayRankSpecifierSyntax arss in node.Type.RankSpecifiers)
            {
                foreach(ExpressionSyntax es in arss.Sizes)
                {
                    if (fop_total != null)
                    {
                        FlatOperand fop_size = ResolveExpression(es, null, instructions);
                        
                        FlatOperand fop_total_lvalue = AllocateRegister("");

                        FlatStatement.MUL(fop_total_lvalue, fop_total, fop_size);

                        fop_total = fop_total_lvalue.AsRValue(FlatValue.Int32(0));
                    }
                    else
                    {
                        fop_total = ResolveExpression(es, null, instructions);
                    }
                }
            }

            TypeInfo ti = Model.GetTypeInfo(node.Type.ElementType);
            FlatOperand fop_type = Resolve(ti.ConvertedType, null, instructions);

            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("");
                into_lvalue = register_fop.GetLValue(this, instructions);
            }
            instructions.Add(FlatStatement.NEWARRAY(into_lvalue, fop_total, fop_type));

            return into_lvalue.AsRValue(FlatValue.FromType(result_type.ConvertedType));


        }

        public FlatOperand Resolve(ObjectCreationExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            /*
        // Summary:
        //     ArgumentListSyntax representing the list of arguments passed as part of the
        //     object creation expression.
        public ArgumentListSyntax ArgumentList { get; }
        //
        // Summary:
        //     InitializerExpressionSyntax representing the initializer expression for the
        //     object being created.
        public InitializerExpressionSyntax Initializer { get; }
        //
        // Summary:
        //     SyntaxToken representing the new keyword.
        public SyntaxToken NewKeyword { get; }
        //
        // Summary:
        //     TypeSyntax representing the type of the object being created.
        public TypeSyntax Type { get; }
             */
            if (node.Initializer != null)
            {
                throw new NotImplementedException("new with initializer");
            }

            TypeInfo ti = Model.GetTypeInfo(node.Type);
           
            SymbolInfo si = Model.GetSymbolInfo(node);

            if (si.Symbol.Kind != SymbolKind.Method)
            {
                throw new NotImplementedException("new without Constructor method?");
            }

            // NEW destination, type, arguments
            if (into_lvalue == null)
            {
                FlatOperand register_fop = AllocateRegister("");
                into_lvalue = register_fop.GetLValue(this, instructions);
            }
            
            FlatOperand fop_type = Resolve(ti.ConvertedType,null,instructions);

            if (si.Symbol.IsImplicitlyDeclared)
            {
                Chunk.AddFunction((MethodSymbol)si.Symbol);
            }

            FlatOperand fop_constructor = Resolve((MethodSymbol)si.Symbol,fop_type,null,instructions);
            
            FlatOperand fop_args = ResolveArgumentsToArray(node.ArgumentList,null,null, instructions);

            instructions.Add(FlatStatement.NEWOBJECT(into_lvalue, fop_constructor, fop_args));

            return into_lvalue.AsRValue(FlatValue.FromType(ti.ConvertedType));
        }

        public FlatOperand Resolve(ElementAccessExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            /*
    // Summary:
    //     BracketedArgumentListSyntax node representing the list of arguments of the
    //     element access expression.
    public BracketedArgumentListSyntax ArgumentList { get; }
    //
    // Summary:
    //     ExpressionSyntax node representing the expression which is accessing the
    //     element.
    public ExpressionSyntax Expression { get; }                 
             */
            // resolve to an Array or Table
            //SymbolInfo si = Model.GetSymbolInfo(node.Expression);
            TypeInfo ti = Model.GetTypeInfo(node.Expression);

            FlatOperand fop_array = ResolveExpression(node.Expression, into_lvalue, instructions);

            switch (ti.ConvertedType.TypeKind)
            {
                case TypeKind.ArrayType:
                    {
                        // resolve the array index
                        List<FlatOperand> args = ResolveArguments(node.ArgumentList, instructions);
                        FlatOperand key = ArrayIndexFrom(args, instructions);

                        if (into_lvalue == null)
                        {
                            FlatOperand register_fop = AllocateRegister("");
                            into_lvalue = register_fop.GetLValue(this, instructions);
                        }

                        instructions.Add(FlatStatement.ARRAYGET(into_lvalue, fop_array, key));
                        return into_lvalue.AsRValue(FlatValue.FromType(result_type.ConvertedType));
                    }
                    break;
                default:
                    throw new NotImplementedException("element access on type " + fop_array.ImmediateValue.ValueType.ToString());
            }

            // resolve the index


            /**/
            throw new NotImplementedException();
        }

        public FlatOperand Resolve(MemberAccessExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            SymbolInfo si = Model.GetSymbolInfo(node);
            if (si.Symbol.Kind != SymbolKind.Property)
            {
                throw new NotImplementedException("non-Property member access");
            }

            PropertySymbol property = (PropertySymbol)si.Symbol;

            // check for intrinsics
            string intrinsic;
            if (property.GetIntrinsic(out intrinsic))
            {
                var im = ls2csc.Intrinsics.ResolveProperty(intrinsic);
                if (im == null)
                {
                    throw new NotImplementedException("Unhandled intrinsic method " + intrinsic);
                }
                return im.Resolve(node, result_type, si, into_lvalue, this, instructions);
            }

            if (si.Symbol.IsStatic)
            {
                FlatOperand fop_type = Resolve(si.Symbol.ContainingType, null, instructions);
                FlatOperand fop_property = Resolve(property, fop_type, null, instructions);

                throw new NotImplementedException("static property access");
            }

            {
                FlatOperand fop_subject = ResolveExpression(node.Expression, null, instructions);
                FlatOperand fop_type = TypeOf(fop_subject,null, null, instructions);

                FlatOperand fop_property = Resolve(property, fop_type, null, instructions);

                if (into_lvalue == null)
                {
                    FlatOperand register_fop = AllocateRegister("");
                    into_lvalue = register_fop.GetLValue(this, instructions);
                }

                instructions.Add(FlatStatement.GETPROPERTY(into_lvalue, fop_property, fop_subject));
                return into_lvalue.AsRValue(FlatValue.FromType(result_type.ConvertedType));
            }
            /*
            LS2Property prop = (LS2Property)member;
            return prop.Flatten_Get(subject, this, instructions);
            */

            throw new NotImplementedException("property " + node.ToString());

        }

        public FlatOperand Resolve(InvocationExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            SymbolInfo si = Model.GetSymbolInfo(node);
            if (si.Symbol == null)
            {
                throw new NotImplementedException("non-Symbol invocation expression");
            }

            if (si.Symbol.Kind != SymbolKind.Method)
            {
                throw new NotImplementedException("non-Method invocation");
            }

            MethodSymbol method = (MethodSymbol)si.Symbol;
            // check for intrinsics
            
            string intrinsic;
            if (method.GetIntrinsic(out intrinsic))
            {
                var im = ls2csc.Intrinsics.ResolveMethod(intrinsic);
                if (im == null)
                {
                    throw new NotImplementedException("Unhandled intrinsic method "+intrinsic);
                }
                return im.Resolve(node,result_type,si,into_lvalue,this,instructions);
            }
            



            ArgumentListSyntax args = node.ArgumentList;
            
            int nArgs = args.Arguments.Count;
            //int nFirstArg = 0;
            if (!method.ReturnsVoid)
            {
                nArgs++;
                //nFirstArg = 1;
            }

            if (si.Symbol.IsStatic)
            {
                FlatOperand fop_type = Resolve(si.Symbol.ContainingType, null, instructions);
                FlatOperand fop_method = Resolve(method, fop_type, null, instructions);
                // 1. RESOLVETYPE               

                switch (nArgs)
                {
                    case 0:
                        {
                            instructions.Add(FlatStatement.FASTCALLSTATICMETHOD(fop_method));
                            return FlatOperand.LiteralNull();
                        }
                        break;
                    case 1:
                        {
                            if (!method.ReturnsVoid)
                            {
                                FlatOperand fop_return = AllocateRegister("");

                                FlatOperand lvalue_return = fop_return.GetLValue(this, instructions);
                                instructions.Add(FlatStatement.REREFERENCE(lvalue_return, FlatOperand.LiteralNull()));
                                instructions.Add(FlatStatement.FASTCALLSTATICMETHOD(fop_method, fop_return));
                                FlatOperand rvalue_return = lvalue_return.AsRValue(FlatValue.Null());

                                if (into_lvalue == null)
                                    into_lvalue = AllocateRegister("").GetLValue(this, instructions);
                                instructions.Add(FlatStatement.DEREFERENCE(into_lvalue, rvalue_return));
                                return into_lvalue.AsRValue(rvalue_return.ImmediateValue);
                            }

                            if (args.Arguments[0].RefOrOutKeyword.Kind != SyntaxKind.None)
                                throw new NotImplementedException("ref or out keyword on " + args.Arguments[0].ToString());
                            FlatOperand input0_fop = ResolveExpression(args.Arguments[0].Expression, null, instructions);
                            instructions.Add(FlatStatement.FASTCALLSTATICMETHOD(fop_method, input0_fop));
                            return FlatOperand.LiteralNull();
                        }
                        break;
                    case 2:
                        {
                            if (!method.ReturnsVoid)
                            {
                                FlatOperand fop_return = AllocateRegister("");

                                FlatOperand lvalue_return = fop_return.GetLValue(this, instructions);
                                instructions.Add(FlatStatement.REREFERENCE(lvalue_return, FlatOperand.LiteralNull()));

                                if (args.Arguments[0].RefOrOutKeyword.Kind != SyntaxKind.None)
                                    throw new NotImplementedException("ref or out keyword on " + args.Arguments[0].ToString());
                                FlatOperand input0_fop = ResolveExpression(args.Arguments[0].Expression, null, instructions);

                                instructions.Add(FlatStatement.FASTCALLSTATICMETHOD(fop_method, fop_return, input0_fop));
                                FlatOperand rvalue_return = lvalue_return.AsRValue(FlatValue.Null());

                                if (into_lvalue == null)
                                    into_lvalue = AllocateRegister("").GetLValue(this, instructions);
                                instructions.Add(FlatStatement.DEREFERENCE(into_lvalue, rvalue_return));
                                return into_lvalue.AsRValue(rvalue_return.ImmediateValue);
                            }
                            {
                                if (args.Arguments[0].RefOrOutKeyword.Kind != SyntaxKind.None)
                                    throw new NotImplementedException("ref or out keyword on " + args.Arguments[0].ToString());
                                if (args.Arguments[1].RefOrOutKeyword.Kind != SyntaxKind.None)
                                    throw new NotImplementedException("ref or out keyword on " + args.Arguments[1].ToString());
                                FlatOperand input0_fop = ResolveExpression(args.Arguments[0].Expression, null, instructions);
                                FlatOperand input1_fop = ResolveExpression(args.Arguments[1].Expression, null, instructions);
                                instructions.Add(FlatStatement.FASTCALLSTATICMETHOD(fop_method, input0_fop, input1_fop));
                                return FlatOperand.LiteralNull();
                            }
                        }
                        break;
                    default:
                        {

                            if (!method.ReturnsVoid)
                            {
                                FlatOperand fop_return = AllocateRegister("");

                                FlatOperand lvalue_return = fop_return.GetLValue(this, instructions);
                                instructions.Add(FlatStatement.REREFERENCE(lvalue_return, FlatOperand.LiteralNull()));

                                FlatOperand fop_args = ResolveArgumentsToArray(node.ArgumentList, fop_return, null, instructions);

                                instructions.Add(FlatStatement.CALLSTATICMETHOD(fop_method, fop_args));
                                FlatOperand rvalue_return = lvalue_return.AsRValue(FlatValue.Null());

                                if (into_lvalue == null)
                                    into_lvalue = AllocateRegister("").GetLValue(this, instructions);
                                instructions.Add(FlatStatement.DEREFERENCE(into_lvalue, rvalue_return));
                                return into_lvalue.AsRValue(rvalue_return.ImmediateValue);
                            }
                            /**/

                            {
                                FlatOperand fop_args = ResolveArgumentsToArray(node.ArgumentList, null, null, instructions);
                                {
                                    instructions.Add(FlatStatement.CALLSTATICMETHOD(fop_method, fop_args));
                                    return FlatOperand.LiteralNull();
                                }
                            }
                        }
                        break;
                }

            }

            if (!(node.Expression is MemberAccessExpressionSyntax))
            {
                throw new NotImplementedException("CALLMETHOD not on MemberAccessExpressionSyntax");
            }

            {
                MemberAccessExpressionSyntax meas = (MemberAccessExpressionSyntax)node.Expression;


                FlatOperand fop_subject = ResolveExpression(meas.Expression, null, instructions);
                FlatOperand fop_type = TypeOf(fop_subject,null, null, instructions);

                FlatOperand fop_method = Resolve(method, fop_type, null, instructions);

                // non-static method            
                // 1. RESOLVETYPE               
                    // FASTCALL
                switch (nArgs)
                {
                    case 0:
                        {
                            instructions.Add(FlatStatement.FASTCALLMETHOD(fop_method, fop_subject));
                            return FlatOperand.LiteralNull();
                        }
                        break;
                    case 1:
                        {
                            if (!method.ReturnsVoid)
                            {
                                FlatOperand fop_return = AllocateRegister("");

                                FlatOperand lvalue_return = fop_return.GetLValue(this,instructions);
                                instructions.Add(FlatStatement.REREFERENCE(lvalue_return,FlatOperand.LiteralNull()));
                                instructions.Add(FlatStatement.FASTCALLMETHOD(fop_method, fop_subject, fop_return));
                                FlatOperand rvalue_return = lvalue_return.AsRValue(FlatValue.Null());

                                if (into_lvalue == null)
                                    into_lvalue = AllocateRegister("").GetLValue(this, instructions);
                                instructions.Add(FlatStatement.DEREFERENCE(into_lvalue, rvalue_return));
                                return into_lvalue.AsRValue(rvalue_return.ImmediateValue);
                            }

                            if (args.Arguments[0].RefOrOutKeyword.Kind != SyntaxKind.None)
                                throw new NotImplementedException("ref or out keyword on " + args.Arguments[0].ToString());
                            FlatOperand input0_fop = ResolveExpression(args.Arguments[0].Expression, null, instructions);
                            instructions.Add(FlatStatement.FASTCALLMETHOD(fop_method, fop_subject, input0_fop));
                            return FlatOperand.LiteralNull();
                        }
                        break;
                    default:
                        {

                            if (!method.ReturnsVoid)
                            {
                                FlatOperand fop_return = AllocateRegister("");

                                FlatOperand lvalue_return = fop_return.GetLValue(this, instructions);
                                instructions.Add(FlatStatement.REREFERENCE(lvalue_return, FlatOperand.LiteralNull()));

                                FlatOperand fop_args = ResolveArgumentsToArray(node.ArgumentList, fop_return, null, instructions);

                                instructions.Add(FlatStatement.CALLMETHOD(fop_method, fop_subject, fop_args));
                                FlatOperand rvalue_return = lvalue_return.AsRValue(FlatValue.Null());

                                if (into_lvalue == null)
                                    into_lvalue = AllocateRegister("").GetLValue(this, instructions);
                                instructions.Add(FlatStatement.DEREFERENCE(into_lvalue, rvalue_return));
                                return into_lvalue.AsRValue(rvalue_return.ImmediateValue);
                            }
                            /**/

                            {
                                FlatOperand fop_args = ResolveArgumentsToArray(node.ArgumentList, null, null, instructions);
                                {
                                    instructions.Add(FlatStatement.CALLMETHOD(fop_method, fop_subject, fop_args));
                                    return FlatOperand.LiteralNull();
                                }
                            }
                        }

                        break;
                }
            }
            /**/

            throw new NotImplementedException();
        }

        public FlatOperand ResolveLValue(ExpressionSyntax expr, List<FlatStatement> instructions)
        {
            if (expr is IdentifierNameSyntax)
            {
                IdentifierNameSyntax ins = (IdentifierNameSyntax)expr;

                TypeInfo ti = Model.GetTypeInfo(expr);

                return this.Resolve(ins, ti, instructions);
            }
            throw new NotImplementedException();
        }

        public void Flatten(ThrowStatementSyntax node, List<FlatStatement> instructions)
        {
            if (node.Expression == null)
            {
                FlatStatement.THROW(FlatOperand.ExceptionRef());
                return;
            }
            FlatOperand fop_exception = this.ResolveExpression(node.Expression, null, instructions);
            instructions.Add(FlatStatement.THROW(fop_exception));
        }

        public FlatOperand ResolveBinaryExpression(SyntaxKind kind, FlatOperand fop_left, FlatOperand fop_right, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            if (into_lvalue == null)
            {
                FlatOperand fop_result = AllocateRegister("");
                into_lvalue = fop_result.GetLValue(this, instructions);
            }

            switch (kind)
            {
                case SyntaxKind.AddAssignExpression:
                case SyntaxKind.AddExpression:
                    {
                        instructions.Add(FlatStatement.ADD(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.BitwiseAndExpression:
                case SyntaxKind.AndAssignExpression:
                    {
                        instructions.Add(FlatStatement.AND(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.DivideExpression:
                case SyntaxKind.DivideAssignExpression:
                    {
                        instructions.Add(FlatStatement.DIV(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.ExclusiveOrExpression:
                case SyntaxKind.ExclusiveOrAssignExpression:
                    {
                        instructions.Add(FlatStatement.XOR(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.LeftShiftAssignExpression:
                    {
                        instructions.Add(FlatStatement.SHL(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.ModuloExpression:
                case SyntaxKind.ModuloAssignExpression:
                    {
                        instructions.Add(FlatStatement.MOD(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.MultiplyAssignExpression:
                    {
                        instructions.Add(FlatStatement.MUL(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.BitwiseOrExpression:
                case SyntaxKind.OrAssignExpression:
                    {
                        instructions.Add(FlatStatement.OR(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.RightShiftAssignExpression:
                    {
                        instructions.Add(FlatStatement.SHR(into_lvalue, fop_left, fop_right));
                    }
                    break;
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.SubtractAssignExpression:
                    {
                        instructions.Add(FlatStatement.SUB(into_lvalue, fop_left, fop_right));
                    }
                    break;
                default:
                    throw new NotImplementedException("unhandled assignment operator");
                    break;
            }

            return into_lvalue.AsRValue(fop_left.ImmediateValue);
        }

        public FlatOperand ResolveParentExpression(SymbolInfo si,SyntaxNode sn, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            if (sn is MemberAccessExpressionSyntax)
            {
                return ResolveExpression(((MemberAccessExpressionSyntax)sn).Expression, null, instructions);
            }
            if (sn is IdentifierNameSyntax)
            {
                IdentifierNameSyntax ins = (IdentifierNameSyntax)sn;
                switch (si.Symbol.Kind)
                {
                    case SymbolKind.Field:
                        {
                            // it's a field, with no member access
                            if (si.Symbol.IsStatic)
                            {
                                throw new NotImplementedException();
                            }
                            // and it's not static.

                            // must be from "this".
                            FlatValue val = FlatValue.FromType(si.Symbol.ContainingType);
                            if (into_lvalue != null)
                            {
                                instructions.Add(FlatStatement.REFERENCE(into_lvalue, FlatOperand.ThisRef(val)));
                                return into_lvalue.AsRValue(val);
                            }
                            return FlatOperand.ThisRef(val);
                        }
                        break;
                    case SymbolKind.Property:
                        {
                            // it's a field, with no member access
                            if (si.Symbol.IsStatic)
                            {
                                throw new NotImplementedException();
                            }
                            // and it's not static.

                            // must be from "this".
                            FlatValue val = FlatValue.FromType(si.Symbol.ContainingType);
                            if (into_lvalue != null)
                            {
                                instructions.Add(FlatStatement.REFERENCE(into_lvalue, FlatOperand.ThisRef(val)));
                                return into_lvalue.AsRValue(val);
                            }
                            return FlatOperand.ThisRef(val);
                        }
                    default:
                        throw new NotImplementedException();
                        break;
                }
            }
            
            throw new NotImplementedException();
        }

        public FlatOperand ResolveAssignmentExpression(BinaryExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            FlatOperand fop_result;
            FlatOperand lvalue_result;
            FlatOperand fop_subject;
            FlatOperand fop_type;

            if (node.Left is ElementAccessExpressionSyntax)
            {
                ElementAccessExpressionSyntax eaes = (ElementAccessExpressionSyntax)node.Left;

                fop_subject = ResolveExpression(eaes.Expression, null, instructions);

                FlatOperand fop_right = ResolveExpression(node.Right, into_lvalue, instructions);

                List<FlatOperand> args = ResolveArguments(eaes.ArgumentList, instructions);
                FlatOperand key = ArrayIndexFrom(args, instructions);

                instructions.Add(FlatStatement.ARRAYSET(fop_subject, key, fop_right));
                return fop_right;
            }

            SymbolInfo si = Model.GetSymbolInfo(node.Left);
            switch (si.Symbol.Kind)
            {
                case SymbolKind.Field:
                    {
                        // need the parent object for the field 
                        fop_subject = ResolveParentExpression(si,node.Left, null, instructions);

                        FlatOperand fop_field;
                        TypeSymbol typeSymbol;
                        FieldSymbol ps = (FieldSymbol)si.Symbol;

                        if (ps.IsStatic)
                        {
                            throw new NotImplementedException("static Field assignment");
                        }

                        typeSymbol = ps.Type;
                        fop_field = Resolve(ps, null, instructions);

                        if (fop_field.OperandType != FlatOperandType.OPND_FIELD_VALUEREF)
                        {
                            throw new NotImplementedException("field reference resolved to non-field reference");
                        }

                        fop_field = FlatOperand.LiteralInteger(fop_field.OperandIndex);

                        FlatOperand fop_right = ResolveExpression(node.Right, into_lvalue, instructions);

                        if (node.Kind == SyntaxKind.AssignExpression)
                        {
                            fop_result = fop_right;
                        }
                        else
                        {
                            fop_result = AllocateRegister("");
                            lvalue_result = fop_result.GetLValue(this, instructions);
                            instructions.Add(FlatStatement.GETFIELD(lvalue_result, fop_subject, fop_field));

                            ResolveBinaryExpression(node.Kind, fop_result, fop_right, lvalue_result, instructions);
                        }

                        instructions.Add(FlatStatement.SETFIELD(fop_subject, fop_field, fop_result));
                        return fop_right;
                    }
                    break;
                case SymbolKind.Property:
                    {
                        fop_subject = ResolveParentExpression(si, node.Left, null, instructions);
                        fop_type = TypeOf(fop_subject, null, null, instructions);

                        FlatOperand fop_property;
                        TypeSymbol typeSymbol;
                        {
                            PropertySymbol ps = (PropertySymbol)si.Symbol;

                            if (ps.IsStatic)
                            {
                                throw new NotImplementedException("static property assignment");
                            }

                            typeSymbol = ps.Type;
                            fop_property = Resolve(ps, fop_type, null, instructions);
                        }

                        FlatOperand fop_right = ResolveExpression(node.Right, into_lvalue, instructions);

                        if (node.Kind == SyntaxKind.AssignExpression)
                        {
                            fop_result = fop_right;
                        }
                        else
                        {
                            fop_result = AllocateRegister("");
                            lvalue_result = fop_result.GetLValue(this, instructions);
                            instructions.Add(FlatStatement.GETPROPERTY(lvalue_result, fop_property, fop_subject));

                            ResolveBinaryExpression(node.Kind, fop_result, fop_right, lvalue_result, instructions);
                        }




                        instructions.Add(FlatStatement.SETPROPERTY(fop_property, fop_subject, fop_result));
                        return fop_right;
                    }
                    break;
                case SymbolKind.Local:
                    {
                        fop_subject = ResolveExpression(node.Left, null, instructions);


                        if (node.Kind == SyntaxKind.AssignExpression)
                        {
                            if (into_lvalue == null)
                            {
                                return ResolveExpression(node.Right, fop_subject.GetLValue(this, instructions), instructions);
                            }

                            FlatOperand fop_right = ResolveExpression(node.Right, into_lvalue, instructions);
                            instructions.Add(FlatStatement.REFERENCE(fop_subject.GetLValue(this, instructions), fop_right));
                            return fop_right;
                        }
                        else
                        {
                            FlatOperand fop_right = ResolveExpression(node.Right, into_lvalue, instructions);
                            ResolveBinaryExpression(node.Kind, fop_subject, fop_right, fop_subject.GetLValue(this, instructions), instructions);
                            return fop_right;
                        }
                    }
                    break;
                case SymbolKind.Parameter:
                    {

                    }
                    break;
            }

            throw new NotImplementedException("unhandled assignment l-value type "+si.Symbol.Kind.ToString());

        }

        public FlatOperand ResolveExpression(LiteralExpressionSyntax les, TypeSymbol result_type)
        {
            FlatValue val = FlatValue.FromLiteralToken(result_type, les.Token);
            return FlatOperand.Immediate(val);
        }


        public FlatOperand ResolveExpression(BinaryExpressionSyntax node, TypeInfo result_type, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            if (node.IsAssignment())
            {
                return ResolveAssignmentExpression(node, result_type, into_lvalue, instructions);
            }

            FlatOperand left = ResolveExpression(node.Left, null, instructions);
            FlatOperand right = ResolveExpression(node.Right, null, instructions);
            return ResolveBinaryExpression(node.Kind, left, right, into_lvalue, instructions);

        }

        public FlatOperand ResolveExpression(ExpressionSyntax node, FlatOperand into_lvalue, List<FlatStatement> instructions)
        {
            TypeInfo result_type = Model.GetTypeInfo(node);
            // resultant type of the expression
            if (!result_type.ImplicitConversion.IsIdentity && !result_type.ImplicitConversion.IsImplicit)
            {
                throw new NotImplementedException("type conversion");
            }
            
            if (node is ParenthesizedExpressionSyntax)
            {
                ParenthesizedExpressionSyntax pes = (ParenthesizedExpressionSyntax)node;
                return ResolveExpression(pes.Expression, into_lvalue, instructions);
            }
            if (node is PostfixUnaryExpressionSyntax)
            {
                PostfixUnaryExpressionSyntax pues = (PostfixUnaryExpressionSyntax)node;

                //pues.Operand;
                FlatOperand left = ResolveLValue(pues.Operand, instructions);
                if (into_lvalue == null)
                {
                    into_lvalue = this.AllocateRegister("");
                }

                FlatOperand left_lvalue = left.GetLValue(this, instructions);

                switch (pues.Kind)
                {
                    case SyntaxKind.PostIncrementExpression:
                        instructions.Add(FlatStatement.DUPLICATE(FlatOperand.Immediate(FlatValue.Int32(into_lvalue.OperandIndex)), left));
                        instructions.Add(FlatStatement.ADD(left_lvalue, left, FlatOperand.Immediate(FlatValue.Int32(1))));
                        // TODO: this should be trying to return immediatevalue+1...?
                        return into_lvalue;
                    case SyntaxKind.PostDecrementExpression:
                        instructions.Add(FlatStatement.DUPLICATE(FlatOperand.Immediate(FlatValue.Int32(into_lvalue.OperandIndex)), left));
                        instructions.Add(FlatStatement.SUB(left_lvalue, left, FlatOperand.Immediate(FlatValue.Int32(1))));
                        // TODO: this should be trying to return immediatevalue-1...?
                        return into_lvalue;
                }
                
                throw new NotImplementedException("postfix unary " + pues.Kind.ToString());
            }
            if (node is PrefixUnaryExpressionSyntax)
            {
                PrefixUnaryExpressionSyntax pues = (PrefixUnaryExpressionSyntax)node;
                FlatOperand right = ResolveExpression(pues.Operand, into_lvalue, instructions);
                FlatOperand left_lvalue;
                switch (pues.Kind)
                {
                    case SyntaxKind.PreIncrementExpression:
                        left_lvalue = ResolveLValue(pues.Operand, instructions);
                        instructions.Add(FlatStatement.ADD(left_lvalue.GetLValue(this, instructions), right, FlatOperand.Immediate(FlatValue.Int32(1))));
                        // TODO: this should be trying to return immediatevalue+1...?
                        return left_lvalue;
                    case SyntaxKind.PreDecrementExpression:
                        left_lvalue = ResolveLValue(pues.Operand, instructions);
                        instructions.Add(FlatStatement.SUB(left_lvalue.GetLValue(this, instructions), right, FlatOperand.Immediate(FlatValue.Int32(1))));
                        // T//ODO: this should be trying to return immediatevalue-1...?
                        return left_lvalue;
                    case SyntaxKind.NegateExpression:
                        if (into_lvalue != null)
                        {
                            instructions.Add(FlatStatement.NEGATE(into_lvalue, right));
                            return into_lvalue.AsRValue(right.ImmediateValue);
                        }
                        FlatOperand left = this.AllocateRegister("");
                        into_lvalue = left.GetLValue(this, instructions);
                        instructions.Add(FlatStatement.NEGATE(into_lvalue, right));
                        return left;
                }
            }
            if (node is BinaryExpressionSyntax)
            {
                BinaryExpressionSyntax bes = (BinaryExpressionSyntax)node;
                return ResolveExpression(bes, result_type, into_lvalue, instructions);


            }
            if (node is LiteralExpressionSyntax)
            {
                LiteralExpressionSyntax les = (LiteralExpressionSyntax)node;

                FlatValue val = FlatValue.FromLiteralToken(result_type.ConvertedType, les.Token);
                FlatOperand right = FlatOperand.Immediate(val);

                if (into_lvalue != null)
                {
                    instructions.Add(FlatStatement.REFERENCE(into_lvalue,right));
                    return into_lvalue.AsRValue(val);
                }
                return right;
            }
            if (node is IdentifierNameSyntax)
            {
                IdentifierNameSyntax ins = (IdentifierNameSyntax)node;
                return Resolve(ins, result_type, instructions);
            }
            if (node is InvocationExpressionSyntax)
            {
                InvocationExpressionSyntax ies = (InvocationExpressionSyntax)node;
                return Resolve(ies, result_type, into_lvalue, instructions);

            }
            if (node is MemberAccessExpressionSyntax)
            {
                MemberAccessExpressionSyntax meas = (MemberAccessExpressionSyntax)node;
                return Resolve(meas, result_type, into_lvalue, instructions);
            }
            if (node is ElementAccessExpressionSyntax)
            {
                ElementAccessExpressionSyntax eaes = (ElementAccessExpressionSyntax)node;
                return Resolve(eaes, result_type, into_lvalue, instructions);

            }
            if (node is ObjectCreationExpressionSyntax)
            {
                ObjectCreationExpressionSyntax oces = (ObjectCreationExpressionSyntax)node;
                return Resolve(oces, result_type, into_lvalue, instructions);
            }
            if (node is ArrayCreationExpressionSyntax)
            {
                ArrayCreationExpressionSyntax aces = (ArrayCreationExpressionSyntax)node;
                return Resolve(aces, result_type, into_lvalue, instructions);
            }

            throw new NotImplementedException();
        }

        public void Flatten(BlockSyntax node, List<FlatStatement> instructions)
        {
            PushVariableScope(instructions);
            foreach (StatementSyntax ss in node.Statements)
            {
                FlattenStatement(ss, instructions);
            }
            PopVariableScope(instructions);
        }

        public void Flatten(ReturnStatementSyntax node, List<FlatStatement> instructions)
        {
            if (node.Expression != null)
            {
                if (this.MethodSymbol.ReturnsVoid)
                {
                    throw new NotImplementedException("returning a value from a void function");
                }

                FlatOperand fop_return = ResolveExpression(node.Expression, null, instructions);
                instructions.Add(FlatStatement.REREFERENCE(FlatOperand.InputRef(0, FlatValue.Null()),fop_return));
            }

            instructions.Add(FlatStatement.RETURN());
        }

        public void Flatten(ExpressionStatementSyntax node, List<FlatStatement> instructions)
        {
            FlatOperand into_lvalue = null;
            ResolveExpression(node.Expression, into_lvalue, instructions);

            // nothing else to do
        }
        public void Flatten(LocalDeclarationStatementSyntax node, List<FlatStatement> instructions)
        {
            TypeInfo ti = Model.GetTypeInfo(node.Declaration.Type);

            foreach (VariableDeclaratorSyntax vds in node.Declaration.Variables)
            {
                Flatten(ti.Type, vds, instructions);
            }            
        }

        public void Flatten(VariableDeclarationSyntax node, List<FlatStatement> instructions)
        {
            /*
            public TypeSyntax Type { get; }
            public SeparatedSyntaxList<VariableDeclaratorSyntax> Variables { get; }
        /**/

            TypeInfo ti = Model.GetTypeInfo(node.Type);

            foreach (VariableDeclaratorSyntax vds in node.Variables)
            {                
                Flatten(ti.Type, vds, instructions);
            }
        }

        public void Flatten(TypeSymbol type, VariableDeclaratorSyntax vds, List<FlatStatement> instructions)
        {
            if (vds.ArgumentList != null)
            {
                throw new NotImplementedException("array variable");
            }

            // allocate a register for the variable
            FlatOperand fop_register = AllocateRegister("");

            CurrentVariableScope.Add(vds.Identifier.ToString(), fop_register.OperandIndex);

            // flatten the initializer into instructions
            if (vds.Initializer!=null && vds.Initializer.Value != null)
            {
                FlatOperand lvalue_register = fop_register.GetLValue(this, instructions);
                ResolveExpression(vds.Initializer.Value, lvalue_register, instructions);
            }
        }

        #region Exception handling
        public void Flatten(CatchClauseSyntax node, FlatOperand fop_exceptionType, string ehEndLabel, List<FlatStatement> instructions)
        {
            string catchPrefix = this.MakeUniqueLabelPrefix("catch");
            string catchendLabel = catchPrefix + "end";
            /*
        public BlockSyntax Block { get; }
        public SyntaxToken CatchKeyword { get; }
        public CatchDeclarationSyntax Declaration { get; }
/**/
            
            // JNE catchendLabel
            this.PushVariableScope(instructions);

            
            // declare local variable
            if (node.Declaration!=null)
            {
                TypeInfo ti = Model.GetTypeInfo(node.Declaration.Type);

                // TYPEOF exception
                FlatOperand fop_catchType = Resolve(ti.ConvertedType, null, instructions);

                instructions.Add(FlatStatement.JNE(FlatOperand.LabelRef(catchendLabel), fop_catchType, fop_exceptionType));


                FlatOperand fop_register = AllocateRegister("");

                CurrentVariableScope.Add(node.Declaration.Identifier.ToString(), fop_register.OperandIndex);

                // flatten the initializer into instructions
                {
                    FlatOperand lvalue_register = fop_register.GetLValue(this, instructions);
                    instructions.Add(FlatStatement.REFERENCE(lvalue_register,FlatOperand.ExceptionRef()));
                }
            }

            this.FlattenStatement(node.Block, instructions);
            
            this.PopVariableScope(instructions);

            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(ehEndLabel)));
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(catchendLabel)));
        }


        public void Flatten(TryStatementSyntax node, List<FlatStatement> instructions)
        {
            // set new exception handler
            string tryPrefix = this.MakeUniqueLabelPrefix("try");
            string ehbeginLabel = tryPrefix + "begin";
            string ehendLabel = tryPrefix + "end"; 
            string finallyLabel = tryPrefix + "finally";
            
            if (node.Finally != null)
            {
                instructions.Add(FlatStatement.TRY(FlatOperand.LabelRef(ehbeginLabel),FlatOperand.LabelRef(finallyLabel)));
            }
            else
            {
                instructions.Add(FlatStatement.TRY(FlatOperand.LabelRef(ehbeginLabel),ehendLabel));
            }

            Flatten(node.Block, instructions);

            // leave will be injected later!
            /*
            if (node.Finally != null)
            {
                instructions.Add(FlatStatement.LEAVE());
            }
            /**/
             
            // jump past exception handler
            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(ehendLabel)));

            // flatten exception handler
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(ehbeginLabel)));

            // get exception type
            if (node.Catches != null && node.Catches.Count>0)
            {
                FlatOperand fop_exceptionType = this.AllocateRegister("");
                {
                    FlatOperand lvalue_exceptionType = fop_exceptionType.GetLValue(this, instructions);
                    instructions.Add(FlatStatement.TYPEOF(lvalue_exceptionType, FlatOperand.ExceptionRef()));
                }

                foreach (CatchClauseSyntax ccs in node.Catches)
                {
                    this.Flatten(ccs, fop_exceptionType, ehendLabel, instructions);
                }
            }

            // leave will be injected later
            /*
            if (node.Finally != null)
            {
                instructions.Add(FlatStatement.LEAVE());
            }
            /**/

            instructions.Add(FlatStatement.THROW(FlatOperand.ExceptionRef()));

            if (node.Finally != null)
            {
                instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(finallyLabel)));

                this.Flatten(node.Finally.Block, instructions);

                instructions.Add(FlatStatement.ENDFINALLY());
            }

            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(ehendLabel)));
        }
        #endregion

        #region Conditional jumps
        public void FlattenCondition(ExpressionSyntax expr, string prefix, bool ss_jump_if_value, string ss_jump_label, List<FlatStatement> instructions)
        {
            if (expr is LiteralExpressionSyntax)
            {
                switch (expr.Kind)
                {
                    case SyntaxKind.TrueLiteralExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(ss_jump_label)));
                        return;
                    case SyntaxKind.FalseLiteralExpression:
                        if (!ss_jump_if_value)
                            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(ss_jump_label)));
                        return;
                }
                throw new NotImplementedException("literal expression " + expr.Kind.ToString());
            }
            if (expr is BinaryExpressionSyntax)
            {
                BinaryExpressionSyntax condition = expr as BinaryExpressionSyntax;


                switch (condition.Kind)
                {
                    case SyntaxKind.LogicalAndExpression:
                        {
                            FlattenCondition(condition.Left, prefix, ss_jump_if_value, ss_jump_label, instructions);
                            // fell through because left side didnt match. check right side
                            FlattenCondition(condition.Right, prefix, ss_jump_if_value, ss_jump_label, instructions);
                            // fell through because right side didnt match either.
                            return;
                        }
                        break;
                    case SyntaxKind.LogicalOrExpression:
                        {
                            string ifEnterLabel = prefix + MakeUniqueLabelPrefix("shortcircuit");
                            FlattenCondition(condition.Left, prefix, !ss_jump_if_value, ifEnterLabel, instructions); // short circuit if left side matches
                            // fell through because left side didn't match. check right side
                            FlattenCondition(condition.Right, prefix, ss_jump_if_value, ss_jump_label, instructions);

                            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(ifEnterLabel)));
                            return;
                        }
                        break;
                }

                FlatOperand opnd_left = ResolveExpression(condition.Left, null, instructions);
                FlatOperand opnd_right = ResolveExpression(condition.Right, null, instructions);

                switch (condition.Kind)
                {
                    case SyntaxKind.GreaterThanOrEqualExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JGE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        else
                            instructions.Add(FlatStatement.JL(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        return;
                    case SyntaxKind.GreaterThanExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JG(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        else
                            instructions.Add(FlatStatement.JLE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        return;
                    case SyntaxKind.LessThanExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JL(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        else
                            instructions.Add(FlatStatement.JGE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        return;
                    case SyntaxKind.LessThanOrEqualExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JLE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        else
                            instructions.Add(FlatStatement.JG(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        return;
                    case SyntaxKind.NotEqualsExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JNE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        else
                            instructions.Add(FlatStatement.JE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        return;
                    case SyntaxKind.EqualsExpression:
                        if (ss_jump_if_value)
                            instructions.Add(FlatStatement.JE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        else
                            instructions.Add(FlatStatement.JNE(FlatOperand.LabelRef(ss_jump_label), opnd_left, opnd_right));
                        return;
                    default:
                        throw new NotImplementedException("binary expression " + condition.Kind);
                }

                throw new NotImplementedException();
            }
            throw new NotImplementedException();
        }
        #endregion

        public void Flatten(IfStatementSyntax node, List<FlatStatement> instructions)
        {
            string ifPrefix = MakeUniqueLabelPrefix("if");
            string ifBeginLabel = ifPrefix + "begin";
            string ifElseLabel = ifPrefix + "else";
            string ifExitLabel = ifPrefix + "end"; //MakeUniqueLabelPrefix("exit");


            PushVariableScope(instructions);

            string firstJumpLabel;
            if (node.Else != null)
                firstJumpLabel = ifElseLabel;
            else
                firstJumpLabel = ifExitLabel;

            this.FlattenCondition(node.Condition, ifPrefix, false, firstJumpLabel, instructions);

            this.FlattenStatement(node.Statement, instructions);
            PopVariableScope(instructions);

            if (node.Else != null)
            {
                instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(ifExitLabel)));
                instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(ifElseLabel)));
                this.FlattenStatement(node.Else.Statement, instructions);
            }
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(ifExitLabel)));
        }
        #region Loops
        public void Flatten(ForStatementSyntax node, List<FlatStatement> instructions)
        {

            this.PushVariableScope(instructions);

            // variable declarations
            Flatten(node.Declaration, instructions);

            string forPrefix = this.MakeUniqueLabelPrefix("for");

            string conditionCheckLabel = forPrefix + "conditionCheck"; // WHERE THE CONTINUE GOES!

            string incrementLabel = forPrefix + "increment";
            string endForLabel = forPrefix + "end"; // WHERE THE BREAK GOES

            string wasContinue = this.SetContinueLabel(incrementLabel);
            string wasBreak = this.SetBreakLabel(endForLabel);

            string beginForLabel = forPrefix + "begin"; // WHERE YOUR MOM GOES

            // check conditions
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(conditionCheckLabel)));
            FlattenCondition(node.Condition, forPrefix, false, endForLabel, instructions);           

            // drop statement
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(beginForLabel)));
            this.FlattenStatement(node.Statement, instructions);
            

            // increment
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(incrementLabel)));

            foreach (ExpressionSyntax es in node.Incrementors)
            {
                FlatOperand fop_incresult = this.ResolveExpression(es, null, instructions);
            }

            // jump to check conditions
            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(conditionCheckLabel)));

            // end
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(endForLabel)));

            this.PopVariableScope(instructions);            

            this.SetContinueLabel(wasContinue);
            this.SetBreakLabel(wasBreak);            
        }

        public void Flatten(WhileStatementSyntax node, List<FlatStatement> instructions)
        {
            this.PushVariableScope(instructions);

            string whilePrefix = this.MakeUniqueLabelPrefix("while");

            string conditionCheckLabel = whilePrefix + "conditionCheck"; // WHERE THE CONTINUE GOES!

            string beginWhileLabel = whilePrefix + "begin"; 
            string endWhileLabel = whilePrefix + "end"; // WHERE THE BREAK GOES

            string wasContinue = this.SetContinueLabel(conditionCheckLabel);
            string wasBreak = this.SetBreakLabel(endWhileLabel);

            // check conditions
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(conditionCheckLabel)));
            FlattenCondition(node.Condition, whilePrefix, false, endWhileLabel, instructions);

            // drop statement
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(beginWhileLabel)));
            this.FlattenStatement(node.Statement, instructions);

            // end
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(endWhileLabel)));

            this.PopVariableScope(instructions);
            this.SetContinueLabel(wasContinue);
            this.SetBreakLabel(wasBreak);            
        }

        public void Flatten(DoStatementSyntax node, List<FlatStatement> instructions)
        {
            this.PushVariableScope(instructions);

            string doPrefix = this.MakeUniqueLabelPrefix("do");

            string conditionCheckLabel = doPrefix + "conditionCheck";  // WHERE THE CONTINUE GOES!

            string beginDoLabel = doPrefix + "begin";
            string endDoLabel = doPrefix + "end";  // WHERE THE BREAK GOES

            string wasContinue = this.SetContinueLabel(beginDoLabel);
            string wasBreak = this.SetBreakLabel(endDoLabel);

            // drop statement
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(beginDoLabel)));
            this.FlattenStatement(node.Statement, instructions);

            // check conditions
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(conditionCheckLabel)));
            FlattenCondition(node.Condition, doPrefix, true, beginDoLabel, instructions);

            // end
            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(endDoLabel)));

            this.PopVariableScope(instructions);
            this.SetContinueLabel(wasContinue);
            this.SetBreakLabel(wasBreak);
        }

        public void Flatten(ForEachStatementSyntax node, List<FlatStatement> instructions)
        {
            throw new NotImplementedException("foreach");
        }

        #endregion
        #region Switches
        public void FlattenSwitchAsBinaryConditions(SwitchStatementSyntax node, string prefix, string endSwitchLabel, string defaultCaseLabel, List<FlatStatement> instructions)
        {
            FlatOperand fop_subject = ResolveExpression(node.Expression, null, instructions);
            bool bHasDefault = false;
            foreach (SwitchSectionSyntax sss in node.Sections)
            {

                foreach (SwitchLabelSyntax sls in sss.Labels)
                {
                    if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.CaseKeyword)
                    {
                        FlatOperand fop_test = ResolveExpression(sls.Value, null, instructions);

                        string labelName = prefix + "case[" + sls.Value.ToString() + "]";
                        instructions.Add(FlatStatement.JE(FlatOperand.LabelRef(labelName), fop_subject, fop_test));
                        
                    }
                    else if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.DefaultKeyword)
                    {
                        bHasDefault = true;
                    }
                }
            }

            if (bHasDefault)
            {
                instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(defaultCaseLabel)));
            }
            else
            {
                instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(endSwitchLabel)));
            }

            int nSection = 1;
            foreach (SwitchSectionSyntax sss in node.Sections)
            {
                string sectionBeginLabel = prefix + "section" + nSection + "begin";
                string sectionEndLabel = prefix + "section" + nSection + "end";

                instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(sectionBeginLabel)));

                foreach (SwitchLabelSyntax sls in sss.Labels)
                {
                    if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.CaseKeyword)
                    {
                        string labelName = prefix + "case[" + sls.Value.ToString() + "]";
                        instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(labelName)));

                    }
                    else if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.DefaultKeyword)
                    {
                        instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(defaultCaseLabel)));
                    }
                }

                foreach (StatementSyntax ss in sss.Statements)
                {
                    FlattenStatement(ss, instructions);
                }

                FlatStatement fs = FlatStatement.THROW(FlatOperand.LiteralString("Illegal case fall-through, please use a break, return, goto case, etc"));
                fs.Comment = "this should be UNUSED and stripped during post-processing";
                instructions.Add(fs);

                instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(sectionEndLabel)));

                nSection++;
            }

        }

        public void FlattenStringSwitch(SwitchStatementSyntax node, string prefix, string endSwitchLabel, string defaultCaseLabel, List<FlatStatement> instructions)
        {
            FlatTableBuilder ftb = new FlatTableBuilder();
            bool bHasDefault = false;
            // build table of cases
            foreach (SwitchSectionSyntax sss in node.Sections)
            {
                foreach (SwitchLabelSyntax sls in sss.Labels)
                {
                    if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.CaseKeyword)
                    {
                        if (!(sls.Value is LiteralExpressionSyntax))
                        {
                            throw new NotImplementedException("non-literal string case");
                        }

                        
                        LiteralExpressionSyntax les = (LiteralExpressionSyntax)sls.Value;
                        FlatOperand fop = ResolveExpression(les, FlatObjectType.System_String);

                        string labelName = prefix + "case[" + fop.ImmediateValue.ValueText+"]";
                        ftb.Add(fop.ImmediateValue.Object.ToString(), FlatValue.Label(labelName)); // these will be post-processed to instruction numbers
                    }
                    else if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.DefaultKeyword)
                    {
                        bHasDefault = true;
                    }
                }
            }

            int nValue = FunctionValues.Count;
            FlatValue fv_table = ftb.GetFlatValue();
            FunctionValues.Add(fv_table);
            FlatOperand fop_functionvalue = FlatOperand.FunctionValueRef(nValue, fv_table);

            FlatOperand fop_comparison = ResolveExpression(node.Expression, null, instructions);
            instructions.Add(FlatStatement.SWITCH(fop_functionvalue,fop_comparison));
            if (bHasDefault)
            {
                instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(defaultCaseLabel)));
            }
            else
            {
                instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(endSwitchLabel)));
            }
            

            //string switchTableBuilderLabel = this.MakeUniqueLabelName("switchTableBuilder");

            int nSection = 1;

            foreach (SwitchSectionSyntax sss in node.Sections)
            {
                string sectionBeginLabel = prefix + "section" + nSection + "begin";
                string sectionEndLabel = prefix + "section" + nSection + "end";

                instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(sectionBeginLabel)));

                foreach (SwitchLabelSyntax sls in sss.Labels)
                {
                    if (sls.CaseOrDefaultKeyword.Kind == SyntaxKind.DefaultKeyword)
                    {
                        instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(defaultCaseLabel)));
                    }
                    else
                    {
                        LiteralExpressionSyntax les = (LiteralExpressionSyntax)sls.Value;
                        FlatOperand fop = ResolveExpression(les, FlatObjectType.System_String);

                        string labelName = prefix + "case[" + fop.ImmediateValue.ValueText+"]";
                        instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(labelName)));
                    }
                }

                foreach (StatementSyntax ss in sss.Statements)
                {
                    FlattenStatement(ss, instructions);
                }

                FlatStatement fs = FlatStatement.THROW(FlatOperand.LiteralString("Illegal case fall-through, please use a break, return, goto case, etc"));
                fs.Comment = "this should be UNUSED and stripped during post-processing";
                instructions.Add(fs);
                /*
                        // Summary:
                        //     Gets a SyntaxList of SwitchLabelSyntax's the represents the possible labels
                        //     that control can transfer to within the section.
                        public SyntaxList<SwitchLabelSyntax> Labels { get; }
                        //
                        // Summary:
                        //     Gets a SyntaxList of StatementSyntax's the represents the statements to be
                        //     executed when control transfer to a label the belongs to the section.
                        public SyntaxList<StatementSyntax> Statements { get; }
                /**/
                instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(sectionEndLabel)));

                nSection++;
            }
        }

        public void Flatten(SwitchStatementSyntax node, List<FlatStatement> instructions)
        {
        /*
        // Summary:
        //     Gets a SyntaxToken that represents the open braces succeeding the switch
        //     sections.
        public SyntaxToken CloseBraceToken { get; }
        //
        // Summary:
        //     Gets a SyntaxToken that represents the close parenthesis succeeding the switch
        //     expression.
        public SyntaxToken CloseParenToken { get; }
        //
        // Summary:
        //     Gets an ExpressionSyntax representing the expression of the switch statement.
        public ExpressionSyntax Expression { get; }
        //
        // Summary:
        //     Gets a SyntaxToken that represents the open braces preceding the switch sections.
        public SyntaxToken OpenBraceToken { get; }
        //
        // Summary:
        //     Gets a SyntaxToken that represents the open parenthesis preceding the switch
        //     expression.
        public SyntaxToken OpenParenToken { get; }
        //
        // Summary:
        //     Gets a SyntaxList of SwitchSectionSyntax's that represents the switch sections
        //     of the switch statement.
        public SyntaxList<SwitchSectionSyntax> Sections { get; }
        //
        // Summary:
        //     Gets a SyntaxToken that represents the switch keyword.
        public SyntaxToken SwitchKeyword { get; }
        /**/
            TypeInfo ti = Model.GetTypeInfo(node.Expression);

            string switchPrefix = this.MakeUniqueLabelPrefix("switch");

            string beginSwitchLabel = switchPrefix + "begin"; 
            string endSwitchLabel = switchPrefix + "end"; // WHERE THE BREAK GOES
            string defaultCaseLabel = switchPrefix + "default";
            string wasBreak = this.SetBreakLabel(endSwitchLabel);

            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(beginSwitchLabel)));
            this.PushVariableScope(instructions);

            switch (ti.ConvertedType.SpecialType)
            {
                case SpecialType.System_String:
                    FlattenStringSwitch(node, switchPrefix, endSwitchLabel, defaultCaseLabel, instructions);
                    break;
                default:
                    // there's a lot of ways to do this.
                    // individual if's?
                    // jump array?
                    // jump table? (strings)

                    // here's the default fallback implementation!
                    FlattenSwitchAsBinaryConditions(node, switchPrefix, endSwitchLabel, defaultCaseLabel, instructions);
                    break;
            }

            instructions.Add(FlatStatement.LABEL(FlatOperand.LabelRef(endSwitchLabel)));
            this.PopVariableScope(instructions);
            this.SetBreakLabel(wasBreak);

        }
        #endregion

        public void Flatten(BreakStatementSyntax node, List<FlatStatement> instructions)
        {
            if (string.IsNullOrEmpty(this.CurrentBreakLabel))
            {
                throw new NotSupportedException("break without context");
            }
            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(CurrentBreakLabel)));
        }

        public void Flatten(ContinueStatementSyntax node, List<FlatStatement> instructions)
        {
            if (string.IsNullOrEmpty(this.CurrentContinueLabel))
            {
                throw new NotSupportedException("continue without context");
            }
            instructions.Add(FlatStatement.JMP(FlatOperand.LabelRef(CurrentContinueLabel)));
        }

        public void Flatten(GotoStatementSyntax node, List<FlatStatement> instructions)
        {
            /*        
             * public SyntaxToken CaseOrDefaultKeyword { get; }
        //
        // Summary:
        //     Gets a constant expression for a goto case statement.
        public ExpressionSyntax Expression { get; }
        //
        // Summary:
        //     Gets a SyntaxToken that represents the goto keyword.
        public SyntaxToken GotoKeyword { get; }
        //
        // Summary:
        //     Gets a SyntaxToken that represents the semi-colon at the end of the statement.
        public SyntaxToken SemicolonToken { get; }
             */
            switch(node.CaseOrDefaultKeyword.Kind)
            {
                case SyntaxKind.DefaultKeyword:
                    break;
                case SyntaxKind.CaseKeyword:
                    break;
                default:
                    break;
            }
            throw new NotImplementedException("goto");
        }
        public void Flatten(UsingStatementSyntax node, List<FlatStatement> instructions)
        {
            /*
        public SyntaxToken CloseParenToken { get; }
        public VariableDeclarationSyntax Declaration { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken OpenParenToken { get; }
        public StatementSyntax Statement { get; }
        public SyntaxToken UsingKeyword { get; }
             */
            
            throw new NotImplementedException("using");
        }

        public void Flatten(LockStatementSyntax node, List<FlatStatement> instructions)
        {
            /*
        public SyntaxToken CloseParenToken { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken LockKeyword { get; }
        public SyntaxToken OpenParenToken { get; }
        public StatementSyntax Statement { get; }
             */
            throw new NotImplementedException("lock");
        }




        public void FlattenStatement(StatementSyntax ss, List<FlatStatement> instructions)
        {
            switch (ss.Kind)
            {
                case SyntaxKind.Block:
                    Flatten((BlockSyntax)ss,instructions);
                    return;
                case SyntaxKind.ExpressionStatement:
                    Flatten((ExpressionStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.ReturnStatement:
                    Flatten((ReturnStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.LocalDeclarationStatement:
                    Flatten((LocalDeclarationStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.ForStatement:
                    Flatten((ForStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.IfStatement:
                    Flatten((IfStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.BreakStatement:
                    Flatten((BreakStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.ContinueStatement:
                    Flatten((ContinueStatementSyntax)ss, instructions);
                    return;
                case SyntaxKind.TryStatement:
                    Flatten((TryStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.DoStatement:
                    Flatten((DoStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.WhileStatement:
                    Flatten((WhileStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.EmptyStatement:
                    // do nothing. :)
                    return;
                case SyntaxKind.SwitchStatement:
                    Flatten((SwitchStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.ThrowStatement:
                    Flatten((ThrowStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.ForEachStatement:
                    Flatten((ForEachStatementSyntax)ss,instructions);
                    return;
                case SyntaxKind.LockStatement:
                    Flatten((LockStatementSyntax)ss, instructions);
                    return;
                case SyntaxKind.UsingStatement:
                    Flatten((UsingStatementSyntax)ss, instructions);
                    return;
                case SyntaxKind.GotoStatement:
                case SyntaxKind.GotoCaseStatement:
                case SyntaxKind.GotoDefaultStatement:
                    Flatten((GotoStatementSyntax)ss, instructions);
                    return;

                case SyntaxKind.CheckedStatement:
                case SyntaxKind.FixedStatement:
                case SyntaxKind.GlobalStatement:
                case SyntaxKind.LabeledStatement:

                case SyntaxKind.UncheckedStatement:
                case SyntaxKind.UnsafeStatement:
                case SyntaxKind.YieldBreakStatement:
                case SyntaxKind.YieldReturnStatement:
                    throw new NotImplementedException("statement type " + ss.GetType().ToString() + ": " + ss.Kind.ToString());
                    break;
                default:
                    throw new NotImplementedException("statement type " + ss.GetType().ToString() + ": " + ss.Kind.ToString());
                    break;
            }
        }
    }
}
