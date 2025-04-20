using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;
using System.Xml;

public class UDPClient : TransportClient
{
    private UdpClient? udpClient;
    private ChatOptions? options;
    private ClientStates.States? state = ClientStates.States.START;
    private CancellationTokenSource cancel = new CancellationTokenSource();
    private IPEndPoint server;
    private IPEndPoint current;
    private string username = string.Empty;
    private UInt16 msg = 0;
    private ConcurrentDictionary<ushort, TaskCompletionSource<bool>> confirmations;
    private bool end = false;

    public UDPClient(ChatOptions parsed)
    {
        options = parsed;
        IPAddress ipAddress = Dns.GetHostEntry(options.ServerHost)
                         .AddressList
                         .First(addr => addr.AddressFamily == AddressFamily.InterNetwork);
        server = new IPEndPoint(ipAddress, options.ServerPort);
        current = server;
        confirmations = new ConcurrentDictionary<ushort, TaskCompletionSource<bool>>();
    }

    public Task Connect()
    {
        udpClient = new UdpClient();
        if (options == null)
        {
            throw new InvalidOperationException("ChatOptions cannot be null.");
        }
        udpClient.Client.ReceiveTimeout = options.UDPTimeout;
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        Console.Error.WriteLine($"Connecting to server...");
        return Task.CompletedTask;
    }

    public async Task ServerData(CancellationToken token)
    {
        try{
            while(true)
            {
                Console.Error.WriteLine("Receiving data...");
                token.ThrowIfCancellationRequested();
    
                UdpReceiveResult receive;
                if (udpClient == null)
                {
                    throw new InvalidOperationException("UDP Client is not initialized.");
                }
                receive = await udpClient.ReceiveAsync();
                server = receive.RemoteEndPoint;

                if(receive.Buffer == null || receive.Buffer.Length == 0)
                {
                    Console.Error.WriteLine("ERROR: No data received from server.");
                    break;
                }
                await ServerMessage(receive.Buffer);
            }
        }
        catch(OperationCanceledException)
        {   
            if(!end)
            {
                end = true;
                Console.Error.WriteLine("Exiting...");
                await ShowMessage(Messages.UDPByeMessage(msg, username));
            }
            return;
        }
        catch(Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
        }
    }

