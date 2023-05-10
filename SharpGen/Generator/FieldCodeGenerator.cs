using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Generator;

internal sealed partial class FieldCodeGenerator : MemberMultiCodeGeneratorBase<CsField>
{
    private readonly bool explicitLayout;

    public FieldCodeGenerator(Ioc ioc, bool explicitLayout) : base(ioc)
    {
        this.explicitLayout = explicitLayout;
    }

    public override IEnumerable<MemberDeclarationSyntax> GenerateCode(CsField csElement)
    {
        if (csElement.IsBoolToInt && !csElement.IsArray)
        {
            yield return GenerateBackingField(csElement, csElement.MarshalType);

            yield return GenerateProperty(
                csElement, PredefinedType(Token(SyntaxKind.BoolKeyword)),
                GeneratorHelpers.GenerateIntToBoolConversion,
                (_, value) => GeneratorHelpers.CastExpression(
                    ParseTypeName(csElement.MarshalType.QualifiedName),
                    GeneratorHelpers.GenerateBoolToIntConversion(value)
                )
            );
        }
        else if (!csElement.IsString && csElement.ArraySpecification?.Type == ArraySpecificationType.Constant)
        {
            var elementType = ParseTypeName(csElement.PublicType.QualifiedName);

            var fieldDecl = FieldDeclaration(
                    VariableDeclaration(ArrayType(elementType, SingletonList(ArrayRankSpecifier())),
                        SingletonSeparatedList(
                            VariableDeclarator(Identifier(csElement.IntermediateMarshalName)))))
                    .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword)));

            yield return AddDocumentationTrivia(fieldDecl, csElement);

            var indexVariable = Identifier("i");
            var indexVariableName = IdentifierName("i");
            var arrayIdentifierName = IdentifierName("value");

            var assign = ParseStatement($"{csElement.IntermediateMarshalName} ??= new {csElement.PublicType.QualifiedName}[{csElement.ArrayDimensionValue}];");

            var assert = ExpressionStatement(
                InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("System.Diagnostics"),
                                IdentifierName("Debug")),
                            IdentifierName("Assert")))
                    .AddArgumentListArguments(
                        Argument(
                            BinaryExpression(
                                SyntaxKind.LessThanOrEqualExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, arrayIdentifierName, IdentifierName("Length")),
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(csElement.IntermediateMarshalName), IdentifierName("Length"))))));


            var loop = ForStatement(
                VariableDeclaration(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    SeparatedList(new[] { VariableDeclarator(indexVariable, default, EqualsValueClause(GeneratorHelpers.ZeroLiteral)) })),
                default,
                BinaryExpression(SyntaxKind.LessThanExpression, indexVariableName,
                    GeneratorHelpers.LengthExpression(arrayIdentifierName)),
                SingletonSeparatedList<ExpressionSyntax>(
                    PrefixUnaryExpression(SyntaxKind.PreIncrementExpression, indexVariableName)
                ), ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        ElementAccessExpression(IdentifierName(csElement.IntermediateMarshalName), BracketedArgumentList(SingletonSeparatedList(Argument(indexVariableName)))),
                        ElementAccessExpression(arrayIdentifierName, BracketedArgumentList(SingletonSeparatedList(Argument(indexVariableName))))))
            );

            var setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithBody(Block(assign, assert, loop));

            var getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(assign, ReturnStatement(IdentifierName(csElement.IntermediateMarshalName))));

            yield return AddDocumentationTrivia(
                PropertyDeclaration(ArrayType(elementType, SingletonList(ArrayRankSpecifier())), csElement.Name)
                    .WithAccessorList(
                        AccessorList(
                            List(
                                new[]
                                {
                                        setter,
                                        getter
                                }
                            )
                        )
                    )
                    .WithModifiers(csElement.VisibilityTokenList),
                csElement
            );

        }
        else if (csElement.ArraySpecification?.Type == ArraySpecificationType.Dynamic)
        {
            yield return GenerateBackingField(csElement, csElement.PublicType, isArray: true, propertyBacking: false, document: true);
        }
        else if (csElement.DiligentCallback != null)
        {
            yield return AddDocumentationTrivia(FieldDeclaration(
                    VariableDeclaration(ParseTypeName(csElement.DiligentCallback.IdentifierType),
                        SingletonSeparatedList(
                            VariableDeclarator(Identifier(csElement.Name)))))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword))), csElement);

            yield return AddDocumentationTrivia(FieldDeclaration(
                    VariableDeclaration(ParseTypeName("System.IntPtr"))
                        .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier($"NativePFN{csElement.Name}"))
                            .WithInitializer(EqualsValueClause(ParseExpression($"(System.IntPtr)(delegate* unmanaged[Cdecl]<System.IntPtr, System.IntPtr, void>)(&{csElement.Name}Impl)"))))))
                .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword), Token(SyntaxKind.UnsafeKeyword))),
                    csElement);
        }
        else if (csElement.IsOptionalPointer)
        {
            var elementType = ParseTypeName(csElement.PublicType.QualifiedName);

            var fieldDecl = FieldDeclaration(
                    VariableDeclaration(NullableType(elementType),
                        SingletonSeparatedList(
                            VariableDeclarator(Identifier(csElement.Name)))))
                .WithModifiers(csElement.VisibilityTokenList);

            yield return AddDocumentationTrivia(fieldDecl, csElement);
        }
        else if (csElement.IsBitField)
        {
            PropertyValueGetTransform getterTransform;
            PropertyValueSetTransform setterTransform;
            TypeSyntax propertyType;

            if (csElement.IsBoolBitField)
            {
                getterTransform = GeneratorHelpers.GenerateIntToBoolConversion;
                setterTransform = (_, value) => GeneratorHelpers.GenerateBoolToIntConversion(value);
                propertyType = PredefinedType(Token(SyntaxKind.BoolKeyword));
            }
            else
            {
                getterTransform = valueExpression => GeneratorHelpers.CastExpression(
                    ParseTypeName(csElement.PublicType.QualifiedName),
                    valueExpression
                );
                setterTransform = null;
                propertyType = ParseTypeName(csElement.PublicType.QualifiedName);
            }

            yield return GenerateBackingField(csElement, csElement.PublicType);

            var bitMask = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(csElement.BitMask));
            var bitOffset = LiteralExpression(
                SyntaxKind.NumericLiteralExpression, Literal(csElement.BitOffset)
            );

            yield return GenerateProperty(
                csElement, propertyType,
                Compose(
                    getterTransform,
                    value => BinaryExpression(
                        SyntaxKind.BitwiseAndExpression,
                        GeneratorHelpers.WrapInParentheses(
                            BinaryExpression(SyntaxKind.RightShiftExpression, value, bitOffset)
                        ),
                        bitMask
                    )
                ),
                Compose(
                    (oldValue, value) => GeneratorHelpers.CastExpression(
                        ParseTypeName(csElement.PublicType.QualifiedName),
                        BinaryExpression(
                            SyntaxKind.BitwiseOrExpression,
                            GeneratorHelpers.WrapInParentheses(
                                BinaryExpression(
                                    SyntaxKind.BitwiseAndExpression,
                                    oldValue,
                                    PrefixUnaryExpression(
                                        SyntaxKind.BitwiseNotExpression,
                                        GeneratorHelpers.WrapInParentheses(
                                            BinaryExpression(SyntaxKind.LeftShiftExpression, bitMask, bitOffset)
                                        )
                                    )
                                )
                            ),
                            GeneratorHelpers.WrapInParentheses(
                                BinaryExpression(
                                    SyntaxKind.LeftShiftExpression,
                                    GeneratorHelpers.WrapInParentheses(
                                        BinaryExpression(SyntaxKind.BitwiseAndExpression, value, bitMask)
                                    ),
                                    bitOffset
                                )
                            )
                        )
                    ),
                    setterTransform
                )
            );
        }
        else
        {
            yield return GenerateBackingField(csElement, csElement.PublicType, propertyBacking: false, document: true);
        }
    }

    private MemberDeclarationSyntax GenerateBackingField(CsField field, CsTypeBase backingType,
                                                         bool isArray = false, bool propertyBacking = true,
                                                         bool document = false)
    {
        var elementType = ParseTypeName(backingType.QualifiedName);

        var fieldDecl = FieldDeclaration(
                VariableDeclaration(
                    isArray
                        ? ArrayType(elementType, SingletonList(ArrayRankSpecifier()))
                        : elementType,
                    SingletonSeparatedList(
                        VariableDeclarator(propertyBacking ? field.IntermediateMarshalName : field.Name)
                    )
                )
            )
           .WithModifiers(
                propertyBacking
                    ? TokenList(Token(SyntaxKind.InternalKeyword))
                    : field.VisibilityTokenList
            );

        if (explicitLayout)
            fieldDecl = AddFieldOffsetAttribute(fieldDecl, field.Offset);

        return document ? AddDocumentationTrivia(fieldDecl, field) : fieldDecl;
    }
}