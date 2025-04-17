using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Program{

    static TCPClient? tcpCommunication;

    
    static async Task Main(string[] args){
        
        ArgParser parser = new ArgParser();
        ChatOptions options = parser.Parse(args);

        if(options.TransportProtocol == "tcp")
        {
            tcpCommunication = new TCPClient(options); 
            await tcpCommunication.Setup();
        }
        else if(options.TransportProtocol == "udp")
        {
            //UDPClient udpCommunication = new UDPClient(options);
            //await udpCommunication.Setup();
        }
    }
}