    public async Task ServerMessage(byte[] buffer)
    {
        ParsedMessage message = Messages.UDPMessage(buffer);
        Console.Error.WriteLine($"Received message in state: {state} ref: {message.refMsgId} ID: {message.msgId} from server. TYPE: {message.type} MSG: {message.content}");
        try{
            if(message.type == ClientStates.MessageTypes.CONFIRM)
            {
                if(confirmations.TryRemove(message.refMsgId, out TaskCompletionSource<bool> tcs))
                {
                    Task.Run(() => tcs.TrySetResult(true));
                    Console.Error.WriteLine($"CONFIRM message ID: {message.refMsgId}");
                }
                else
                {
                    Console.Error.WriteLine($"CONFIRM unknown message ID: {message.refMsgId}");
                }
            }
            
            
            if(message.confirm==true)
            {
                Console.Error.WriteLine($"CONFIRMATION for {message.type}, for message ID {message.msgId}");
                bool isAuthReply = state == ClientStates.States.AUTH && message.type == ClientStates.MessageTypes.REPLY;
                bool isSameIP = server.Address.Equals(current.Address);
                bool isDifferentPort = current.Port != server.Port;
                if (isAuthReply && isSameIP && isDifferentPort)
                {
                    current = server;
                    Console.Error.WriteLine($"Switching to port {server.Port}");
                }
                await Confirm(message.msgId);
            }

            switch(state)
            {
                case ClientStates.States.START:
                    if(message.type == ClientStates.MessageTypes.ERR)
                    {
                        Console.WriteLine($"ERROR FROM {message.display}: {message.content}");
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.BYE)
                    {
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.PING)
                    {
                        return;
                    }
                    else
                    {
                        Console.Error.WriteLine($"ERROR: Unknown message type in start {message.type}");            
                        return;
                    }
                break;
                case ClientStates.States.AUTH:
                    if(message.type == ClientStates.MessageTypes.REPLY)
                    {
                        if(message.username == "OK")
                        {
                            Console.WriteLine($"Action Success: {message.content}");
                            state = ClientStates.States.OPEN;
                        }
                        else if(message.username == "NOK")
                        {
                            Console.WriteLine($"Action Failure: {message.content}");
                            return;
                        }
                    }
                    else if(message.type == ClientStates.MessageTypes.ERR)
                    {
                        Console.WriteLine($"ERROR FROM {message.display}: {message.content}");
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.BYE)
                    {
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.MSG)
                    {
                        try{
                            await ShowMessage(Messages.UDPErrMessage(msg, username, "Message received before being authenticated"));
                        }
                        catch(ArgumentException exception)
                        {
                            Console.WriteLine($"ERROR: {exception.Message}");
                        }
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.PING)
                    {
                        return;
                    }
                    else
                    {
                        Console.Error.WriteLine($"ERROR: Unknown message type in reply {message.type}");            
                        return;
                    }
                break;
                case ClientStates.States.JOIN:
                    if(message.type == ClientStates.MessageTypes.REPLY)
                    {
                        if(message.username == "OK")
                        {
                            Console.WriteLine($"Action Success: {message.content}");
                            state = ClientStates.States.OPEN;
                        }
                        else if(message.username == "NOK")
                        {
                            Console.WriteLine($"Action Failure: {message.content}");
                            return;
                        }                       
                    }
                    else if(message.type == ClientStates.MessageTypes.ERR)
                    {
                        Console.WriteLine($"ERROR FROM {message.display}: {message.content}");
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.BYE)
                    {
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.MSG)
                    {
                        Console.WriteLine($"{message.username}: {message.content}"); 
                    }
                    else if(message.type == ClientStates.MessageTypes.PING)
                    {
                        return;
                    }
                    else
                    {
                        Console.Error.WriteLine($"ERROR: Unknown message type in join {message.type}");            
                        return;
                    }
                break;
                case ClientStates.States.OPEN:
                    if(message.type == ClientStates.MessageTypes.REPLY)
                    {
                        if(message.username == "OK")
                        {
                            Console.WriteLine($"Action Success: {message.content}");
                            state = ClientStates.States.OPEN;
                        }
                        else if(message.username == "NOK")
                        {
                            Console.WriteLine($"Action Failure: {message.content}");
                            return;
                        }
                        try{
                            await ShowMessage(Messages.UDPErrMessage(msg, username, "Reply received in OPEN"));
                        }
                        catch(ArgumentException exception)
                        {
                            Console.Error.WriteLine($"ERROR: {exception.Message}");
                        }
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.ERR)
                    {
                        Console.WriteLine($"ERROR FROM {message.display}: {message.content}");
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.BYE)
                    {
                        state = ClientStates.States.END;
                        await DisconnectAsync();
                    }
                    else if(message.type == ClientStates.MessageTypes.MSG)
                    {
                        Console.Error.WriteLine($"{message.username}: {message.content}"); 
                    }
                    else if(message.type == ClientStates.MessageTypes.PING)
                    {
                        return;
                    }
                    else
                    {
                        Console.Error.WriteLine($"ERROR: Unknown message type in open {message.type}");            
                        return;
                    }
                break;
                case ClientStates.States.END:
                break;
                default:
                    Console.Error.WriteLine($"ERROR: Unknow FSM state: {state}");
                break;
            }  
        }
        catch(Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");
        } 
    }

