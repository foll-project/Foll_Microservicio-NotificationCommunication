namespace foll_notifications.ExternalServices.Infrastructure.Configuration;

public class FirebaseOptions
{
    public bool Enabled { get; set; }
    public string? ProjectId { get; set; }
    public string? CredentialsPath { get; set; }
}
