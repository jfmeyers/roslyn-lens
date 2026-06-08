using Microsoft.CodeAnalysis;

namespace RoslynLens;

internal static class TypeStructureHelper
{
    /// <summary>
    /// Yields all named types structurally referenced by a type:
    /// base type, interfaces, field/property types, method return types, and parameter types.
    /// </summary>
    internal static IEnumerable<INamedTypeSymbol> CollectStructuralTypeRefs(
        INamedTypeSymbol type, CancellationToken ct = default)
    {
        if (type.BaseType is not null)
            yield return type.BaseType;

        foreach (var iface in type.Interfaces)
            yield return iface;

        foreach (var member in type.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            switch (member)
            {
                case IFieldSymbol f:
                    if (f.Type is INamedTypeSymbol fn) yield return fn;
                    break;
                case IPropertySymbol p:
                    if (p.Type is INamedTypeSymbol pn) yield return pn;
                    break;
                case IMethodSymbol m:
                    if (m.ReturnType is INamedTypeSymbol rn) yield return rn;
                    foreach (var param in m.Parameters)
                        if (param.Type is INamedTypeSymbol paramN) yield return paramN;
                    break;
            }
        }
    }
}
