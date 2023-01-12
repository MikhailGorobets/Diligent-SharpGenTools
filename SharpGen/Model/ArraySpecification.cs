namespace SharpGen.Model;

#nullable enable

public enum ArraySpecificationType
{
    Undefined,
    Constant,
    Dynamic
}

public struct ArraySpecification
{
    public ArraySpecification()
    {
        Type = ArraySpecificationType.Undefined;
        Dimension = default;
        SizeIdentifier = default;
        TypeSizeIdentifier = default;
    }

    public ArraySpecification(uint dimension)
    {
        Type = ArraySpecificationType.Constant;
        Dimension = dimension;
        SizeIdentifier = default;
        TypeSizeIdentifier = default;
    }

    public ArraySpecification(string identifier, string typeName)
    {
        Type = ArraySpecificationType.Dynamic;
        Dimension = default;
        SizeIdentifier = identifier;
        TypeSizeIdentifier = typeName;
    }

    public uint? Dimension { get; set; }

    public string? SizeIdentifier { get; set; }

    public string? TypeSizeIdentifier { get; set; }

    public ArraySpecificationType Type;
}
