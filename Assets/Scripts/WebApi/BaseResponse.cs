using System.Runtime.Serialization;

[DataContract]
public class BaseResponse
{
    [DataMember(IsRequired = true)]
    public int status { get; set; }

    [DataMember(IsRequired = true)]
    public bool error { get; set; }

    [DataMember]
    public string message { get; set; }
}