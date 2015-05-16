using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Handlers
{
    public interface INamespaceBlob
    {
        string AccountName { get; set; }

        string Container { get; set; }

        string BlobName { get; set; }

        bool? IsMarkedForDeletion { get; set; }

        Task SaveAsync();

        Task<bool> ExistsAsync(bool forceRefresh);
    }
}