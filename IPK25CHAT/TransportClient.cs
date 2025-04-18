public interface TransportClient
{
    Task Connect();
    Task Setup();
    Task Loop();
    Task DisconnectAsync();
    Task ServerData(CancellationToken token);
    Task UserCommands(CancellationToken token);
    Task ServerMessage(string message);
    Task ShowMessage(string message);
    Task ProcessCommand(string message);
    Task Authenticate(string[] input);
    Task Join(string[] input);
    void CommandsHelp();
    string ConstructByeMessage(string displayName);
    string ConstructErrorMessage(string displayName, string content);
    void Terminate();
    void CancelHandler(object? sender, ConsoleCancelEventArgs e);

}