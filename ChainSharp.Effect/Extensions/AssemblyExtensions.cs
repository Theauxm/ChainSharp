using System.Reflection;

namespace ChainSharp.Effect.Extensions;

public static class AssemblyExtensions
{
    public static string GetAssemblyProject(this Assembly assembly)
    {
        if (assembly.FullName is null)
            throw new NullReferenceException("Could not get FullName of Entry Assembly");

        var assemblyNameArray = assembly.FullName.Split(",");

        var assemblyName = assemblyNameArray.FirstOrDefault();

        if (assemblyName is null)
            throw new Exception($"Could not get name of Assembly from ({assembly.FullName})");

        return assemblyName;
    }
}