    public async Task ShowMessage(byte[] message)
    {
        string display = Encoding.UTF8.GetString(message);
        Console.Error.WriteLine($"Sending {msg} message to server. MSG: {display}");
        if(udpClient == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        var task = new TaskCompletionSource<bool>();
        confirmations.TryAdd(msg, task);
        int attempts = 0;
        bool confirmed = false;
        ushort id = msg;
        msg++;
        while(attempts < options.UDPRetransmissions && !confirmed)
        {
            try
            {
                try {
                    await udpClient.SendAsync(message, message.Length, current);
                } catch (Exception ex) {
                    Console.Error.WriteLine($"ERROR SEND: {ex.Message}");
                }
                var timeout = Task.Delay(options.UDPTimeout);
                var confirmTask = task.Task;
                var completedTask = await Task.WhenAny(confirmTask, timeout);
                if (completedTask == confirmTask)
                {
                    confirmed = true;
                    confirmations.TryRemove(id, out _);
                    Console.Error.WriteLine($"Confirmation {id} received for msg {display}");
                }
                else
                {
                    attempts++;
                    Console.Error.WriteLine($"No confirmation received for msg {id}. Retrying... ({attempts}/{options.UDPRetransmissions})");
                }  
            }
            catch (SocketException exception)
            {
                Console.WriteLine($"ERROR: {exception.Message}");
            }
        }
        if(!confirmed)
        {
            Console.WriteLine($"ERROR: No confirmation for message ID {id}, tried {attempts} times");
            confirmations.TryRemove(id, out _);
        }
        Console.Error.WriteLine($"Message {id} sent successfully.");
    }
    public async Task ProcessCommand(string message)
    {
        Console.Error.WriteLine("User command processing");
        if(message == null)
        {
            return;
        }
        if(message.StartsWith("/"))
        {
            string[] input = message.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if(input[0] == "/help")
            {
                Messages.CommandsHelp();
            }
            else if(input[0] == "/auth")
            {                
                Console.Error.WriteLine("Authenticating...");
                if(input.Length == 4)
                {
                    await Authenticate(input);
                }
                else{
                    Console.WriteLine("ERROR: Invalid params, /help to see needed parameters");
                }
            }
            else if(input[0] == "/join")
            {
                Console.Error.WriteLine($"Joining channel: {input[1]}");
                if(input.Length == 2)
                {
                    await Join(input);
                }
                else{
                    Console.WriteLine("ERROR: Invalid params, /help to see needed parameters");
                }
            }
            else if(input[0] == "/rename")
            {
                Console.Error.WriteLine($"Renaming user to: {input[1]}");
                if(input.Length == 2)
                {
                    username = Messages.Rename(input);
                }
                else{
                    Console.WriteLine("ERROR: Invalid params, /help to see needed parameters");
                }
            }
            else if(input[0] == "/exit")
            {
                state = ClientStates.States.END;
                Terminate();
            }
            else
            {
                Console.WriteLine("ERROR: unknown command, /help to see needed parameters");
            }
        }
        else
        {
            if(state == ClientStates.States.OPEN || state == ClientStates.States.JOIN)
            {
                byte[] output = Messages.UDPMessage(msg, message, username);
                await ShowMessage(output);
            }
            else
            {
                Console.WriteLine("ERROR: unknown command, use /help");
                DisconnectAsync();
            }
        }
    }
    public async Task Join(string[] input)
    {
        if(state == ClientStates.States.OPEN)
        {
            try{
                byte[] output = Messages.UDPJoinMessage(msg, input, username);
                state = ClientStates.States.JOIN;
                await ShowMessage(output);
            }
            catch(Exception ex){
                Console.WriteLine($"ERROR: {ex.Message}");
                state = ClientStates.States.OPEN;
            }
            
        }
        else{
            Console.WriteLine("ERROR: Can only join from open state");
            return;
        }
    }
    public async Task Authenticate(string[] input)
    {
        Console.Error.WriteLine($"Authenticating as {input[1]} with display name {input[3]}");
        if(state == ClientStates.States.START || state == ClientStates.States.AUTH)
        {
            try{
                byte[] output = Messages.UDPAuthMessage(msg, input);
                username = input[1];
                state = ClientStates.States.AUTH;
                await ShowMessage(output);
            }
            catch(Exception exception)
            {
                Console.WriteLine($"ERROR: {exception.Message}");
            }
        }
        else
        {
            Console.WriteLine("ERROR: Already authenticated");
            return;
        }
        Console.Error.WriteLine($"Authentication message sent.");
    }
    public async Task UserCommands(CancellationToken token)
    {
       Console.Error.WriteLine("User input processs"); 
       try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string ?input = Console.ReadLine();
                if (input == null)  // EOF (Ctrl+D)
                    break;
                
                await ProcessCommand(input);
            }
        }
        catch (OperationCanceledException)
        {
            if(!end)
            {
                end = true;
                Console.Error.WriteLine("Exiting...");
                await ShowMessage(Messages.UDPByeMessage(msg, username));
                
            }
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    public async Task Loop()
    {
        Console.Error.WriteLine("Connected to server. Type /help for available commands.");
        state = ClientStates.States.START;
        var server = ServerData(cancel.Token);
        var user = UserCommands(cancel.Token);

        await Task.WhenAny(server, user);
        cancel.Cancel();
        
        try
        {
            await Task.WhenAll(server,user);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("ERROR: Tasks cancelled");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");
        }
        
    }
    public async Task Confirm(UInt16 msgId)
    {
        byte[] msg = Messages.UDPConfirm(msgId);
        Console.Error.WriteLine($"Sending confirmation for:  {msg} ON PORT: {current.Port}");
        if(udpClient == null)
        {
            throw new InvalidOperationException("UDP Client isn't connected");
        }
        await udpClient.SendAsync(msg, msg.Length, current);
    }
    public async Task Setup()
    {
        try{
            await Connect();
            await Loop();
            await DisconnectAsync();
        }
        catch(Exception exception){
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            await DisconnectAsync();
        }
    }

    public async Task DisconnectAsync()
    {
        cancel?.Cancel();
        cancel?.Dispose();
        cancel = new CancellationTokenSource();
        udpClient?.Dispose();
        Console.Error.WriteLine("Disconnected from server.");

        //await Task.Delay(1000);
        Environment.Exit(0);
    }

    public void Terminate()
    {
        Console.Error.WriteLine("Shutting down...");
        cancel.Cancel();
    }
}