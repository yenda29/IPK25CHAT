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
        //??reg
        Match content = Regex.Match(message, @"^ERR FROM ([\x21-\x7E]{1,20}) IS ([\x20-\x7E\x0A]{1,60000})\r\n?$");
        if (content.Success)
        {
            return new string[] { "ERR", content.Groups[1].Value, content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }
    public static string[] ByeMessage(string message)
    {
        Match content = Regex.Match(message, @"^BYE FROM ([\x21-\x7E]{1,20})\r\n?$");
        if (content.Success)
        {
            return new string[] { "BYE", content.Groups[1].Value};
        }
        return new string[] { "UNKNOWN", ""};
    }

    public static string[] ReplyMessage(string message)
    {
        Match content = Regex.Match(message, @"^REPLY (OK|NOK) IS ([\x20-\x7E\x0A]{1,60000})\r\n?$");
        if (content.Success)
        {
            return new string[] { "REPLY", content.Groups[1].Value, content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
    }

    public static string[] MsgMessage(string message)
    {
        //??
        Match content = Regex.Match(message, @"^MSG FROM ([\x21-\x7E]{1,20}) IS ([\x20-\x7E\x0A]{1,60000})\r\n?$");
        if (content.Success)
        {
            return new string[] { "MSG", content.Groups[1].Value, content.Groups[2].Value };
        }
        return new string[] { "UNKNOWN", "", "" };
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
        if(input[1].Length > 20 || !UsernameRegex.IsMatch(input[1]))
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
    
}