using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Generator.Marshallers;

internal sealed class ArrayOfInterfaceMarshaller : ArrayMarshallerBase
{
    public override bool CanMarshal(CsMarshalBase csElement) => csElement.IsArray && csElement.IsInterface;

    public override StatementSyntax GenerateManagedToNative(CsMarshalBase csElement, bool singleStackFrame) =>
        csElement switch
        {
            CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                IfStatement(BinaryExpression(SyntaxKind.GreaterThanExpression, GeneratorHelpers.NullableLengthExpression(IdentifierName(csElement.Name)), ZeroLiteral),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.Name)),
                                ParseExpression($"(System.IntPtr*) System.Runtime.InteropServices.NativeMemory.Alloc((nuint) (System.Runtime.CompilerServices.Unsafe.SizeOf<System.IntPtr>() * {csElement.Name}.Length))"))),
                        LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => MarshalInterfaceInstanceToNative(csElement, publicElement, marshalElement)),
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.ArraySpecification?.SizeIdentifier)),
                                ParseExpression($"({csElement.ArraySpecification?.TypeSizeIdentifier}){csElement.Name}.Length")))
                )),
            CsField { ArraySpecification.Type: ArraySpecificationType.Constant } =>
             FixedStatement(
                    VariableDeclaration(
                    ParseTypeName($"{csElement.MarshalType.QualifiedName}*"),
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
                ), LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => MarshalInterfaceInstanceToNative(csElement, publicElement, ParseExpression($"({PtrIdentifier.Text})[i]")))),
            _ => LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => MarshalInterfaceInstanceToNative(csElement, publicElement, marshalElement))
        };

    public override StatementSyntax GenerateNativeCleanup(CsMarshalBase csElement, bool singleStackFrame) =>
         csElement switch
         {
             CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                Block(
                    GenerateGCKeepAlive(csElement),
                    GenerateNativeMemoryFree(csElement)
                ),
             _ => GenerateGCKeepAlive(csElement)
         };

    public override StatementSyntax GenerateNativeToManaged(CsMarshalBase csElement, bool singleStackFrame) =>
        csElement switch
        {
            CsParameter { IsFast: true, IsOut: true } => GenerateNullCheckIfNeeded(
                csElement,
                ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            GlobalNamespace.GetTypeNameSyntax(WellKnownName.MarshallingHelpers),
                            GenericName(Identifier("ConvertToInterfaceArrayFast"))
                               .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SingletonSeparatedList<TypeSyntax>(
                                            IdentifierName(csElement.PublicType.QualifiedName)
                                        )
                                    )
                                )
                        ),
                        ArgumentList(
                            SeparatedList(new[]
                                {
                                    // ReadOnlySpan<IntPtr> pointers, Span<TCallback> interfaces
                                    Argument(GetMarshalStorageLocation(csElement)),
                                    Argument(IdentifierName(csElement.Name))
                                }
                            )
                        )
                    )
                )
            ),
            CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                IfStatement(BinaryExpression(SyntaxKind.GreaterThanExpression, MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.ArraySpecification?.SizeIdentifier)), ZeroLiteral),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(csElement.Name),
                                ParseExpression($"new {csElement.PublicType.QualifiedName}[@ref.{csElement.ArraySpecification?.SizeIdentifier}]"))),
                        LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => MarshalInterfaceInstanceFromNative(csElement, publicElement, marshalElement))
                )),
            CsField { ArraySpecification.Type: ArraySpecificationType.Constant } => FixedStatement(
                    VariableDeclaration(
                    ParseTypeName($"{csElement.MarshalType.QualifiedName}*"),
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
                ), LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => MarshalInterfaceInstanceFromNative(csElement, publicElement, ParseExpression($"({PtrIdentifier.Text})[i]")))),
            _ => LoopThroughArrayParameter(csElement, (publicElement, marshalElement) => MarshalInterfaceInstanceFromNative(csElement, publicElement, marshalElement))
        };

    protected override TypeSyntax GetMarshalElementTypeSyntax(CsMarshalBase csElement) => IntPtrType;

    public ArrayOfInterfaceMarshaller(Ioc ioc) : base(ioc)
    {
    }
}