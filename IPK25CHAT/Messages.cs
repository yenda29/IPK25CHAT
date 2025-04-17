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
    
}