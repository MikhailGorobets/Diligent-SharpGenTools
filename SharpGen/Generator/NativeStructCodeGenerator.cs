using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Logging;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Generator;

internal sealed class NativeStructCodeGenerator : MemberMultiCodeGeneratorBase<CsStruct>
{
    private static readonly NameSyntax StructLayoutAttributeName = ParseName("System.Runtime.InteropServices.StructLayoutAttribute");
    private const string StructLayoutKindName = "System.Runtime.InteropServices.LayoutKind.";
    private static readonly AttributeArgumentSyntax StructLayoutExplicit = AttributeArgument(ParseName(StructLayoutKindName + "Explicit"));
    private static readonly AttributeArgumentSyntax StructLayoutSequential = AttributeArgument(ParseName(StructLayoutKindName + "Sequential"));
    private static readonly NameEqualsSyntax StructLayoutPackName = NameEquals(IdentifierName("Pack"));
    private static readonly SyntaxToken MarshalParameterRefName = Identifier("@ref");

    private static readonly AttributeArgumentSyntax StructLayoutCharset = AttributeArgument(
        ParseName("System.Runtime.InteropServices.CharSet.Unicode")
    ).WithNameEquals(NameEquals(IdentifierName("CharSet")));

    public override IEnumerable<MemberDeclarationSyntax> GenerateCode(CsStruct csStruct)
    {
        IEnumerable<MemberDeclarationSyntax> GenerateMarshalStructFieldBase()
        {
            if (csStruct.BaseObject != null)
            {
                yield return FieldDeclaration(
                        VariableDeclaration(ParseTypeName(csStruct.BaseObject.QualifiedName + ".__Native"))
                            .AddVariables(VariableDeclarator("Base")))
                    .AddModifiers(Token(SyntaxKind.PublicKeyword));
            }
        }

        IEnumerable<MemberDeclarationSyntax> GenerateMarshalStructField(CsField field)
        {
            var fieldDecl = FieldDeclaration(VariableDeclaration(ParseTypeName(field.MarshalType.QualifiedName)))
               .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));

            if (csStruct.ExplicitLayout)
                fieldDecl = AddFieldOffsetAttribute(fieldDecl, field.Offset);

