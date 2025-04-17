using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class TCPClient : TransportClient
{
    private TcpClient? tcpClient;
    private ChatOptions? options;
    private StreamReader? reader;
    private StreamWriter? writer;
    private NetworkStream? stream;
    private ClientStates.States? state = ClientStates.States.START;
    private string username = string.Empty;
    private CancellationTokenSource cancel = new CancellationTokenSource();

    public TCPClient(ChatOptions parsed)
    {
        options = parsed;
    }

    public async Task ConnectAsync()
    {
        tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(options.ServerHost, options.ServerPort);
        stream = tcpClient.GetStream();
        writer = new StreamWriter(stream, Encoding.ASCII) 
        { 
            AutoFlush = true 
        };
        reader = new StreamReader(stream, Encoding.ASCII);
    }

    public async Task Loop()
    {
        var tasks = new[]
        {
            Task.Run(() => ServerData(cancel.Token)),
            Task.Run(() => UserCommands(cancel.Token))
        };

        await Task.WhenAny(tasks);
        cancel.Cancel();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Tasks cancelled");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Error: {exception.Message}");
        }
    }

    public void CancelHandler(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Console.Error.WriteLine("Interrupt received, graceful termination incoming");
        this.Terminate();
    }

    public async Task Setup()
    {
        Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelHandler);
        try{
            await ConnectAsync();
            await Loop();
            await DisconnectAsync();
        }
        catch(Exception exception){
            Console.Error.WriteLine($"Error: {exception.Message}");
            await DisconnectAsync();
        }
    }

    public async Task ServerData(CancellationToken token)
    {
        bool disconnect = false;
        try{
            while(!token.IsCancellationRequested)
            {
                string? inputMessage = await reader.ReadLineAsync();
                if(inputMessage==null)
                {
                    break;
                }
                await ServerMessage(inputMessage);
            }
            //await byemessage?
        }
        catch(OperationCanceledException)
        {
            disconnect = true;
        }
        catch(Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
        }
        finally
        {
            if(!disconnect || token.IsCancellationRequested);
            //await byemessage
        }
    }
    public async Task ServerMessage(string message)
    {
        switch(state)
        {
            case ClientStates.States.START:
                if(message.StartsWith("ERR"))
                {
                    string[] content = Messages.ErrorMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("BYE"))
                {
                    string[] content = Messages.ByeMessage(message);
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else
                {
                    Console.Error.WriteLine("Error: wrong type of message received");
                    return;
                }
            break;
            case ClientStates.States.AUTH:
                if(message.StartsWith("MSG"))
                {
                    string[] content = Messages.MsgMessage(message);
                    try{
                        await ShowMessage(ConstructErrorMessage(content[1], "Message was received before authentication"));
                    }
                    catch(ArgumentException exception)
                    {
                        Console.WriteLine($"ERROR: {exception}");
                    }
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("ERR"))
                {
                    string[] content = Messages.ReplyMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("BYE"))
                {
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("REPLY"))
                {
                    string[] content = Messages.ReplyMessage(message);
                    if(content[1] == "OK")
                    {
                        Console.WriteLine($"Action Success: {content[2]}");
                        state = ClientStates.States.OPEN;
                    }
                    else{
                        Console.WriteLine($"Action Failure: {content[2]}");
                        return;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: wrong type of message received");
                    return;
                }
            break;
            case ClientStates.States.OPEN:
                if(message.StartsWith("MSG"))
                {
                    string[] content = Messages.MsgMessage(message);
                    try{
                        await ShowMessage(ConstructErrorMessage(content[1], "Message was received before authentication"));
                    }
                    catch(ArgumentException exception)
                    {
                        Console.WriteLine($"ERROR: {exception}");
                    }
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("ERR"))
                {
                    string[] content = Messages.ReplyMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("BYE"))
                {
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("REPLY"))
                {
                    string[] content = Messages.ReplyMessage(message);
                    if(content[1] == "OK")
                    {
                        Console.WriteLine($"Action Success: {content[2]}");
                        state = ClientStates.States.OPEN;
                    }
                    else{
                        Console.WriteLine($"Action Failure: {content[2]}");
                        return;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: wrong type of message received");
                    return;
                }
            break;
            case ClientStates.States.JOIN:
                if(message.StartsWith("MSG"))
                {
                    string[] content = Messages.MsgMessage(message);
                    try{
                        await ShowMessage(ConstructErrorMessage(content[1], "Message was received before authentication"));
                    }
                    catch(ArgumentException exception)
                    {
                        Console.WriteLine($"ERROR: {exception}");
                    }
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("ERR"))
                {
                    string[] content = Messages.ReplyMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("BYE"))
                {
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("REPLY"))
                {
                    string[] content = Messages.ReplyMessage(message);
                    if(content[1] == "OK")
                    {
                        Console.WriteLine($"Action Success: {content[2]}");
                        state = ClientStates.States.OPEN;
                    }
                    else{
                        Console.WriteLine($"Action Failure: {content[2]}");
                        return;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: wrong type of message received");
                    return;
                }
            break;
            case ClientStates.States.END:
            break;
            default:
                Console.Error.WriteLine("Error: unknown state");
                return;
        }
    }
    public string ConstructErrorMessage(string displayName, string content)
    {
        if(displayName.Length > 20)
        {
            throw new ArgumentException($"Length of display name: {displayName} is too long, must be less than 20");
        }
        if(content.Length > 60000)
        {
            Console.Error.WriteLine("Warning: Content of message is too long, max 60000 characters, truncating...");
            content = content.Substring(0, 60000);
        }
        return $"ERR FROM {displayName} IS {content}\r\n";
    }
    public async Task ShowMessage(string message)
    {
        if (tcpClient == null || !tcpClient.Connected)
        {
            throw new InvalidOperationException("Client not connected");
        }

        message = message.EndsWith("\r\n") ? message : message + "\r\n";

        await writer.WriteAsync(message);
        await writer.FlushAsync();
    }
    public async Task UserCommands(CancellationToken token)
    {
        
    }
    
    
    public async Task DisconnectAsync()
    {
        cancel.Dispose();    
        reader?.Dispose();
        writer?.Dispose();
        tcpClient?.Dispose();
        stream?.Dispose();
    }

    public async Task ReceiveAsync()
    {
        
    }

    public void Terminate()
    {
        cancel.Cancel();
    }



}



