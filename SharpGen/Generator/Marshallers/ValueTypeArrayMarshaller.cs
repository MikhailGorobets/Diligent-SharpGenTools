using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Generator.Marshallers;

internal sealed partial class ValueTypeArrayMarshaller : MarshallerBase, IMarshaller
{
    public bool CanMarshal(CsMarshalBase csElement) => csElement.IsValueType && csElement.IsArray &&
                                                       !csElement.MappedToDifferentPublicType;

    public ArgumentSyntax GenerateManagedArgument(CsParameter csElement) =>
        Argument(IdentifierName(csElement.Name));

    public ParameterSyntax GenerateManagedParameter(CsParameter csElement) =>
        GenerateManagedArrayParameter(csElement);

    public StatementSyntax GenerateManagedToNative(CsMarshalBase csElement, bool singleStackFrame)
    {
        const ArrayCopyDirection direction = ArrayCopyDirection.ManagedToNative;

        return csElement switch
        {
            CsParameter { IsLocalManagedReference: true } parameter => GenerateCopyBlock(parameter, direction),
            CsField { ArraySpecification.Type: ArraySpecificationType.Constant } => GenerateCopyMemory(csElement, direction),
            CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                 IfStatement(BinaryExpression(SyntaxKind.GreaterThanExpression, GeneratorHelpers.NullableLengthExpression(IdentifierName(csElement.Name)), ZeroLiteral),
                     Block(
                          ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.Name)),
                                ParseExpression($"({csElement.PublicType.QualifiedName}*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint) (System.Runtime.CompilerServices.Unsafe.SizeOf<{csElement.PublicType.QualifiedName}>() * {csElement.Name}.Length))"))), 
                         GenerateCopyMemory(csElement, ArrayCopyDirection.ManagedToNative)
                     )
                 ),
            _ => null
        };
    }

    public IEnumerable<StatementSyntax> GenerateManagedToNativeProlog(CsMarshalCallableBase csElement) =>
        Enumerable.Empty<StatementSyntax>();

    public ArgumentSyntax GenerateNativeArgument(CsMarshalCallableBase csElement) =>
        Argument(GetMarshalStorageLocation(csElement));

    public StatementSyntax GenerateNativeCleanup(CsMarshalBase csElement, bool singleStackFrame) =>
        csElement switch
        {
            CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                 Block(ParseStatement($"System.Runtime.InteropServices.NativeMemory.Free(@ref.{csElement.Name});")),
            _ => null
        };

    public StatementSyntax GenerateNativeToManaged(CsMarshalBase csElement, bool singleStackFrame)
    {
        const ArrayCopyDirection direction = ArrayCopyDirection.NativeToManaged;

        return csElement switch
        {
            CsParameter { PassedByManagedReference: true } parameter => GenerateCopyBlock(parameter, direction),
            CsField { ArraySpecification.Type: ArraySpecificationType.Constant } => GenerateCopyMemory(csElement, direction),
            CsField { ArraySpecification.Type: ArraySpecificationType.Dynamic } =>
                IfStatement(BinaryExpression(SyntaxKind.GreaterThanExpression, MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(MarshalParameterRefName), IdentifierName(csElement.ArraySpecification?.SizeIdentifier)), ZeroLiteral),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(csElement.Name),
                                ParseExpression($"new {csElement.PublicType.QualifiedName}[@ref.{csElement.ArraySpecification?.SizeIdentifier}]"))),
                        GenerateCopyMemory(csElement, ArrayCopyDirection.NativeToManaged)
                    )
                ),
            _ => null
        };
    }

    public IEnumerable<StatementSyntax> GenerateNativeToManagedExtendedProlog(CsMarshalCallableBase csElement)
    {
        yield return GenerateArrayNativeToManagedExtendedProlog(csElement);
    }

    public FixedStatementSyntax GeneratePin(CsParameter csElement) => FixedStatement(
        VariableDeclaration(
            GetMarshalTypeSyntax(csElement),
            SingletonSeparatedList(
                VariableDeclarator(GetMarshalStorageLocationIdentifier(csElement)).WithInitializer(
                    EqualsValueClause(IdentifierName(csElement.Name))
                )
            )
        ),
        EmptyStatement()
    );

    public bool GeneratesMarshalVariable(CsMarshalCallableBase csElement) => true;

    public TypeSyntax GetMarshalTypeSyntax(CsMarshalBase csElement) =>
        PointerType(ParseTypeName(csElement.PublicType.QualifiedName));

    public ValueTypeArrayMarshaller(Ioc ioc) : base(ioc)
    {
    }
}