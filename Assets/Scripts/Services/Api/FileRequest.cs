
public class FileRequest : BaseRequest
{
    public string fileId { get; }

    public string nodeId { get; set; } = null;


    public FileRequest(string fileId)
    {
        this.fileId = fileId;
    }
}