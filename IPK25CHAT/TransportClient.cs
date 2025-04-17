public interface TransportClient
{
    Task ConnectAsync();
    Task Setup();
    Task Loop();
    Task DisconnectAsync();
    Task ReceiveAsync();
    Task ServerData(CancellationToken token);
    Task UserCommands(CancellationToken token);
    Task ServerMessage(string message);
    Task ShowMessage(string message);
    string ConstructErrorMessage(string displayName, string content);
    void Terminate();
    void CancelHandler(object? sender, ConsoleCancelEventArgs e);

}