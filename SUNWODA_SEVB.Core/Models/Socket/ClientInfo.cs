
namespace SUNWODA_SEVB.Core.Models.Socket
{
    public class ClientInfo
    {
        public string Id { get; set; } = string.Empty;
        public string EndPoint { get; set; } = string.Empty;
        public DateTime ConnectedTime { get; set; }
    }
}
