namespace SampleApp.Services;

/// <summary>
/// Class that inherits from OrderController (which depends on OrderService).
/// EXPECTED: Transitive fan-in (inherits OrderController, which depends on OrderService).
/// </summary>
public class AdminController : OrderController
{
    public void AdminAction()
    {
        HandleRequest();
    }
}
