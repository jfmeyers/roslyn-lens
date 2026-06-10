using Microsoft.CodeAnalysis;

namespace RoslynLens;

/// <summary>
/// Recognizes types the .NET runtime activates through standard framework contracts
/// (hosted services, MVC controllers, Razor pages, Blazor components) so reference
/// counting doesn't mistake them for dead code: the framework discovers and instantiates
/// them by scanning for these base types/interfaces, leaving zero source references.
/// <para>
/// Scope is deliberately limited to BCL / ASP.NET Core contracts. Framework- or
/// domain-specific discovery (e.g. third-party assembly scanning by name suffix or
/// marker attribute) has no generic syntactic signal and belongs in caller-supplied
/// configuration, not baked into a general-purpose tool.
/// </para>
/// </summary>
internal static class ActivationRoots
{
    private static readonly HashSet<string> Contracts =
    [
        "Microsoft.Extensions.Hosting.IHostedService",
        "Microsoft.Extensions.Hosting.BackgroundService",
        "Microsoft.AspNetCore.Mvc.ControllerBase",
        "Microsoft.AspNetCore.Mvc.RazorPages.PageModel",
        "Microsoft.AspNetCore.Components.ComponentBase"
    ];

    private static readonly SymbolDisplayFormat FullName = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <summary>
    /// True when the symbol is a type that derives from or implements a recognized
    /// runtime-activation contract, making it reachable even with zero source references.
    /// Roots the type only — its members keep normal dead-code analysis, so a genuinely
    /// unused private/internal helper on an activated type is still reported.
    /// </summary>
    internal static bool IsFrameworkActivated(ISymbol symbol)
    {
        if (symbol is not INamedTypeSymbol type)
            return false;

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (Contracts.Contains(current.ToDisplayString(FullName)))
                return true;
        }

        return type.AllInterfaces.Any(i => Contracts.Contains(i.ToDisplayString(FullName)));
    }
}
