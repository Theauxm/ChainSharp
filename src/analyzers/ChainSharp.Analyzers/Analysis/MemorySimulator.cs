using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ChainSharp.Analyzers.Analysis;

/// <summary>
/// Simulates the runtime Memory dictionary as a set of type symbols.
/// Tracks which types are available at each point in the workflow chain.
/// </summary>
internal sealed class MemorySimulator
{
    private const string UnitMetadataName = "LanguageExt.Unit";

    private readonly HashSet<ITypeSymbol> _types;

    public MemorySimulator()
    {
        _types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// Seeds Memory with the workflow input type and Unit.
    /// </summary>
    public void Initialize(ITypeSymbol inputType, INamedTypeSymbol? unitType)
    {
        _types.Add(inputType);

        if (unitType != null)
            _types.Add(unitType);
    }

    /// <summary>
    /// Adds a type to Memory (e.g., after a Chain step produces TOut).
    /// Skips Unit since it's always present.
    /// </summary>
    public void AddType(ITypeSymbol type)
    {
        if (!IsUnit(type))
            _types.Add(type);
    }

    /// <summary>
    /// Checks whether a type is currently available in Memory.
    /// </summary>
    public bool Contains(ITypeSymbol type)
    {
        return _types.Contains(type);
    }

    /// <summary>
    /// Returns a comma-separated string of all available type names, for diagnostic messages.
    /// </summary>
    public string GetAvailableTypesString()
    {
        return string.Join(
            ", ",
            _types.Select(t => t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
        );
    }

    private static bool IsUnit(ITypeSymbol type)
    {
        return type.ToDisplayString() == UnitMetadataName;
    }
}
