namespace ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

public interface IBrewingJug
{
    int Gallons { get; set; }
    int Yeast { get; set; }
    bool HasCinnamonSticks { get; set; }
    bool IsFermented { get; set; }
    bool IsBrewed { get; set; }
    Ingredients Ingredients { get; set; }
}
