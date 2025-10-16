
public class ImageFillsRequest : BaseRequest
{
    public string fileId { get; }

    public string[] imageRefs { get; set; }

    public ImageFillsRequest(string fileId)
    {
        this.fileId = fileId;
    }
}