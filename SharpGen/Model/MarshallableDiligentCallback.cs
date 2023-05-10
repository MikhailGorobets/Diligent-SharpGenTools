using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharpGen.Model;

public class MarshallableDiligentCallback
{
    public string IdentifierType { get; set; }

    public string IdentifierReferenceName { get; set; }

    public override string ToString()
    {
        return $"Type: '{IdentifierType}' Ref: '{IdentifierReferenceName}'";
    }
}
