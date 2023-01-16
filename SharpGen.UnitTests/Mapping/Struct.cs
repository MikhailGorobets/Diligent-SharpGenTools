using System.Linq;
using SharpGen.Config;
using SharpGen.CppModel;
using SharpGen.Logging;
using SharpGen.Model;
using SharpGen.Transform;
using Xunit;
using Xunit.Abstractions;

namespace SharpGen.UnitTests.Mapping;

public class Struct : MappingTestBase
{
    public Struct(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void Simple()
    {
        var config = new ConfigFile
        {
            Id = nameof(Simple),
            Namespace = nameof(Simple),
            Includes =
            {
                new IncludeRule
                {
                    File = "simple.h",
                    Attach = true,
                    Namespace = nameof(Simple)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            }
        };

        var cppStruct = new CppStruct("struct");

        cppStruct.Add(new CppField("field")
        {
            TypeName = "int"
        });

        var cppInclude = new CppInclude("simple");

        var cppModule = new CppModule("SharpGenTestModule");

        cppModule.Add(cppInclude);
        cppInclude.Add(cppStruct);

        var (solution, _) = MapModel(cppModule, config);

        Assert.Single(solution.EnumerateDescendants<CsStruct>());

        var csStruct = solution.EnumerateDescendants<CsStruct>().First();

        Assert.Single(csStruct.Fields.Where(fld => fld.Name == "Field"));

        var field = csStruct.Fields.First(fld => fld.Name == "Field");

        Assert.IsType<CsFundamentalType>(field.PublicType);
        Assert.Equal(TypeRegistry.Int32, field.PublicType);
    }


    [Fact]
    public void SequentialOffsets()
    {
        var config = new ConfigFile
        {
            Id = nameof(SequentialOffsets),
            Namespace = nameof(SequentialOffsets),
            Includes =
            {
                new IncludeRule
                {
                    File = "simple.h",
                    Attach = true,
                    Namespace = nameof(SequentialOffsets)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            }
        };

        var cppStruct = new CppStruct("struct");

        cppStruct.Add(new CppField("field")
        {
            TypeName = "int"
        });

        cppStruct.Add(new CppField("field2")
        {
            TypeName = "int",
            Offset = 1
        });

        var cppInclude = new CppInclude("simple");

        var cppModule = new CppModule("SharpGenTestModule");

        cppModule.Add(cppInclude);
        cppInclude.Add(cppStruct);

        var (solution, _) = MapModel(cppModule, config);

        var csStruct = solution.EnumerateDescendants<CsStruct>().First();

        var field = csStruct.Fields.First(fld => fld.Name == "Field");
        var field2 = csStruct.Fields.First(fld => fld.Name == "Field2");

        Assert.Equal(0u, field.Offset);

        Assert.Equal(4u, field2.Offset);
    }

    [Fact]
    public void InheritingStructs()
    {
        var config = new ConfigFile
        {
            Id = nameof(InheritingStructs),
            Namespace = nameof(InheritingStructs),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(InheritingStructs)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            }
        };

        var struct0 = new CppStruct("struct0");

        struct0.Add(new CppField("field0")
        {
            TypeName = "char",
            Pointer = "*"
        });

        var struct1 = new CppStruct("struct1")
        {
            Base = "struct0"
        };

        struct1.Add(new CppField("field1")
        {
            TypeName = "int"
        });

        
        var struct2 = new CppStruct("struct2")
        {
            Base = "struct1"
        };

        struct2.Add(new CppField("field2")
        {
            TypeName = "int"
        });

        var cppInclude = new CppInclude("test");
        cppInclude.Add(struct0);
        cppInclude.Add(struct1);
        cppInclude.Add(struct2);

        var cppModule = new CppModule("SharpGenTestModule");
        cppModule.Add(cppInclude);

        var (solution, _) = MapModel(cppModule, config);

        var csStruct = solution.EnumerateDescendants<CsStruct>().First(@struct => @struct.Name == "Struct2");

        var field0 = csStruct.Fields.First(fld => fld.Name == "Field0");
        var field1 = csStruct.Fields.First(fld => fld.Name == "Field1");
        var field2 = csStruct.Fields.First(fld => fld.Name == "Field2");

        Assert.NotNull(field0);
        Assert.NotNull(field1);
        Assert.NotNull(field2);

        Assert.Equal(0u, field0.Offset);
        Assert.Equal(8u, field1.Offset);
        Assert.Equal(12u, field2.Offset);
    }

    [Fact]
    public void IntFieldMappedToBoolIsMarkedAsBoolToInt()
    {
        var structName = "BoolToInt";
        var config = new ConfigFile
        {
            Id = nameof(IntFieldMappedToBoolIsMarkedAsBoolToInt),
            Namespace = nameof(IntFieldMappedToBoolIsMarkedAsBoolToInt),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(IntFieldMappedToBoolIsMarkedAsBoolToInt)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32"),
                new BindRule("bool", "System.Boolean")
            },
            Mappings =
            {
                new MappingRule
                {
                    Field = $"{structName}::field",
                    MappingType = "bool",
                },
            }
        };

        var structure = new CppStruct(structName);

        structure.Add(new CppField("field")
        {
            TypeName = "int"
        });

        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);

        var (solution, _) = MapModel(module, config);

        Assert.Single(solution.EnumerateDescendants<CsStruct>().Where(csStruct => csStruct.Name == structName));

        var generatedStruct = solution.EnumerateDescendants<CsStruct>().First(csStruct => csStruct.Name == structName);

        Assert.Single(generatedStruct.Fields);

        var field = generatedStruct.Fields.First();

        Assert.True(field.IsBoolToInt);
    }

