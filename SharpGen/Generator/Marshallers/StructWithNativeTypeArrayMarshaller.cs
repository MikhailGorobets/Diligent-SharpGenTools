using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Generator.Marshallers;

internal sealed class StructWithNativeTypeArrayMarshaller : ArrayMarshallerBase
{
    public override bool CanMarshal(CsMarshalBase csElement) => csElement.HasNativeValueType && csElement.IsArray;

    public override StatementSyntax GenerateManagedToNative(CsMarshalBase csElement, bool singleStackFrame) =>
        csElement switch
        {
            CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                IfStatement(BinaryExpression(SyntaxKind.GreaterThanExpression, GeneratorHelpers.NullableLengthExpression(IdentifierName(csElement.Name)), ZeroLiteral),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.Name)),
                                ParseExpression($"({csElement.PublicType.QualifiedName}.__Native*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint) (System.Runtime.CompilerServices.Unsafe.SizeOf<{csElement.PublicType.QualifiedName}.__Native>() * {csElement.Name}.Length))"))),
                        LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => GenerateMarshalStructManagedToNative(csElement, publicElement, marshalElement)),
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.ArraySpecification?.SizeIdentifier)),
                                ParseExpression($"({csElement.ArraySpecification?.TypeSizeIdentifier}){csElement.Name}.Length")))
                )),
            CsField { ArraySpecification.Type: ArraySpecificationType.Constant } => FixedStatement(
                    VariableDeclaration(
                    ParseTypeName($"{csElement.PublicType.QualifiedName}.__Native*"),
                    SingletonSeparatedList(
                        VariableDeclarator(PtrIdentifier)
                           .WithInitializer(
                                EqualsValueClause(
                                    PrefixUnaryExpression(
                                        SyntaxKind.AddressOfExpression,
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName),
                                                IdentifierName(csElement.Name)
                                        )
                                    )
                                )
                            )
                    )
                ), LoopThroughArrayParameter(
                    csElement,
                    (publicElement, marshalElement) =>
                        GenerateMarshalStructManagedToNative(csElement, publicElement, ParseExpression($"({PtrIdentifier.Text})[i]"))
                    )),
            _ => LoopThroughArrayParameter(
                csElement,
                (publicElement, marshalElement) =>
                    GenerateMarshalStructManagedToNative(csElement, publicElement, marshalElement))
        };

    public override StatementSyntax GenerateNativeCleanup(CsMarshalBase csElement, bool singleStackFrame) =>
         csElement switch
         {
             CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                Block(
                      LoopThroughArrayParameter(
                          csElement,
                          (publicElement, marshalElement) =>
                              CreateMarshalStructStatement(csElement, StructMarshalMethod.Free, publicElement, marshalElement)),
                      GenerateNativeMemoryFree(csElement)
                ),
             CsField { ArraySpecification.Type: ArraySpecificationType.Constant } => FixedStatement(
                    VariableDeclaration(
                    ParseTypeName($"{csElement.PublicType.QualifiedName}.__Native*"),
                    SingletonSeparatedList(
                        VariableDeclarator(PtrIdentifier)
                           .WithInitializer(
                                EqualsValueClause(
                                    PrefixUnaryExpression(
                                        SyntaxKind.AddressOfExpression,
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName),
                                                IdentifierName(csElement.Name)
                                        )
                                    )
                                )
                            )
                    )
                ), LoopThroughArrayParameter(
                    csElement,
                    (publicElement, marshalElement) =>
                        CreateMarshalStructStatement(csElement, StructMarshalMethod.Free, publicElement, ParseExpression($"({PtrIdentifier.Text})[i]"))
                    )),
             _ => LoopThroughArrayParameter(
                    csElement,
                    (publicElement, marshalElement) =>
                        CreateMarshalStructStatement(csElement, StructMarshalMethod.Free, publicElement, marshalElement))
         };

    public override StatementSyntax GenerateNativeToManaged(CsMarshalBase csElement, bool singleStackFrame) =>
         csElement switch
         {
             CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                IfStatement(BinaryExpression(SyntaxKind.GreaterThanExpression, MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.ArraySpecification?.SizeIdentifier)), ZeroLiteral),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(csElement.Name),
                                ParseExpression($"new {csElement.PublicType.QualifiedName}[@ref.{csElement.ArraySpecification?.SizeIdentifier}]"))),
                        LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => CreateMarshalStructStatement(csElement, StructMarshalMethod.From, publicElement, marshalElement))
                )),
             CsField { ArraySpecification.Type: ArraySpecificationType.Constant } =>
                FixedStatement(
                    VariableDeclaration(
                    ParseTypeName($"{csElement.PublicType.QualifiedName}.__Native*"),
                    SingletonSeparatedList(
                        VariableDeclarator(PtrIdentifier)
                           .WithInitializer(
                                EqualsValueClause(
                                    PrefixUnaryExpression(
                                        SyntaxKind.AddressOfExpression,
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName),
                                                IdentifierName(csElement.Name)
                                        )
                                    )
                                )
                            )
                    )
                ), LoopThroughArrayParameter(
                    csElement,
                    (publicElement, marshalElement) =>
                        CreateMarshalStructStatement(csElement, StructMarshalMethod.From, publicElement, ParseExpression($"({PtrIdentifier.Text})[i]"))
                    )),
            _ => LoopThroughArrayParameter(
                    csElement,
                    (publicElement, marshalElement) =>
                        CreateMarshalStructStatement(csElement, StructMarshalMethod.From, publicElement, marshalElement))
         };

    protected override TypeSyntax GetMarshalElementTypeSyntax(CsMarshalBase csElement) =>
        ParseTypeName($"{csElement.PublicType.QualifiedName}.__Native");

    public StructWithNativeTypeArrayMarshaller(Ioc ioc) : base(ioc)
    {
    }
}