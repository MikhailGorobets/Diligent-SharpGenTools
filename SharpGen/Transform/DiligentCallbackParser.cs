using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SharpGen.Logging;
using SharpGen.Model;

namespace SharpGen.Transform;

#nullable enable

internal static class DiligentCallbackParser
{
    private sealed class ParseResult
    {
        public enum FunctionType
        {
            Type,
            Ref,
            Unexpected
        }

        public enum ParseIssue
        {
            FunctionInvocationExpected,
            FunctionNameIdentifierExpected,
            ArgumentCountMismatch,
            UnknownFunctionIdentifier
        }

        public FunctionType Type;

        public string? Argument;

        public ParseIssue? Issue;
        public TextSpan IssueSource { get; }

        public string IssueDescription => Issue switch
        {
            ParseIssue.FunctionInvocationExpected => "expected function invocation",
            ParseIssue.FunctionNameIdentifierExpected => "expected function identifier",
            ParseIssue.ArgumentCountMismatch => "mismatched argument count",
            ParseIssue.UnknownFunctionIdentifier => "unknown function name",
            _ => throw new Exception($"Unknown {nameof(ParseIssue)}")
        };

        public ParseResult(FunctionType type, string arg)
        {
            Type = type;
            Argument = arg;
        }

        public ParseResult(ParseIssue issue, TextSpan issueSource)
        {
            Type = FunctionType.Unexpected;
            Issue = issue;
            IssueSource = issueSource;
        }
    }

    private static ParseResult ParseFunctionExpression(string item)
    {
        var expression = SyntaxFactory.ParseExpression(item);

        if (expression is not InvocationExpressionSyntax invocationExpression)
            return new ParseResult(ParseResult.ParseIssue.FunctionInvocationExpected, expression.Span);

        var functionNameExpression = invocationExpression.Expression;

        if (functionNameExpression is not IdentifierNameSyntax functionIdentifier)
            return new ParseResult(ParseResult.ParseIssue.FunctionNameIdentifierExpected, functionNameExpression.Span);

        var argumentList = invocationExpression.ArgumentList.Arguments;

        ParseResult ParseArgument(ParseResult.FunctionType type)
        {
            return argumentList.Count switch
            {
                1 => new ParseResult(type, argumentList[0].Expression.ToString()),
                _ => new ParseResult(ParseResult.ParseIssue.ArgumentCountMismatch, argumentList.Span)
            };
        }

        var identifierText = functionIdentifier.Identifier.ValueText;
        return identifierText switch
        {
            "Type" => ParseArgument(ParseResult.FunctionType.Type),
            "type" => ParseArgument(ParseResult.FunctionType.Type),
            "pfn" => ParseArgument(ParseResult.FunctionType.Ref),
            "PFN" => ParseArgument(ParseResult.FunctionType.Ref),
            _ => new ParseResult(ParseResult.ParseIssue.UnknownFunctionIdentifier, functionIdentifier.Span)
        };
    }

    public static MarshallableDiligentCallback? ParseDiligentCallback(string statement, Logger logger)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return null;

        var strings = statement.Split(';');
        if (strings.Length != 2)
        {
            logger.Error(LoggingCodes.InvalidDiligentCallback, $"Diligent-Callback [{statement}] parse failed: unexpected statements count");
            return null;
        }

        var expressions = strings.Select(ParseFunctionExpression);
        if (expressions.All(e => e.Issue != null))
        {
            foreach (var (e, s) in strings.Zip(expressions, (s, e) => (e, s)))
                if (e.Issue != null)
                    logger.Error(LoggingCodes.InvalidDiligentCallback, 
                        $"Diligent-Callback [{statement}] parse failed: {e.IssueDescription} '{s.Substring(e.IssueSource.Start, e.IssueSource.Length)}'");

            return null;
        }

        bool ContainsDuplicates(IEnumerable<ParseResult> enumerable)
        {
            HashSet<ParseResult.FunctionType> set = new();
            return enumerable.Any(element => !set.Add(element.Type));
        }

        if (ContainsDuplicates(expressions))
        {
            logger.Error(LoggingCodes.InvalidDiligentCallback, $"Diligent-Callback [{statement}] parse failed: invocations identifier names must be different");
            return null;
        }

        var identifierType = expressions.First(e => e.Type == ParseResult.FunctionType.Type).Argument;
        var identifierRef = expressions.First(e => e.Type == ParseResult.FunctionType.Ref).Argument;

        return new MarshallableDiligentCallback
        {
            IdentifierType = identifierType,
            IdentifierReferenceName = identifierRef
        };
    }
}
