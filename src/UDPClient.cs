using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;

public class UDPClient : TransportClient
{
    private UdpClient? udpClient;
    private ChatOptions options;
    private ClientStates.States? state = ClientStates.States.START;
    private CancellationTokenSource cancel = new CancellationTokenSource();
    private IPEndPoint server;
    private IPEndPoint current;
    private string username = string.Empty;
    private UInt16 msg = 0;
    private ConcurrentDictionary<ushort, TaskCompletionSource<bool>> confirmations;
    private bool end = false;
    /*
    * Constructor for the UDPClient class.
    * Initializes the UDP client with options, resolving the server host to an IP address.
    */
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
    /*
    * Connects the UDP client to the network.
    * Initializes a new UdpClient instance and binds it to any available IP address and a random port.
    */
    public Task Connect()
    {
        udpClient = new UdpClient();
        udpClient.Client.ReceiveTimeout = options.UDPTimeout;
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        return Task.CompletedTask;
    }
    /*
    * Asynchronously listens for data from the server.
    * Continuously receives data from the server as long as the token is not canceled.
    */
    public async Task ServerData(CancellationToken token)
    {
        try{
            while(true)
            {
                token.ThrowIfCancellationRequested();
    
                if (udpClient == null)
                {
                    throw new InvalidOperationException("UDP Client is not initialized.");
                }
                Task<UdpReceiveResult> receive = udpClient.ReceiveAsync();
                Task completed = await Task.WhenAny(receive, Task.Delay(Timeout.Infinite, token));

                token.ThrowIfCancellationRequested();

                if (completed == receive)
                {
                    var result = await receive;
                    server = result.RemoteEndPoint;
                    if (result.Buffer == null || result.Buffer.Length == 0)
                    {
                        Console.WriteLine("ERROR: No data received from server.");
                        break;
                    }
                    await ServerMessage(result.Buffer);
                }
            }
        }
        catch(OperationCanceledException)
        {   
            if(!end)
            {
                end = true;
                await ShowMessage(Messages.UDPByeMessage(msg, username));
            }
            return;
        }
        catch(Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");
        }
    }
    /* 
    * The ServerMessage method processes incoming UDP messages.
    * If the UDP client is initialized, it creates a confirmation message for the received message ID and sends it to the server.
    * Based on the message type and the client's current state, it performs various actions.
    */
    public async Task ServerMessage(byte[] buffer)
    {
        if (buffer.Length >= 3)
        {
            UInt16 msgId = BitConverter.ToUInt16(buffer, 1);
            if(udpClient != null)
            {
                byte[] confirm = Messages.UDPConfirm(msgId);
                if (!IsClientUsable())
                {
                    Console.WriteLine("ERROR: UDP Client not available (disposed).");
                    return;
                }
                await udpClient.SendAsync(confirm, confirm.Length, server);
                Console.Error.WriteLine($"Sent CONFIRM for message with id: {msgId}");
            }
        }
        
        try{
            ParsedMessage message = Messages.UDPMessage(buffer);
            Console.Error.WriteLine($"Received message: {message.type}, MsgId: {message.msgId}");
            if(message.type == ClientStates.MessageTypes.CONFIRM)
            {
                Console.Error.WriteLine($"Received CONFIRM for MsgId: {message.refMsgId}");
                if(confirmations.TryRemove(message.refMsgId, out TaskCompletionSource<bool>? tcs) && tcs != null)
                {
                    Console.Error.WriteLine($"Confirming MsgId: {message.refMsgId}");
                    tcs.TrySetResult(true);
                }
                else
                {
                    Console.Error.WriteLine($"CONFIRM unknown message ID: {message.refMsgId}");
                }
            }
            
            
            if(message.confirm==true)
            {
                bool isAuthReply = state == ClientStates.States.AUTH && message.type == ClientStates.MessageTypes.REPLY;
                bool isSameIP = server.Address.Equals(current.Address);
                bool isDifferentPort = current.Port != server.Port;
                if (isAuthReply && isSameIP && isDifferentPort)
                {
                    current = server;
                }
                await Confirm(message.msgId);
            }
            
            switch(state)
            {
                case ClientStates.States.START:
                    if(message.type == ClientStates.MessageTypes.ERR)
                    {
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
                        Console.WriteLine($"ERROR: Unknown message type in start {message.type}");            
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
                    else if(message.type == ClientStates.MessageTypes.CONFIRM)
                    {
                        await Confirm(message.msgId);
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: Unknown message type in auth {message.type}");            
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
                        Console.WriteLine($"ERROR: Unknown message type in join {message.type}");            
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
                            Console.WriteLine($"ERROR: {exception.Message}");
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
                        Console.WriteLine($"{message.username}: {message.content}"); 
                    }
                    else if(message.type == ClientStates.MessageTypes.PING)
                    {
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: Unknown message type in open {message.type}");            
                        return;
                    }
                break;
                case ClientStates.States.END:
                break;
                default:
                    Console.WriteLine($"ERROR: Unknow FSM state: {state}");
                break;
            }  
        }
        catch(Exception exception)
        {
            await ShowMessage(Messages.UDPErrMessage(msg, username, "Invalid message format"));
            Console.WriteLine($"ERROR: {exception.Message}");
        } 
    }

    /* 
    * The ShowMessage method sends a message to the server and waits for a confirmation.
    * A confirmation is expected, and the method will retransmit the message if confirmation is not received, 
    * up to a maximum number of attempts.
    */
    public async Task ShowMessage(byte[] message)
    {
        string display = Encoding.UTF8.GetString(message);
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
        Console.Error.WriteLine($"Sending message ID {id}: {display}");
        while(attempts < options.UDPRetransmissions && !confirmed)
        {
            try
            {
                try {
                    if (!IsClientUsable())
                    {
                        Console.WriteLine("ERROR: UDP Client not available (disposed).");
                        return;
                    }
                    await udpClient.SendAsync(message, message.Length, current);
                    Console.Error.WriteLine($"Message ID {id} sent, waiting for confirmation...");
                    var timeout = Task.Delay(options.UDPTimeout);
                    var confirmTask = task.Task;
                    var completedTask = await Task.WhenAny(confirmTask, timeout);
                    if (completedTask == confirmTask)
                    {
                        confirmed = true;
                        confirmations.TryRemove(id, out _);
                        Console.Error.WriteLine($"Message ID {id} confirmed.");
                    }
                    else
                    {
                        attempts++;
                        Console.Error.WriteLine($"Message ID {id} not confirmed, attempt {attempts}.");
                    }  
                } catch (Exception ex) {
                    Console.WriteLine($"ERROR: {ex.Message}");
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
    }
    /* 
    * The ProcessCommand method processes user input for commands.
    * It checks if the message is a command (starting with "/").
    * If it's a command, it processes known commands such as /help, /auth, /join, /rename, and /exit.
    * If it's a regular message, it is sent to the server.
    */
    public async Task ProcessCommand(string message)
    {        
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
            }
        }
    }
    /* 
    * The Join method attempts to join a chat or service.
    * The method can only be invoked when the client is in the OPEN state.
    */
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
    /* 
    * The Authenticate method is used to authenticate the user.
    * The method can only be invoked when the client is in the START or AUTH state.
    */
    public async Task Authenticate(string[] input)
    {
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
    }
    /* 
    * The UserCommands method handles user input from the console in a loop.
    * It continuously listens for user input until the operation is canceled or an error occurs.
    * If the input is a valid command, it is processed using the ProcessCommand method.
    */
    public async Task UserCommands(CancellationToken token)
    {
       try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string ?input = Console.ReadLine();
                if (input == null)
                    break;
                
                await ProcessCommand(input);
            }
        }
        catch (OperationCanceledException)
        {
            if(!end)
            {
                end = true;
                await ShowMessage(Messages.UDPByeMessage(msg, username));
                
            }
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
    /* 
    * The Loop method coordinates the execution of the server and user command tasks.
    * The method starts two tasks: one for the server-side processing (`ServerData`) and one for user commands (`UserCommands`).
    */
    public async Task Loop()
    {
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
        finally
        {
            udpClient?.Dispose(); // tady už je bezpečné zlikvidovat klienta
        }
        
    }
    /* 
    * The Confirm method sends a confirmation message for a given message ID.
    * It constructs a UDP confirmation message using the provided message ID.
    */
    public async Task Confirm(UInt16 msgId)
    {
        byte[] msg = Messages.UDPConfirm(msgId);
        if(udpClient == null)
        {
            throw new InvalidOperationException("UDP Client isn't connected");
        }
        if (!IsClientUsable())
        {
            Console.WriteLine("ERROR: UDP Client not available (disposed).");
            return;
        }
        await udpClient.SendAsync(msg, msg.Length, current);
    }
    /* 
    * The IsClientUsable method checks if the UDP client is in a valid state and ready for use.
    * It verifies if the UDP client and its associated socket are initialized and have a valid handle.
    */
    private bool IsClientUsable()
    {
        try
        {
            return udpClient != null && udpClient.Client != null && udpClient.Client.Handle != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }
    /* 
    * The Setup method handles the entire setup process by connecting the client, running the main loop, and disconnecting.
    * It first attempts to establish a connection with the server.
    * After that, it runs the main loop (`Loop`), handling server communication and user input.
    */
    public async Task Setup()
    {
        try{
            await Connect();
            await Loop();
            await DisconnectAsync();
        }
        catch(Exception exception){
            Console.WriteLine($"ERROR: {exception.Message}");
            await DisconnectAsync();
        }
    }
    /*
    * Disconnects the client by canceling the operation, disposing of resources, and resetting necessary objects.
    */
    public async Task DisconnectAsync()
    {
        try{
            if (udpClient == null){
                Console.Error.WriteLine("Tried to disconnect from disconnected client");
                return;
            }
            udpClient.Close();
            Console.Error.WriteLine("Disconnected");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            cancel?.Dispose();
        }
        await Task.CompletedTask;
        Environment.Exit(0);
        
    }

    public void Terminate()
    {
        cancel.Cancel();
    }
}