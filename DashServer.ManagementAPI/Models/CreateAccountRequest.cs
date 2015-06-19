namespace DashServer.ManagementAPI.Models
{
    public class CreateAccountRequest : BaseRequest
    {
        public string Location { get; set; }
        public string AccountType { get; set; }

    }
}
