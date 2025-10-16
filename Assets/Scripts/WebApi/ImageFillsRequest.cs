
public class ImageFillsRequest : BaseRequest
{
    public string fileId { get; }

    /// <summary>
    /// The image references to be downloaded. Leave as empty to download all the images in the file.
    /// </summary>
    /// <seealso cref="ImagePaint.imageRef"/>
    public string[] imageRefs { get; set; }

    public ImageFillsRequest(string fileId)
    {
        this.fileId = fileId;
    }
}