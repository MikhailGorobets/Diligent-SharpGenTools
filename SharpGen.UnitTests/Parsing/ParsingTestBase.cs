using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SharpGen.Config;
using SharpGen.CppModel;
using SharpGen.Parser;
using SharpGen.Platform;
using Xunit.Abstractions;

namespace SharpGen.UnitTests.Parsing;

public abstract class ParsingTestBase : FileSystemTestBase
{
    private static readonly string CastXmlDirectoryPath;

    static ParsingTestBase()
    {
        var info = new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            FileName = "python",
            ArgumentList = {
                "-c",
                "import sys; import os; print(os.path.join(sys.exec_prefix, 'Scripts', 'castxml-patch.exe'), end ='')"
            }
        };

        using var process = Process.Start(info);
        using var reader = process.StandardOutput;
        CastXmlDirectoryPath = reader.ReadToEnd();
    }

    protected ParsingTestBase(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    protected IncludeRule CreateCppFile(string cppFileName, string cppFile, [CallerMemberName] string testName = "")
    {
        var includesDir = TestDirectory.CreateSubdirectory("includes");
        File.WriteAllText(Path.Combine(includesDir.FullName, cppFileName + ".h"), cppFile);
        return new IncludeRule
        {
            Attach = true,
            File = cppFileName + ".h",
            Namespace = testName,
        };
    }

    protected IncludeRule CreateCppFile(string cppFileName, string cppFile, List<string> attaches, [CallerMemberName] string testName = "")
    {
        var includesDir = TestDirectory.CreateSubdirectory("includes");
        File.WriteAllText(Path.Combine(includesDir.FullName, cppFileName + ".h"), cppFile);
        return new IncludeRule
        {
            AttachTypes = attaches,
            File = cppFileName + ".h",
            Namespace = testName,
        };
    }

    protected CastXmlRunner GetCastXml(ConfigFile config, string[] additionalArguments = null)
    {
        IncludeDirectoryResolver resolver = new(Ioc);
        resolver.Configure(config);

        return new CastXmlRunner(resolver, CastXmlDirectoryPath,
                                 additionalArguments ?? Array.Empty<string>(), Ioc)
        {
            OutputPath = TestDirectory.FullName
        };
    }

    protected CppModule ParseCpp(ConfigFile config)
    {
        config.Load(null, Array.Empty<string>(), Logger);

        config.GetFilesWithIncludesAndExtensionHeaders(out var configsWithIncludes,
                                                       out var configsWithExtensionHeaders);

        CppHeaderGenerator cppHeaderGenerator = new(TestDirectory.FullName, Ioc);

        var updated = cppHeaderGenerator
                     .GenerateCppHeaders(config, configsWithIncludes, configsWithExtensionHeaders)
                     .UpdatedConfigs;

        var castXml = GetCastXml(config);

        var macro = new MacroManager(castXml);
        var extensionGenerator = new CppExtensionHeaderGenerator();

        var skeleton = config.CreateSkeletonModule();

        macro.Parse(Path.Combine(TestDirectory.FullName, config.HeaderFileName), skeleton);

        extensionGenerator.GenerateExtensionHeaders(
            config, TestDirectory.FullName, skeleton, configsWithExtensionHeaders, updated
        );

        CppParser parser = new(config, Ioc)
        {
            OutputPath = TestDirectory.FullName
        };

        using var xmlReader = castXml.Process(parser.RootConfigHeaderFileName);

        return parser.Run(skeleton, xmlReader);
    }
}