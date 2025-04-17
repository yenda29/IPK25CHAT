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
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("BYE"))
                {
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
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("ERR"))
                {
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
                    //NOK-return OR OK-OPEN
                    state = ClientStates.States.OPEN;
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
                }
                else if(message.StartsWith("ERR"))
                {
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
                    //NOK OR OK
                    state = ClientStates.States.END;
                    await DisconnectAsync();
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
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.StartsWith("ERR"))
                {
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
                    //NOK OR OK
                    state = ClientStates.States.OPEN;
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



