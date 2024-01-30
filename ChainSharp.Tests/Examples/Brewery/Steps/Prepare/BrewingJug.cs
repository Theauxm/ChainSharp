namespace ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

public class BrewingJug
{
    public required int Gallons { get; set; }
    
    public int Yeast { get; set; }
    
    public bool HasCinnamonSticks { get; set; }
    
    public bool IsFermented { get; set; }
    
    public bool IsBrewed { get; set; }
    
    public Ingredients Ingredients { get; set; }
}