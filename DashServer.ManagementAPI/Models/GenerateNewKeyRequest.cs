namespace DashServer.ManagementAPI.Models
{
    public class GenerateNewKeyRequest : BaseRequest
    {
        public string AccountKeyType { get; set; }
    }
}
