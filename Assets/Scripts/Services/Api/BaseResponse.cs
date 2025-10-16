using System.Runtime.Serialization;

[DataContract]
public class BaseResponse
{
    [DataMember(IsRequired = false)]
    public int status { get; set; }

    [DataMember(IsRequired = false)]
    public bool error { get; set; }

    [DataMember]
    public string message { get; set; }
}
