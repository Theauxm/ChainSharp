namespace ChainSharp.Blazor.Components.Pages;

public partial class Dashboard()
{
    public int Counter { get; set; } = 50;

    public void Increment()
    {
        Counter++;
    }
}