    [Fact]
    public void MultipleBitfieldOffsetsGeneratedCorrectly()
    {
        var config = new ConfigFile
        {
            Id = nameof(MultipleBitfieldOffsetsGeneratedCorrectly),
            Namespace = nameof(MultipleBitfieldOffsetsGeneratedCorrectly),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(MultipleBitfieldOffsetsGeneratedCorrectly)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            }
        };


        var structure = new CppStruct("Test");

        structure.Add(new CppField("bitfield1")
        {
            TypeName = "int",
            IsBitField = true,
            BitOffset = 16,
            Offset = 0,
        });
        structure.Add(new CppField("field")
        {
            TypeName = "int",
            Offset = 1,
        });
        structure.Add(new CppField("bitfield2")
        {
            TypeName = "int",
            IsBitField = true,
            BitOffset = 16,
            Offset = 2
        });

        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);

        var (solution, _) = MapModel(module, config);

        var csStruct = solution.EnumerateDescendants<CsStruct>().First();

        var bitField1 = csStruct.Fields.First(field => field.Name == "Bitfield1");

        Assert.Equal(0, bitField1.BitOffset);
        Assert.Equal(0u, bitField1.Offset);
        Assert.Equal((1 << 16) - 1, bitField1.BitMask);
        Assert.True(bitField1.IsBitField);

        var csField = csStruct.Fields.First(field => field.Name == "Field");

        Assert.Equal(4u, csField.Offset);
        Assert.False(csField.IsBitField);

        var bitField2 = csStruct.Fields.First(field => field.Name == "Bitfield2");

        Assert.Equal(0, bitField2.BitOffset);
        Assert.Equal(8u, bitField2.Offset);
        Assert.Equal((1 << 16) - 1, bitField2.BitMask);
        Assert.True(bitField2.IsBitField);
    }

    [Fact]
    public void UnionsWithPointersGeneratesStructure()
    {
        var config = new ConfigFile
        {
            Id = nameof(UnionsWithPointersGeneratesStructure),
            Namespace = nameof(UnionsWithPointersGeneratesStructure),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(UnionsWithPointersGeneratesStructure)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            }
        };


        var structure = new CppStruct("Test")
        {
            IsUnion = true
        };

        structure.Add(new CppField("pointer")
        {
            TypeName = "int",
            Pointer = "*"
        });

        structure.Add(new CppField("scalar")
        {
            TypeName = "int"
        });

        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);

        var (solution, _) = MapModel(module, config);

        var csStruct = solution.EnumerateDescendants<CsStruct>().First();

        foreach (var field in csStruct.Fields)
        {
            Assert.Equal(0u, field.Offset);
        }

        Assert.False(Logger.HasErrors);
    }

    [Fact(Skip = "TODO")]
    public void NonPortableStructAlignmentRaisesError()
    {
        var config = new ConfigFile
        {
            Id = nameof(NonPortableStructAlignmentRaisesError),
            Namespace = nameof(NonPortableStructAlignmentRaisesError),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(NonPortableStructAlignmentRaisesError)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            }
        };


        var structure = new CppStruct("Test");

        structure.Add(new CppField("bitfield1")
        {
            TypeName = "int",
            IsBitField = true,
            BitOffset = 16,
            Offset = 0,
        });
        structure.Add(new CppField("bitfield2")
        {
            TypeName = "int",
            IsBitField = true,
            BitOffset = 16,
            Offset = 0
        });

        structure.Add(new CppField("pointer")
        {
            TypeName = "int",
            Pointer = "*",
            Offset = 1
        });

        structure.Add(new CppField("field")
        {
            TypeName = "int",
            Offset = 2,
        });

        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);

        using (LoggerMessageCountEnvironment(1, LogLevel.Error))
        using (LoggerCodeRequiredEnvironment(LoggingCodes.NonPortableAlignment))
        {
            MapModel(module, config);
        }
    }

    [Fact]
    public void NonPortableLayoutDoesNotErrorWhenMarkedForCustomMarshalling()
    {
        var config = new ConfigFile
        {
            Id = nameof(NonPortableLayoutDoesNotErrorWhenMarkedForCustomMarshalling),
            Namespace = nameof(NonPortableLayoutDoesNotErrorWhenMarkedForCustomMarshalling),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(NonPortableLayoutDoesNotErrorWhenMarkedForCustomMarshalling)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            },
            Mappings =
            {
                new MappingRule
                {
                    Struct = "Test",
                    StructCustomMarshal = true
                }
            }
        };


        var structure = new CppStruct("Test");

        structure.Add(new CppField("bitfield1")
        {
            TypeName = "int",
            IsBitField = true,
            BitOffset = 16,
            Offset = 0,
        });
        structure.Add(new CppField("bitfield2")
        {
            TypeName = "int",
            IsBitField = true,
            BitOffset = 16,
            Offset = 0
        });

        structure.Add(new CppField("pointer")
        {
            TypeName = "int",
            Pointer = "*",
            Offset = 1
        });

        structure.Add(new CppField("field")
        {
            TypeName = "int",
            Offset = 2,
        });

        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);

        var (solution, _) = MapModel(module, config);

        Assert.False(Logger.HasErrors);
    }


    [Fact]
    public void LengthRelationInStruct()
    {
        var structName = "Test";
        var config = new ConfigFile
        {
            Id = nameof(InvalidLengthRelationInStruct),
            Namespace = nameof(InvalidLengthRelationInStruct),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(InvalidLengthRelationInStruct)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            },
            Mappings =
            {
                new MappingRule
                {
                    Field = $"{structName}::count",
                    Relation = "length(elements)"
                },
            }
        };

        var structure = new CppStruct(structName);

        structure.Add(new CppField("elements")
        {
            TypeName = "int",
            Pointer = "*"
        });
        structure.Add(new CppField("count")
        {
            TypeName = "int",
            Offset = 1,
        });


        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);


        var (solution, _) = MapModel(module, config);

        var csStruct = solution.EnumerateDescendants<CsStruct>().First(@struct => @struct.Name == structName);
        Assert.NotNull(csStruct);

        var field0 = csStruct.Fields.First(fld => fld.Name == "Elements");
        var field1 = csStruct.Fields.First(fld => fld.Name == "Count");

        Assert.NotNull(field0);
        Assert.NotNull(field1);
        Assert.Equal(ArraySpecificationType.Dynamic, field0.ArraySpecification?.Type);
        Assert.Equal("Count", field0.ArraySpecification?.SizeIdentifier);
    }


    [Fact]
    public void InvalidLengthRelationInStruct()
    {
        var structName = "Test";
        var config = new ConfigFile
        {
            Id = nameof(InvalidLengthRelationInStruct),
            Namespace = nameof(InvalidLengthRelationInStruct),
            Includes =
            {
                new IncludeRule
                {
                    File = "test.h",
                    Attach = true,
                    Namespace = nameof(InvalidLengthRelationInStruct)
                }
            },
            Bindings =
            {
                new BindRule("int", "System.Int32")
            },
            Mappings =
            {
                new MappingRule
                {
                    Field = $"{structName}::count",
                    Relation = "length(check)"
                },
            }
        };

        var structure = new CppStruct(structName);

        structure.Add(new CppField("elements")
        {
            TypeName = "int",
            Pointer = "*"
        });
        structure.Add(new CppField("count")
        {
            TypeName = "int",
            Offset = 1,
        });


        var include = new CppInclude("test");

        include.Add(structure);

        var module = new CppModule("SharpGenTestModule");
        module.Add(include);


        using (LoggerMessageCountEnvironment(1, LogLevel.Error))
        using (LoggerCodeRequiredEnvironment(LoggingCodes.InvalidLengthRelation))
        {
            MapModel(module, config);
        }
    }
}
