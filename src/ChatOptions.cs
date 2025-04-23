using System;
using System.Net;

/**
 * Class to hold configuration options for a chat application.
 * Includes properties for server host, port, transport protocol, UDP timeout, and UDP retransmissions. 
 * Host address/IP and transport protocol are mandatory and are set by user.
 */
public class ChatOptions
{
    public string ServerHost { get; set; } = string.Empty;
    public ushort ServerPort { get; set; } = 4567;
    public string TransportProtocol { get; set; } = string.Empty;
    public ushort UDPTimeout { get; set; } = 250;
    public byte UDPRetransmissions { get; set; } = 3;
}
