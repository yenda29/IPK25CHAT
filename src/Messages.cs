using System;
using System.Text.RegularExpressions;
using System.Text;
/*
*Class to provide message parsing and formatting for a chat application.
*Includes methods for creating and parsing messages, as well as validating input.
*/
class Messages
{
    private static readonly Regex IdRegex = new Regex(@"^[a-zA-Z0-9_-]{1,20}$");
    private static readonly Regex SecretRegex = new Regex(@"^[a-zA-Z0-9_-]{1,128}$");
    private static readonly Regex DisplayNameRegex = new Regex(@"^[\x21-\x7E]{1,20}$");
    private static readonly Regex ContentRegex = new Regex(@"^[\x20-\x7E\x0A]{1,60000}$");
    private static readonly Regex UsernameRegex = new Regex(@"^[a-zA-Z0-9_-]{1,20}$");

    
    /*
    * Parses an ERR message and returns printable message or unknown if invalid input
    */
    public static string[] ErrorMessage(string message)
    {
        Match content = Regex.Match(message, @"^ERR FROM ([\x21-\x7E]{1,20}) IS ([\x20-\x7E\x0A]{1,60000})?$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "ERR", content.Groups[1].Value, content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }
    /*
    * Parses an BYE message and returns printable message or unknown if invalid input
    */
    public static string[] ByeMessage(string message)
    {
        Match content = Regex.Match(message, @"^BYE FROM ([\x21-\x7E]{1,20})?$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "BYE", content.Groups[1].Value};
        }
        return new string[] { "UNKNOWN", ""};
    }
    /*
    * Parses an REPLY message and returns printable message or unknown if invalid input
    */
    public static string[] ReplyMessage(string message)
    {
        Match content = Regex.Match(message, @"^REPLY (OK|NOK) IS ([\x20-\x7E\x0A]{1,60000})$", RegexOptions.IgnoreCase);
        if (content.Success)
        {
            return new string[] { "REPLY", content.Groups[1].Value.ToUpper(), content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }
    /*
    * Parses an MSG message and returns printable message or unknown if invalid input
    */
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
    /*
    * Parses an AUTH message for UDP
    */
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
        msg[1] = byteMsgId[0];
        msg[2] = byteMsgId[1];

        for (int i = 0; i < byteUsername.Length; i++)
        {
            msg[3 + i] = byteUsername[i];
        }

        msg[3 + byteUsername.Length] = 0;

        for (int i = 0; i < byteDisplayName.Length; i++)
        {
            msg[4 + byteUsername.Length + i] = byteDisplayName[i];
        }

        msg[4 + byteUsername.Length + byteDisplayName.Length] = 0;

        for (int i = 0; i < byteSecret.Length; i++)
        {
            msg[5 + byteUsername.Length + byteDisplayName.Length + i] = byteSecret[i];
        }

        msg[5 + byteUsername.Length + byteDisplayName.Length + byteSecret.Length] = 0;

        return msg;
    }
    /*
    * Parses a JOIN message for UDP
    */
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
        msg[1] = byteMsgId[0];
        msg[2] = byteMsgId[1];

        for (int i = 0; i < byteChannelID.Length; i++)
        {
            msg[3 + i] = byteChannelID[i];
        }

        msg[3 + byteChannelID.Length] = 0;

        for (int i = 0; i < byteDisplayName.Length; i++)
        {
            msg[4 + byteChannelID.Length + i] = byteDisplayName[i];
        }

        msg[4 + byteChannelID.Length + byteDisplayName.Length] = 0;

        return msg;
    }
    /*
    * Parses a BYE message for UDP
    */
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
        msg[1] = byteMsgId[0];
        msg[2] = byteMsgId[1];

        for (int i = 0; i < byteDisplayName.Length; i++)
        {
            msg[3 + i] = byteDisplayName[i];
        }

        msg[3 + byteDisplayName.Length] = 0;
        return msg;
    }
    /*
    * Parses an ERR message for UDP
    */
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
        msg[1] = byteMsgId[0];
        msg[2] = byteMsgId[1];

        for (int i = 0; i < byteDisplayName.Length; i++)
        {
            msg[3 + i] = byteDisplayName[i];
        }

        msg[3 + byteDisplayName.Length] = 0;

        int contentStart = 3 + byteDisplayName.Length + 1;
        for (int i = 0; i < byteMessageContent.Length; i++)
        {
            msg[contentStart + i] = byteMessageContent[i];
        }

        msg[contentStart + byteMessageContent.Length] = 0;
        return msg;
    }
    /*
    * Parses a message for UDP
    */
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
        msg[1] = byteMsgId[0];
        msg[2] = byteMsgId[1];
        for (int i = 0; i < byteDisplayName.Length; i++)
        {
            msg[3 + i] = byteDisplayName[i];
        }

        msg[3 + byteDisplayName.Length] = 0;

        int messageStart = 3 + byteDisplayName.Length + 1;
        for (int i = 0; i < byteMessageContent.Length; i++)
        {
            msg[messageStart + i] = byteMessageContent[i];
        }

        msg[messageStart + byteMessageContent.Length] = 0;
        return msg;
    }
    /*
    * Parses an AUTH message
    */
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

    /*
    * Parses an AUTH message
    */
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
    public static ushort CheckEndian(ReadOnlySpan<byte> span)
    {
        if (BitConverter.IsLittleEndian)
        {
            return (ushort)((span[0] << 8) | span[1]);
        }
        else
        {
            return BitConverter.ToUInt16(span);
        }
    }

    public static ParsedMessage UDPMessage(byte[] input)
    {
        if(input.Length >= 3)
        {
            var parsed = new ParsedMessage();
            switch((ClientStates.MessageTypes)input[0])
            {
                case 0x00:
                    parsed.type = ClientStates.MessageTypes.CONFIRM;
                    parsed.refMsgId = CheckEndian(input.AsSpan(1, 2));
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
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.refMsgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.content = MessageContent(input,6);

                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.AUTH:
                    parsed.type = ClientStates.MessageTypes.AUTH;
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.username = MessageContent(input,3);
                    parsed.display = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.secret = MessageContent(input,3 + parsed.username.Length + 1 + parsed.display.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.JOIN:
                    parsed.type = ClientStates.MessageTypes.JOIN;
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.channel = MessageContent(input,3);
                    parsed.display = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.MSG:
                    parsed.type = ClientStates.MessageTypes.MSG;
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.username = MessageContent(input,3);
                    parsed.content = MessageContent(input,3 + parsed.username.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.PING:
                    parsed.type = ClientStates.MessageTypes.PING;
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.ERR:
                    parsed.type = ClientStates.MessageTypes.ERR;
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
                    parsed.display = MessageContent(input,3);
                    parsed.content = MessageContent(input,3 + parsed.display.Length + 1);
                    parsed.confirm = true;
                break;
                case ClientStates.MessageTypes.BYE:
                    parsed.type = ClientStates.MessageTypes.BYE;
                    parsed.msgId = CheckEndian(input.AsSpan(1, 2));
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
        string result = Encoding.UTF8.GetString(input, index, length);
        return result;  
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
        byte[] byteMsgId = MessageIdConvertor(msgId);
        byte [] msg = new byte[3];
        msg[0] = 0x00; 
        msg[1] = byteMsgId[0];
        msg[2] = byteMsgId[1];
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