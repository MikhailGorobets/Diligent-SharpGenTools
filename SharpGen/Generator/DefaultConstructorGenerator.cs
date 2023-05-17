using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Logging;
using SharpGen.Model;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Generator;

internal sealed class DefaultConstructorGenerator : MemberMultiCodeGeneratorBase<CsStruct>
{
    public DefaultConstructorGenerator(Ioc ioc) : base(ioc) { }

    private static string PathFloatLiteral(string expression) => expression.Replace(".F", ".0f");

    private static string PathBoolLiteral(string expression) => expression.ToLower();

    private static StatementSyntax GenerateForLoop(CsField csElement)
    {
        var elementInitialization = ExpressionStatement(AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            ElementAccessExpression(IdentifierName(csElement.Name))
                .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(IdentifierName("i"))))),
            ImplicitObjectCreationExpression()
        ));

        return ForStatement(VariableDeclaration(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                SeparatedList(
                    new[]
                    {
                        VariableDeclarator(Identifier("i"), default,
                            EqualsValueClause(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))
                    }
                )),
            default,
            BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName("i"),
                LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal((int) csElement.ArraySpecification.Value.Dimension))),
            SingletonSeparatedList<ExpressionSyntax>(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression,
                IdentifierName("i"))),
            elementInitialization
        );
    }

    public override IEnumerable<MemberDeclarationSyntax> GenerateCode(CsStruct csElement)
    {
        var syntaxList = new StatementSyntaxList();
        if (!csElement.GenerateAsClass)
            syntaxList.Add(ExpressionStatement(ParseExpression("this = default")));
        foreach (var item in csElement.PublicFields)
        {
            var defaultValue = item.DefaultValue ?? "{}";

            if (item.PublicType is CsFundamentalType { IsFloatingPointType: true })
                defaultValue = PathFloatLiteral(defaultValue);

            if (item is CsMarshalBase { IsBoolToInt: true })
                defaultValue = PathBoolLiteral(defaultValue);

            if (defaultValue is "nullptr" or "0" or "0.0" or "0.0f" or "false")
                continue;

            if (item.ArraySpecification is { Type: ArraySpecificationType.Dynamic } || item.IsInterface || item.DiligentCallback != null)
                continue;

            if (item.PublicType is CsFundamentalType or CsEnum)
            {
                if (defaultValue == "{}")
                    continue;

                syntaxList.Add(item.ArraySpecification is { Type: ArraySpecificationType.Constant }
                    ? ExpressionStatement(ParseExpression($"{item.Name} = new {item.PublicType.Name}[{item.ArraySpecification.Value.Dimension}] {defaultValue}"))
                    : ExpressionStatement(ParseExpression($"{item.Name} = {defaultValue}")));
            }
            else
            {
                if (defaultValue == "{}")
                {
                    syntaxList.Add(item.ArraySpecification is { Type: ArraySpecificationType.Constant }
                        ? GenerateForLoop(item)
                        : ExpressionStatement(ParseExpression($"{item.Name} = new()")));
                }
                else
                {
                    var argumentValues = defaultValue.Trim('{', '}').Split(',').Select(e => e.Trim()).ToArray();
                    var argumentFields = ((CsStruct) item.PublicType).PublicFields.ToArray();

                    if (argumentFields.Length != argumentValues.Length)
                    {
                        Logger.Error(LoggingCodes.InvalidDefaultConstructor, 
                            $"Incorrect number of elements in the list for initializing the field [{item.Name}] in the structure [{csElement.Name}].");
                        yield break;
                    }

                    var declarations = new List<ExpressionSyntax>();
                    foreach (var (field, value) in argumentFields.Zip(argumentValues, (x, y) => (x, y)))
                    {
                        declarations.Add(AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(field.Name),
                            ParseExpression(value))
                        );
                    }

                    syntaxList.Add(ExpressionStatement(
                        AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(item.Name),
                            ImplicitObjectCreationExpression()
                                .WithInitializer(InitializerExpression(SyntaxKind.ObjectInitializerExpression, SeparatedList(declarations)))
                    )));
                }
            }
        }

        if (!csElement.GenerateAsClass && syntaxList.Count == 1)
            yield break;

        yield return ConstructorDeclaration(csElement.Name)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBody(syntaxList.ToBlock());
    }
}