            if (field.ArraySpecification is { } arraySpecification)
            {
                FieldDeclarationSyntax ComputeType(ArraySpecification? arraySpec, string typeName, string fieldName, bool hasNativeValueType)
                {
                    var qualifiedName = typeName;
                    if (hasNativeValueType) qualifiedName += ".__Native";
                    if (arraySpec?.Type == ArraySpecificationType.Dynamic) qualifiedName += "*";

                    return fieldDecl.WithDeclaration(VariableDeclaration(ParseTypeName(qualifiedName), SingletonSeparatedList(VariableDeclarator(fieldName))));
                }

                yield return ComputeType(field.ArraySpecification, field.MarshalType.QualifiedName, field.Name, field.HasNativeValueType);

                if (arraySpecification.Dimension is { } dimension)
                    for (var i = 1; i < dimension; i++)
                    {
                        var declaration = ComputeType(field.ArraySpecification, field.MarshalType.QualifiedName, $"__{field.Name}{i}", field.HasNativeValueType);

                        if (csStruct.ExplicitLayout)
                        {
                            var offset = field.Offset + i * field.Size / dimension;
                            declaration = AddFieldOffsetAttribute(declaration, offset, true);
                        }

                        yield return declaration;
                    }
                else if (arraySpecification.Type != ArraySpecificationType.Dynamic || arraySpecification is { Type: ArraySpecificationType.Dynamic, SizeIdentifier: null })
                    Logger.Warning(LoggingCodes.UnknownArrayDimension, $"Unknown array dimensions for [{field.QualifiedName}]");
            }
            else if (field.HasNativeValueType)
            {
                var qualifiedName = field.MarshalType.QualifiedName;
                qualifiedName += ".__Native";
                if (field.IsOptionalPointer) qualifiedName += "*";

                yield return fieldDecl.WithDeclaration(
                    VariableDeclaration(
                        ParseTypeName(qualifiedName),
                        SingletonSeparatedList(VariableDeclarator(field.Name))
                    )
                );
            }
            else if (field.PublicType.IsWellKnownType(GlobalNamespace, WellKnownName.FunctionCallback))
            {
                yield return fieldDecl.WithDeclaration(
                    VariableDeclaration(
                        GeneratorHelpers.IntPtrType,
                        SingletonSeparatedList(VariableDeclarator(field.Name))
                    )
                );
            }
            else
            {
                var qualifiedName = field.MarshalType.QualifiedName;
                if (field.IsOptionalPointer) qualifiedName += "*";

                yield return fieldDecl.WithDeclaration(
                    VariableDeclaration(
                        ParseTypeName(qualifiedName),
                        SingletonSeparatedList(VariableDeclarator(field.Name))
                    )
                ); ;
            }
        }

        yield return StructDeclaration("__Native")
                    .WithModifiers(
                        csStruct.BaseObject != null
                            ? TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.UnsafeKeyword), Token(SyntaxKind.NewKeyword))
                            : TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.UnsafeKeyword), Token(SyntaxKind.PartialKeyword))
                    )
                    .WithAttributeLists(SingletonList(GenerateStructLayoutAttribute(csStruct)))
                    .WithMembers(List(GenerateMarshalStructFieldBase().Concat(csStruct.Fields.SelectMany(GenerateMarshalStructField))));

        if (csStruct.GenerateAsClass)
        {
            var methodName = IdentifierName("__MarshalFrom");
            var marshalArgument = Argument(IdentifierName(MarshalParameterRefName))
               .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword));

            var invocationExpression = csStruct.IsStaticMarshal
                                           ? InvocationExpression(
                                               methodName,
                                               ArgumentList(
                                                   SeparatedList(
                                                       new[]
                                                       {
                                                           Argument(ThisExpression())
                                                              .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword)),
                                                           marshalArgument
                                                       }
                                                   )
                                               )
                                           )
                                           : InvocationExpression(
                                               methodName,
                                               ArgumentList(SingletonSeparatedList(marshalArgument))
                                           );

            yield return ConstructorDeclaration(csStruct.Name)
                        .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword)))
                        .WithParameterList(MarshalParameterListSyntax)
                        .WithBody(Block(ExpressionStatement(invocationExpression)));
        }

        yield return GenerateMarshalFree(csStruct);
        yield return GenerateMarshalFrom(csStruct);
        yield return GenerateMarshalTo(csStruct);
    }

    internal static AttributeListSyntax GenerateStructLayoutAttribute(CsStruct csElement) => AttributeList(
        SingletonSeparatedList(
            Attribute(
                StructLayoutAttributeName,
                AttributeArgumentList(
                    SeparatedList(
                        new[]
                        {
                            csElement.ExplicitLayout ? StructLayoutExplicit : StructLayoutSequential,
                            AttributeArgument(
                                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.Align))
                                )
                               .WithNameEquals(StructLayoutPackName),
                            StructLayoutCharset
                        }
                    )
                )
            )
        )
    );

    private MethodDeclarationSyntax GenerateMarshalFree(CsStruct csStruct)
    {
        IEnumerable<StatementSyntax> FieldMarshallers(CsField field)
        {
            yield return GetMarshaller(field)?.GenerateNativeCleanup(field, false);
        }

        return GenerateMarshalMethod(
            "__MarshalFree",
            csStruct.BaseObject != null,
            csStruct.Fields,
            FieldMarshallers
        );
    }

    private IEnumerable<StatementSyntax> GenerateMarshalToNativeForDiligentCallback(CsField field)
    {
        yield return ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("@ref"), IdentifierName(field.Name)),
                ConditionalExpression(BinaryExpression(SyntaxKind.NotEqualsExpression, IdentifierName(field.Name), LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    IdentifierName($"NativePFN{field.Name}"), IdentifierName("System.IntPtr.Zero")))
        );

        yield return ExpressionStatement(
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("@ref"),
                IdentifierName(field.DiligentCallback!.IdentifierReferenceName)
            ), ConditionalExpression(BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                IdentifierName(field.Name),
                LiteralExpression(SyntaxKind.NullLiteralExpression)
            ), InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System.Runtime.InteropServices.Marshal"),
                    IdentifierName("GetFunctionPointerForDelegate")
                ),
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            IdentifierName(field.Name)
                        )
                    )
                )
            ), IdentifierName("System.IntPtr.Zero")))
        );
    }

    private MethodDeclarationSyntax GenerateMarshalTo(CsStruct csStruct)
    {
        IEnumerable<StatementSyntax> FieldMarshallers(CsField field)
        {
            if (field.DiligentCallback != null)
            {
                foreach (var item in GenerateMarshalToNativeForDiligentCallback(field))
                    yield return item;
                yield break;
            }

            if (field.Relations.Count == 0 && csStruct.CallbacksFields.All(e => e.DiligentCallback!.IdentifierReferenceName != field.Name))
            {
                yield return GetMarshaller(field).GenerateManagedToNative(field, false);
                yield break;
            }

            foreach (var relation in field.Relations)
            {
                var marshaller = GetRelationMarshaller(relation);
                if (relation is not LengthRelation)
                    yield return marshaller.GenerateManagedToNative(null, field);
            }
        }

        return GenerateMarshalMethod(
            "__MarshalTo",
            csStruct.BaseObject != null,
            csStruct.Fields,
            FieldMarshallers
        );
    }

    private MethodDeclarationSyntax GenerateMarshalFrom(CsStruct csStruct)
    {
        IEnumerable<StatementSyntax> FieldMarshallers(CsField field)
        {
            //TODO
            if (field.DiligentCallback != null)
                yield break;

            if (field.Relations.Count == 0 && csStruct.CallbacksFields.All(e => e.DiligentCallback!.IdentifierReferenceName != field.Name))
                yield return GetMarshaller(field).GenerateNativeToManaged(field, false);
        }

        return GenerateMarshalMethod(
            "__MarshalFrom",
            csStruct.BaseObject != null,
            csStruct.PublicFields,
            FieldMarshallers);
    }

    private static ParameterListSyntax MarshalParameterListSyntax => ParameterList(
        SingletonSeparatedList(Parameter(MarshalParameterRefName).WithType(RefType(ParseTypeName("__Native"))))
    );

    private MethodDeclarationSyntax GenerateMarshalMethod<T>(string name, bool generateBase, IEnumerable<T> source,
                                                             Func<T, IEnumerable<StatementSyntax>> transform)
        where T : CsMarshalBase
    {

        IEnumerable<StatementSyntax> BaseFieldMarshaller(string methodName, bool isHasBase)
        {
            if (isHasBase)
            {
                var arg = Argument(RefExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("@ref"), IdentifierName("Base"))));
                var invoke = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, BaseExpression(), IdentifierName(methodName)),
                    ArgumentList().AddArguments(arg)
                );
                yield return ExpressionStatement(invoke);
            }
        }

        var list = NewStatementList;
        list.AddRange(BaseFieldMarshaller(name, generateBase));
        list.AddRange(source, transform);
        return GenerateMarshalMethod(name, list);
    }

    private static MethodDeclarationSyntax GenerateMarshalMethod(string name, StatementSyntaxList body) =>
        MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), name)
           .WithParameterList(MarshalParameterListSyntax)
           .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.UnsafeKeyword)))
           .WithBody(body.ToBlock());

    public NativeStructCodeGenerator(Ioc ioc) : base(ioc)
    {
    }
}