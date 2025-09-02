using System.Text.Json;
using Newtonsoft.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;

/// <summary>
/// There is unfortunate use cases for both Newtonsoft.Json and System.Text.Json.
///
/// The overwhelming majority of the solution uses System.Text.Json for Serialization as it is
/// much faster, and provides more granularity when it comes to serialization/deserialization.
///
/// The only current use case of Newtonsoft is Serializing arbitrary output objects in steps
/// due to its' ability to handle IDisposable objects and Circular Dependencies much better.
/// </summary>
public interface IChainSharpEffectConfiguration
{
    /// <summary>
    /// System.Text.Json Serialization Options
    /// </summary>
    public JsonSerializerOptions SystemJsonJsonSerializerOptions { get; }

    /// <summary>
    /// Newtonsoft.Json Serialization Options
    /// </summary>
    public JsonSerializerSettings NewtonsoftJsonSerializerSettings { get; }

    public bool SerializeStepData { get; }
}
