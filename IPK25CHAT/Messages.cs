using System;
using System.Text.RegularExpressions;
using System.Text;

class Messages
{
    private static readonly Regex IdRegex = new Regex(@"^[a-zA-Z0-9_-]{1,20}$");
    private static readonly Regex SecretRegex = new Regex(@"^[a-zA-Z0-9_-]{1,128}$");
    private static readonly Regex DisplayNameRegex = new Regex(@"^[\x21-\x7E]{1,20}$");
    private static readonly Regex ContentRegex = new Regex(@"^[\x20-\x7E\x0A]{1,60000}$");
    private static readonly Regex UsernameRegex = new Regex(@"^[a-zA-Z0-9_-]{1,20}$");

    public static string[] ErrorMessage(string message)
    {
        Match content = Regex.Match(message, @"^ERR FROM ([\x21-\x7E]{1,20}) IS ([\x20-\x7E\x0A]{1,60000})?$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "ERR", content.Groups[1].Value, content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }
    public static string[] ByeMessage(string message)
    {
        Match content = Regex.Match(message, @"^BYE FROM ([\x21-\x7E]{1,20})?$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "BYE", content.Groups[1].Value};
        }
        return new string[] { "UNKNOWN", ""};
    }

    public static string[] ReplyMessage(string message)
    {
        Match content = Regex.Match(message, @"^REPLY (OK|NOK) IS ([\x20-\x7E\x0A]{1,60000})$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "REPLY", content.Groups[1].Value.ToUpper(), content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }

    public static string[] MsgMessage(string message)
    {
        Match content = Regex.Match(message, @"^MSG FROM ([\x21-\x7E]{1,20}) IS ([\x20-\x7E\x0A]{1,60000})$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "MSG", content.Groups[1].Value, content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }
    public static void CommandsHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("/help - prints out help");
        Console.WriteLine("/auth {Username} {Secret} {DisplayName} - Sends AUTH message with the data provided");
        Console.WriteLine("/join {ChannelID} - Sends JOIN message with channel name");
        Console.WriteLine("/rename {DisplayName} - Locally changes the display name");
        Console.WriteLine("/exit - Exits");
    }
    public static byte[] UDPAuthMessage(UInt16 msgId, string[] input)
    {
        if (input[1].Length > 20 || !UsernameRegex.IsMatch(input[1]))
        {
            throw new ArgumentException("ERROR: Length of ID can be max 20 and contain only letters, numbers, hyphens and underscores");
        }
        if (input[2].Length > 128 || !SecretRegex.IsMatch(input[2]))
        {
            throw new ArgumentException("ERROR: Length of secret can be max 128 and contain only letters, numbers, hyphens and underscores");
        }
        if (input[3].Length > 20 || !DisplayNameRegex.IsMatch(input[3]))
        {
            throw new ArgumentException("ERROR: Length of display name can be max 20 and contain only printable ASCII characters");
        }

        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] byteUsername = Encoding.UTF8.GetBytes(input[1]); 
        byte [] byteDisplayName = Encoding.UTF8.GetBytes(input[3]);
        byte [] byteSecret = Encoding.UTF8.GetBytes(input[2]);
        int msgSize = 1 + 2 + byteUsername.Length + 1 + byteDisplayName.Length + 1 + byteSecret.Length + 1;
        byte [] msg = new byte[msgSize];
        msg[0] = 0x02;
        Buffer.BlockCopy(byteMsgId, 0, msg, 1, 2);
        Buffer.BlockCopy(byteUsername, 0, msg, 3, byteUsername.Length);
        Buffer.BlockCopy(byteDisplayName, 0, msg, 3 + byteUsername.Length + 1, byteDisplayName.Length);
        Buffer.BlockCopy(byteSecret, 0, msg, 3 + byteUsername.Length + 1 + byteDisplayName.Length + 1, byteSecret.Length);
        return msg;
    }
    public static byte[] UDPJoinMessage(UInt16 msgId, string[] input, string username)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of ID can be max 20 and contain only letters, numbers, hyphens and underscores");
        }

        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] byteChannelID = Encoding.UTF8.GetBytes(input[1]); 
        byte [] byteDisplayName = Encoding.UTF8.GetBytes(username);
        int msgSize = 1 + 2 + byteChannelID.Length + 1 + byteDisplayName.Length + 1;
        byte [] msg = new byte[msgSize];
        msg[0] = 0x03;
        Buffer.BlockCopy(byteMsgId, 0, msg, 1, 2);
        Buffer.BlockCopy(byteChannelID, 0, msg, 3, byteChannelID.Length);
        Buffer.BlockCopy(byteDisplayName, 0, msg, 3 + byteChannelID.Length + 1, byteDisplayName.Length);
        return msg;
        
    }
    public static byte[] UDPByeMessage(UInt16 msgId, string username)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of ID can be max 20 and contain only letters, numbers, hyphens and underscores");
        }

        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] byteDisplayName = Encoding.UTF8.GetBytes(username); 
        int msgSize = 1 + 2 + byteDisplayName.Length + 1;
        byte [] msg = new byte[msgSize];
        msg[0] = 0xFF;
        Buffer.BlockCopy(byteMsgId, 0, msg, 1, 2);
        Buffer.BlockCopy(byteDisplayName, 0, msg, 3, byteDisplayName.Length);
        return msg;
        
    }
    public static byte[] UDPErrMessage(UInt16 msgId, string username, string content)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of ID can be max 20 and contain only letters, numbers, hyphens and underscores");
        }
        if(content.Length > 60000)
        {
            Console.WriteLine("ERROR: Length of message can be max 60000, truncating...");
            content = content.Substring(0, 60000);
        }

        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] byteDisplayName = Encoding.UTF8.GetBytes(username); 
        byte [] byteMessageContent = Encoding.UTF8.GetBytes(content);
        int msgSize = 1 + 2 + byteDisplayName.Length + 1 + byteMessageContent.Length + 1;
        byte [] msg = new byte[msgSize];
        msg[0] = 0xFE; 
        Buffer.BlockCopy(byteMsgId, 0, msg, 1, 2);
        Buffer.BlockCopy(byteDisplayName, 0, msg, 3, byteDisplayName.Length);
        Buffer.BlockCopy(byteMessageContent, 0, msg, 3 + byteDisplayName.Length + 1, byteMessageContent.Length);
        return msg;
    }
    
    public static byte[] UDPMessage(UInt16 msgId, string input, string username)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of display name can be max 20 and contain only printable ASCII characters");
        }
        if(input.Length > 60000)
        {
            Console.WriteLine("ERROR: Length of message can be max 60000, truncating...");
            input = input.Substring(0, 60000);
        }

        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] byteDisplayName = Encoding.UTF8.GetBytes(username); 
        byte [] byteMessageContent = Encoding.UTF8.GetBytes(input);
        int msgSize = 1 + 2 + byteDisplayName.Length + 1 + byteMessageContent.Length + 1;
        byte [] msg = new byte[msgSize];
        msg[0] = 0x04; 
        Buffer.BlockCopy(byteMsgId, 0, msg, 1, 2);
        Buffer.BlockCopy(byteDisplayName, 0, msg, 3, byteDisplayName.Length);
        Buffer.BlockCopy(byteMessageContent, 0, msg, 3 + byteDisplayName.Length + 1, byteMessageContent.Length);
        return msg;
    }
    public static string AuthMessage(string[] input)
    {
        if(input[1].Length > 20 || !UsernameRegex.IsMatch(input[1]))
        {
            throw new ArgumentException("ERROR: Length of ID can be max 20 and contain only letters, numbers, hyphens and underscores");
        }
        if(input[2].Length > 128 || !SecretRegex.IsMatch(input[2]))
        {
            throw new ArgumentException("ERROR: Length of secret can be max 128 and contain only letters, numbers, hyphens and underscores");
        }
        if(input[3].Length > 20 || !DisplayNameRegex.IsMatch(input[3]))
        {
            throw new ArgumentException("ERROR: Length of display name can be max 20 and contain only printable ASCII characters");
        }
        return $"AUTH {input[1]} AS {input[3]} USING {input[2]}\r\n";
    }

    public static string JoinMessage(string[] input, string username)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of ID can be max 20 and contain only letters, numbers, hyphens and underscores");
        }
        return $"JOIN {input[1]} AS {username}\r\n";
    }

    public static string Rename(string[] input)
    {
        if(input[1].Length > 20)
        {
            Console.WriteLine("ERROR: Length of display name can be max 20, truncating...");
            input[1] = input[1].Substring(0, 20);
        }
        if(!DisplayNameRegex.IsMatch(input[1]))
        {
            throw new ArgumentException("ERROR: Display name can contain only printable ASCII characters");
        }
        return input[1];
    }
    public static string MessageSend(string input, string username)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of display name can be max 20 and contain only printable ASCII characters");
        }
        if(input.Length > 60000)
        {
            Console.WriteLine("ERROR: Length of message can be max 60000, truncating...");
            input = input.Substring(0, 60000);
        }
        return $"MSG FROM {username} IS {input}";
    }

    public static string Message(string[] input, string username)
    {
        if(username.Length > 20 || !UsernameRegex.IsMatch(username))
        {
            throw new ArgumentException("ERROR: Length of display name can be max 20 and contain only printable ASCII characters");
        }
        if(input[1].Length > 60000)
        {
            Console.WriteLine("ERROR: Length of message can be max 60000, truncating...");
            input[1] = input[1].Substring(0, 60000);
        }
        return $"MSG FROM {username} IS {input[1]}";
    }
    private static UInt16 MessageIdParser(byte [] byteMsgId){
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(byteMsgId);
        }
        return  BitConverter.ToUInt16(byteMsgId, 0);
    }

    public static ParsedMessage UDPMessage(byte[] input)
    {
        if(input.Length >= 3)
        {
            var parsed = new ParsedMessage();
            byte[] refBytes = new byte[2];
            byte[] msgBytes = new byte[2];
            switch((ClientStates.MessageTypes)input[0])
            {
                case 0x00:
                    parsed.type = ClientStates.MessageTypes.CONFIRM;
                    Buffer.BlockCopy(input, 1, refBytes, 0, 2);
                    parsed.refMsgId = MessageIdParser(refBytes);
                break;
                case ClientStates.MessageTypes.REPLY:
                    parsed.type = ClientStates.MessageTypes.REPLY;
                    if(input[3] == 0x00)
                    {
                        parsed.username = "NOK";
                    }
                    else if(input[3] == 0x01)
                    {
                        parsed.username = "OK";
                    }
                    else
                    {
                        throw new ArgumentException("ERROR: Malformed format of message");
                    }
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    Buffer.BlockCopy(input, 4, refBytes, 0, 2);
                    parsed.refMsgId= MessageIdParser(refBytes);
                    parsed.content = MessageContent(input,6);

                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.AUTH:
                    parsed.type = ClientStates.MessageTypes.AUTH;
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    parsed.username = MessageContent(input,3);
                    parsed.display = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.secret = MessageContent(input,3 + parsed.username.Length + 1 + parsed.display.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.JOIN:
                    parsed.type = ClientStates.MessageTypes.JOIN;
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    parsed.channel = MessageContent(input,3);
                    parsed.display = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.MSG:
                    parsed.type = ClientStates.MessageTypes.MSG;
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    parsed.username = MessageContent(input,3);
                    parsed.content = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.PING:
                    parsed.type = ClientStates.MessageTypes.PING;
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.ERR:
                    parsed.type = ClientStates.MessageTypes.ERR;
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    parsed.username = MessageContent(input,3);
                    parsed.content = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.BYE:
                    parsed.type = ClientStates.MessageTypes.BYE;
                    Buffer.BlockCopy(input, 1, msgBytes, 0, 2);
                    parsed.msgId = MessageIdParser(msgBytes);
                    parsed.username = MessageContent(input,3);
                    parsed.confirm = true;
                break;
            }
            return parsed;
        }
        else{
            throw new ArgumentException("ERROR: Malformed format of message");
        }
    }
    public static string MessageContent(byte[] input,int index)
    {
        int end = index;
        while (end < input.Length && input[end] != 0)
        {
            end++;
        }
        int length = end - index;
        byte[] bytes = new byte[length];
        Buffer.BlockCopy(input, index, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }
    private static byte [] MessageIdConvertor(UInt16 msgId){
        byte [] byteMsgId = BitConverter.GetBytes(msgId);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(byteMsgId);
        }
        return byteMsgId;
    }

    public static byte[] UDPConfirm(UInt16 msgId)
    {
        Console.Error.WriteLine($"CREATING CONFIRM FOR: ID:{msgId}");
        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] msg = new byte[3];
        msg[0] = 0x00; 
        Buffer.BlockCopy(byteMsgId, 0, msg, 1, 2);
        return msg;
    }
    
}
public class ParsedMessage
{
    public ClientStates.MessageTypes type { get; set; } = ClientStates.MessageTypes.UNDEF;
    public ushort msgId { get; set; } = 0;
    public ushort refMsgId { get; set; } = 0;
    public bool confirm { get; set; } = false;
    public string content { get; set; } = "";
    public string username { get; set; } = "";
    public string display { get; set; } = "";
    public string secret { get; set; } = "";
    public string channel { get; set; } = "";
}