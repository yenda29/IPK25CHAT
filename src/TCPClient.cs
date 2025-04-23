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


    /*
    * Asynchronously connects to the chat server using the configured options.
    * Initializes TcpClient, network stream, and stream reader/writer.
    */
    public async Task Connect()
    {
        tcpClient = new TcpClient();
        if (options == null)
        {
            throw new InvalidOperationException("ChatOptions cannot be null.");
        }
        await tcpClient.ConnectAsync(options.ServerHost, options.ServerPort);
        stream = tcpClient.GetStream();
        writer = new StreamWriter(stream, Encoding.ASCII) 
        { 
            AutoFlush = true 
        };
        reader = new StreamReader(stream, Encoding.ASCII);
    }

    /*
    * Main loop of the chat client.
    * Starts two parallel tasks: one for handling server messages and one for user commands.
    * Cancels both tasks when one of them completes.
    * Handles cancellation and other exceptions gracefully.
    */
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

    /*
    * Sets up the entire chat session lifecycle.
    * Connects to the server, enters the main loop, and disconnects afterward.
    * Handles any exceptions that occur during the process and ensures disconnection.
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
    * Asynchronously reads data from the server and processes incoming messages.
    * Continuously listens for new messages, handles cancellations, and processes each message.
    * If cancellation is requested, a "bye" message is sent to the server.
    * Handles exceptions related to message reading and processing.
    */
    public async Task ServerData(CancellationToken token)
    {
        
        try{
            while(true)
            {
                token.ThrowIfCancellationRequested();
                if (reader == null)
                {
                    throw new InvalidOperationException("Reader is not initialized.");
                }
                string? inputMessage = await reader.ReadLineAsync();
                if(inputMessage==null)
                {
                    break;
                }
                await ServerMessage(inputMessage);
            }

        }
        catch(OperationCanceledException)
        {
            await ShowMessage(ConstructByeMessage(username));
            return;
        }
        catch(Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");
        }
        
    }
    /*
    * Asynchronously processes server messages based on the current client state.
    * Handles messages.
    * Performs state transitions and displays appropriate error or success messages.
    * Ensures that unexpected or invalid messages are properly handled and the client disconnects if necessary.
    */
    public async Task ServerMessage(string message)
    {
        Console.Error.WriteLine($"Server message: {message}");
        switch(state)
        {
            case ClientStates.States.START:
                if(message.ToUpper().StartsWith("ERR"))
                {
                    string[] content = Messages.ErrorMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("BYE"))
                {
                    string[] content = Messages.ByeMessage(message);
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else
                {
                    Console.Error.WriteLine("ERROR: wrong type of message received");
                    Console.WriteLine($"ERROR: Unexpected or invalid message received from server");
                    string errMsg = ConstructErrorMessage(username, "Unexpected or invalid message received from server");
                    await ShowMessage(errMsg);
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
            break;
            case ClientStates.States.AUTH:
                if(message.ToUpper().StartsWith("MSG"))
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
                else if(message.ToUpper().StartsWith("ERR"))
                {
                    string[] content = Messages.ErrorMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("BYE"))
                {
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("REPLY"))
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
                    Console.Error.WriteLine("ERROR: wrong type of message received");
                    Console.WriteLine($"ERROR: Unexpected or invalid message received from server");
                    string errMsg = ConstructErrorMessage(username, "Unexpected or invalid message received from server");
                    await ShowMessage(errMsg);
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
            break;
            case ClientStates.States.OPEN:
                if(message.ToUpper().StartsWith("MSG"))
                {
                    string[] content = Messages.MsgMessage(message);
                    Console.WriteLine($"{content[1]}: {content[2]}");          
                }
                else if(message.ToUpper().StartsWith("ERR"))
                {
                    string[] content = Messages.ErrorMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("BYE"))
                {
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("REPLY"))
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
                    Console.Error.WriteLine("ERROR: wrong type of message received");
                    Console.WriteLine($"ERROR: Unexpected or invalid message received from server");
                    string errMsg = ConstructErrorMessage(username, "Unexpected or invalid message received from server");
                    await ShowMessage(errMsg);
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
            break;
            case ClientStates.States.JOIN:
                if(message.ToUpper().StartsWith("MSG"))
                {
                    string[] content = Messages.MsgMessage(message);
                    try{
                        string output = Messages.Message(content, username);
                        await ShowMessage(output);
                    }
                    catch(ArgumentException exception)
                    {
                        Console.WriteLine($"ERROR: {exception}");
                    }
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("ERR"))
                {
                    string[] content = Messages.ErrorMessage(message);
                    Console.WriteLine($"ERROR FROM {content[1]}: {content[2]}");
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("BYE"))
                {
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
                else if(message.ToUpper().StartsWith("REPLY"))
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
                    Console.Error.WriteLine("ERROR: wrong type of message received");
                    Console.WriteLine($"ERROR: Unexpected or invalid message received from server");
                    string errMsg = ConstructErrorMessage(username, "Unexpected or invalid message received from server");
                    await ShowMessage(errMsg);
                    state = ClientStates.States.END;
                    await DisconnectAsync();
                }
            break;
            case ClientStates.States.END:
            break;
            default:
                Console.WriteLine("ERROR: unknown state");
                return;
        }
    }
    /*
    * Constructs an error message formatted for server communication.
    * Validates the length of the display name and content.
    * Returns a formatted error message
    */
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
    /*
    * Constructs a "BYE" message to notify the server of disconnection.
    * Validates the length of the display name.
    */
    public string ConstructByeMessage(string displayName)
    {
        if(displayName.Length > 20)
        {
            throw new ArgumentException($"Length of display name: {displayName} is too long, must be less than 20");
        }
        return $"BYE FROM {displayName}\r\n";
    }
    /*
    * Sends a message to the connected server.
    * Validates the connection and writer initialization.
    * If the client is not connected or writer is uninitialized, an exception is thrown.
    */
    public async Task ShowMessage(string message)
    {
        if (tcpClient == null || !tcpClient.Connected)
        {
            throw new InvalidOperationException("Client not connected");
        }

        message = message.EndsWith("\r\n") ? message : message + "\r\n";

        if (writer == null)
        {
            throw new InvalidOperationException("Writer is not initialized.");
        }
        await writer.WriteAsync(message);
        await writer.FlushAsync();
    }
    /*
    * Continuously listens for user commands and processes them.
    * Processes commands or sends a "BYE" message if no input is provided.
    */
    public async Task  UserCommands(CancellationToken token)
    {
        try{
            while (true)
            {
                var read = Task.Run(() => Console.ReadLine());
                var done = await Task.WhenAny(read, Task.Delay(Timeout.Infinite, token).ContinueWith(_ => (string?)null));
                if(token.IsCancellationRequested)
                {
                    Console.Error.WriteLine("Exiting...");
                    token.ThrowIfCancellationRequested();
                    break;
                }
                string input = await read ?? string.Empty;
                if(input == string.Empty)
                {
                    await ShowMessage(ConstructByeMessage(username));
                    Terminate();
                    break;
                }
                await ProcessCommand(input);
            }
        }
        catch(OperationCanceledException)
        {
            await ShowMessage(ConstructByeMessage(username));
        }
        catch(Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");
        }
    }
    /*
    * Authenticates the user by sending an authentication message to the server.
    * Sends the authentication message using `ShowMessage`.
    * If the state is not START or AUTH, logs an error.
    */
    public async Task Authenticate(string[] input)
    {
        if(state == ClientStates.States.START || state == ClientStates.States.AUTH)
        {
            try{
                string output = Messages.AuthMessage(input);
                username = input[3];
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
    * Joins a chatroom after verifying that the current state is OPEN.
    * If the state is not OPEN, an error is logged.
    */
    public async Task Join(string[] input)
    {
        if(state == ClientStates.States.OPEN)
        {
            try{
                string output = Messages.JoinMessage(input, username);
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
    * Processes a user command. Commands that start with '/' are handled here.
    * If the message is a command, it splits the input and calls the appropriate method.
    * If the message is not a command, it sends a message to the server if the state is OPEN or JOIN.
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
            else{
                Console.WriteLine("ERROR: unknown command, /help to see needed parameters");
            }
        }
        else{
            if(state == ClientStates.States.OPEN || state == ClientStates.States.JOIN)
            {
                string output = Messages.MessageSend(message, username);
                await ShowMessage(output);
            }
            else
            {
                Console.WriteLine("ERROR: unknown command, use /help");
            }
        }
    }
    /*
    * Disconnects the client by canceling the operation, disposing of resources, and resetting necessary objects.
    */
    public async Task DisconnectAsync()
    {
        cancel?.Cancel();
        cancel?.Dispose();
        cancel = new CancellationTokenSource();
        reader?.Dispose();
        writer?.Dispose();
        tcpClient?.Dispose();
        stream?.Dispose();
        tcpClient?.Dispose();
        
        await Task.CompletedTask;
    }

    public void Terminate()
    {
        cancel.Cancel();
    }
}