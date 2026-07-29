namespace SUNWODA_SEVB.Core.Models.MES
{
    public enum UploadStatus
    {
        Unknown = 0,
        Pending = 1,
        Uploading = 2,
        Success = 3,
        Failed = 4,
        Error = 5,
        ServiceDisabled = 6
    }

    public class UploadStatusResponse
    {
        public UploadStatus Status { get; set; }
        public string? Message { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}