namespace DashServer.ManagementAPI.Models
{
    public class CreateAccountRequest
    {
        public string AccountName { get; set; }
        public string Location { get; set; }
        public string AccountType { get; set; }

    }
}
