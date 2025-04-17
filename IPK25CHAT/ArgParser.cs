using System;
using System.Net;

public class ArgParser
{
    public ChatOptions Parse(string[] args)
    {
        ChatOptions options = new ChatOptions();

        if(args.Length == 1 && args[0] == "-h")
        {
            PrintHelp();
            Environment.Exit(0);
        }

        for(int i=0; i<args.Length;i++)
        {
            switch(args[i])
            {
                case "-t":
                    if(i + 1 < args.Length)
                    {
                        options.TransportProtocol = args[++i].ToLower();
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Missing argument for -t");
                        Environment.Exit(1);
                    }
                    break;
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        string serverHost = args[++i];
                        // Check if the serverHost is a valid IPv4 address
                        if (IPAddress.TryParse(serverHost, out IPAddress? ipAddress) && ipAddress != null && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            // It's a valid IPv4 address
                            options.ServerHost = serverHost;
                        }
                        else
                        {
                            // It's not an IPv4 address, so treat it as a hostname and resolve it
                            try
                            {
                                IPHostEntry hostEntry = Dns.GetHostEntry(serverHost);
                                var resolvedAddress = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
                                if (resolvedAddress != null)
                                {
                                    options.ServerHost = resolvedAddress;
                                }
                                else
                                {
                                    Console.Error.WriteLine("ERROR: Unable to resolve hostname to IPv4 address.");
                                    Environment.Exit(1);
                                }
                            }
                            catch (Exception exception)
                            {
                                Console.Error.WriteLine($"ERROR: Unable to resolve hostname '{serverHost}' - {exception.Message}");
                                Environment.Exit(1);
                            }
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Missing argument for -s");
                        Environment.Exit(1);
                    }
                    break;
                case "-p":
                    if(i + 1 < args.Length)
                    {
                        if(ushort.TryParse(args[++i], out ushort port))
                        {
                            options.ServerPort = port;
                        }
                        else
                        {
                            Console.Error.WriteLine("ERROR: Invalid port number");
                            Environment.Exit(1);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Missing argument for -p");
                        Environment.Exit(1);
                    }
                    break;
                case "-d":
                    if(i + 1 < args.Length)
                    {
                        if(ushort.TryParse(args[++i], out ushort timeout))
                        {
                            options.UDPTimeout = timeout;
                        }
                        else
                        {
                            Console.Error.WriteLine("ERROR: Invalid UDP timeout");
                            Environment.Exit(1);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Missing argument for -d");
                        Environment.Exit(1);
                    }
                    break;
                case "-r":
                    if(i + 1 < args.Length)
                    {
                        if(byte.TryParse(args[++i], out byte retransmissions))
                        {
                            options.UDPRetransmissions = retransmissions;
                        }
                        else
                        {
                            Console.Error.WriteLine("ERROR: Invalid UDP retransmissions");
                            Environment.Exit(1);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Missing argument for -r");
                        Environment.Exit(1);
                    }
                    break;
                default:
                    Console.Error.WriteLine($"ERROR: Unknown argument: {args[i]}");
                    Environment.Exit(1);
                break;
            }
        }
        if(options.TransportProtocol == string.Empty)
        {
            Console.Error.WriteLine("ERROR: Transport protocol is required");
            Environment.Exit(1);
        }
        if(options.TransportProtocol != "tcp" && options.TransportProtocol != "udp")
        {
            Console.Error.WriteLine("ERROR: Invalid transport protocol, use 'tcp' or 'udp'");
            Environment.Exit(1);
        }
        if(options.ServerHost == string.Empty)
        {
            Console.Error.WriteLine("ERROR: Server host is required");
            Environment.Exit(1);
        }
        return options;
    }

    static void PrintHelp()
    {
        Console.WriteLine("IPK25CHAT USAGE:");
        Console.WriteLine("REGUIRED ARGUMENTS:");
        Console.WriteLine("  -t <tcp|udp>            Transport protocol used for connection");
        Console.WriteLine("  -s <IP|hostname>        Server IP address or hostname");
        Console.WriteLine("OPTIONAL ARGUMENTS:");
        Console.WriteLine("  -p <port>               Server port (default: 4567)");
        Console.WriteLine("  -d <timeout>            UDP confirmation timeout in milliseconds (default: 250)");
        Console.WriteLine("  -r <retransmissions>    Maximum number of UDP retransmissions (default: 3)");
        Console.WriteLine("  -h                      Prints this help message and exits");
    }
}