/* 
 * The interface contains methods for managing the client's lifecycle, handling server communication, and processing user commands.
 */
public interface TransportClient
{
    Task Connect();
    Task Setup();
    Task Loop();
    Task DisconnectAsync();
    Task ServerData(CancellationToken token);
    Task UserCommands(CancellationToken token);
    Task ProcessCommand(string message);
    Task Authenticate(string[] input);
    Task Join(string[] input);
    void Terminate();
}