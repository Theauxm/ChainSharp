using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Metadata;
using Microsoft.AspNetCore.Components;

namespace ChainSharp.Blazor.Components.Shared;

public partial class WorkflowTable
{
    [Inject]
    private IDataContextProviderFactory DataContextProviderFactory { get; set; }

    private IQueryable<Metadata> _metadatas;

    private IList<Metadata> _selectedMetadatas;

    private bool isLoading = true;
    private int totalCount;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var dataContext = DataContextProviderFactory.Create();

            _metadatas = dataContext.Metadatas;

            _selectedMetadatas = new List<Metadata> { _metadatas.FirstOrDefault() };
        }
        finally
        {
            // Hide the spinner once data is loaded
            isLoading = false;
        }
    }
}
