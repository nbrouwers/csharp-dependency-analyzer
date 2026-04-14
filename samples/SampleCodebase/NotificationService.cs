namespace SampleApp.Services;

/// <summary>
/// Service class with no dependency on OrderService.
/// EXPECTED: NOT a fan-in.
/// </summary>
public class NotificationService
{
    public void SendEmail(string to, string subject, string body)
    {
        // Email logic
    }

    public void SendSms(string phoneNumber, string message)
    {
        // SMS logic
    }
}
