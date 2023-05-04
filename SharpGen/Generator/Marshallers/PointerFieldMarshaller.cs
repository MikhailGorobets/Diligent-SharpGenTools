using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace SharpGen.Generator.Marshallers;

internal sealed class PointerFieldMarshaller : MarshallerBase, IMarshaller
{
    public PointerFieldMarshaller(Ioc ioc) : base(ioc)
    {
    }

    public bool CanMarshal(CsMarshalBase csElement) => csElement is CsField { IsOptionalPointer: true };

    public StatementSyntax GenerateManagedToNative(CsMarshalBase csElement, bool singleStackFrame)
    {
        var typeName = csElement.HasNativeValueType ? csElement.MarshalType.Name + ".__Native" : csElement.PublicType.Name;

        var statementAlloc =
            ParseStatement(
                $"@ref.{csElement.Name} = ({typeName}*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint) System.Runtime.CompilerServices.Unsafe.SizeOf<{typeName}>());");

        var statementTo = csElement.HasNativeValueType ?
            ExpressionStatement(InvocationExpression(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName($"{csElement.Name}.Value"),
                    IdentifierName("__MarshalTo")),
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            RefExpression(PrefixUnaryExpression(
                                SyntaxKind.PointerIndirectionExpression,
                                IdentifierName($"@ref.{csElement.Name}")))))))) :
            ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    PrefixUnaryExpression(SyntaxKind.PointerIndirectionExpression,
                    IdentifierName($"@ref.{csElement.Name}")),
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(csElement.Name),
                        IdentifierName("Value"))));

        return IfStatement(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(csElement.Name), IdentifierName("HasValue")),
            Block(
                statementAlloc,
                statementTo
            )
        );
    }

    public StatementSyntax GenerateNativeToManaged(CsMarshalBase csElement, bool singleStackFrame)
    {
        var statementTo = csElement.HasNativeValueType
            ? ExpressionStatement(InvocationExpression(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName($"{csElement.Name}.Value"),
                    IdentifierName("__MarshalFrom")),
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            RefExpression(PrefixUnaryExpression(
                                SyntaxKind.PointerIndirectionExpression,
                                IdentifierName($"@ref.{csElement.Name}"))))))))
            : ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(csElement.Name),
                    PrefixUnaryExpression(
                        SyntaxKind.PointerIndirectionExpression,
                        IdentifierName($"@ref.{csElement.Name}"))
                )
            );

        return IfStatement(
            BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                IdentifierName($"@ref.{csElement.Name}"),
                LiteralExpression(SyntaxKind.NullLiteralExpression)
            ), statementTo
        );
    }

    public StatementSyntax GenerateNativeCleanup(CsMarshalBase csElement, bool singleStackFrame) => GenerateNativeMemoryFree(csElement);


    public IEnumerable<StatementSyntax> GenerateManagedToNativeProlog(CsMarshalCallableBase csElement)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<StatementSyntax> GenerateNativeToManagedExtendedProlog(CsMarshalCallableBase csElement)
    {
        throw new NotImplementedException();
    }

    public ArgumentSyntax GenerateNativeArgument(CsMarshalCallableBase csElement)
    {
        throw new NotImplementedException();
    }

    public ArgumentSyntax GenerateManagedArgument(CsParameter csElement)
    {
        throw new NotImplementedException();
    }

    public ParameterSyntax GenerateManagedParameter(CsParameter csElement)
    {
        throw new NotImplementedException();
    }

    public FixedStatementSyntax GeneratePin(CsParameter csElement)
    {
        throw new NotImplementedException();
    }

    public bool GeneratesMarshalVariable(CsMarshalCallableBase csElement)
    {
        throw new NotImplementedException();
    }

    public TypeSyntax GetMarshalTypeSyntax(CsMarshalBase csElement)
    {
        throw new NotImplementedException();
    }
}
