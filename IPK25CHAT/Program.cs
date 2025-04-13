using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Program{

    static async Task Main(string[] args){
        
        ArgParser parser = new ArgParser();
        ChatOptions options = parser.Parse(args);

        if(options.TransportProtocol == "tcp"){
            
        }else if(options.TransportProtocol == "udp"){
            
        }
    }
}
