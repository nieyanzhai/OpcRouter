namespace OpcRouter.Models.Entities.Mes;

public class MesEndpoint
{
    public string Url { get; set; }
    public string SoapAction { get; set; }
    public int Timeout { get; set; }
}