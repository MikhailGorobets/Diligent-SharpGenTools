using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.Logging;
using SharpGen.Model;
using SharpGen.Transform;
using Xunit;
using Xunit.Abstractions;

namespace SharpGen.UnitTests;

public class DiligentCallbackParserTests : TestBase
{
    public DiligentCallbackParserTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void ParseFailsOnMissingInput()
    {
        
        using (LoggerEmptyEnvironment())
        {
            Assert.Null(DiligentCallbackParser.ParseDiligentCallback(null, Logger));
        }

        using (LoggerEmptyEnvironment())
        {
            Assert.Null(DiligentCallbackParser.ParseDiligentCallback("", Logger));
        }

        using (LoggerEmptyEnvironment())
        {
            Assert.Null(DiligentCallbackParser.ParseDiligentCallback("        ", Logger));
        }
    }
}
