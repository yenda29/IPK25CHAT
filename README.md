# Documentation for IPK25CHAT application
- Author: Jonáš Herzig (xherzi00)
- Date: April 20, 2025

# Table of contents
1. [Introduction](#introduction)
2. [Compilation](#compilation)
3. [Usage](#usage)
4. [Theory](#theory)
   - [TCP](#tcp)
   - [UDP](#udp)
5. [Implementation](#implementation)
6. [Testing](#testing)
7. [Bibliography](#bibliography)

## Introduction
In this documentation I'll try to cover some basic theory behind this project and the two protocols used for communication between client and server. I'll also talk about the implementation of my solution, basic usage of the app and the testing that was done to ensure it works as intended.

Project was developed in C# with .NET9+ compatibility.
## Compilation
Executable file `ipk25chat-client` is created with the `make` command by provided `Makefile`. To run the executable file type `./ipk25chat-client` with different parameters depending on the required behavior.

## Usage
The app can be executed with several parameters, some of them are mandatory, others depend on the user, they can choose them or the default value will be set:

- `./ipkchat25-client -t (tcp|udp) -s (IP|hostname) -p (PORT) -d (TIMEOUT) -r (RETRANSMISSIONS)` 

Mandatory parameters:
- `-t` - Defines the transport protocol for the communication, either TCP or UDP
- `-s` - The address of the IPK25CHAT server, can be either an ip address e.g. *172.0.0.1* or a domain name e.g. *vut.cz*

Optional parameters:
- `-p` - Sets the server port that the client is gonna connect to, default value is *4567*
- `-d` - Number of miliseconds for the server to confirm the message, before retransmitting the message, default value is *250*
- `-r` - Number of retransmissons the client will make
- `-h` - Prints out help function to show usage

## Theory
TCP and UDP represent the two protocols of the Transport Layer used for communication over internet in the Open Systems Interconnection (OSI) model. Both of them have different use-cases, features, cons and pros.
### TCP
TCP - Transmission Control Protocol, provides reliable connection, which needs to be established before any communication, with threeway handshake. It guarantees that every message, that is send with given protocol will be delivered in an order, that it was sent in, even though it doesn't have to be sent in one piece. If a packet is lost, it detects it and can retransmit it, some delay will occurr tho. TCP is used in a broad scale, WWW uses TCP for ensuring reliable transfer of data between server and browser, FTP and Email use them for transfering messages and files. Often, cause of these usages, TCP is used as a text-based protocol. 
### UDP
UDP - User Datagram Protocol, an alternative to the TCP is the opposite. It is an unreliable, doesn't check if the message was fully received or not, doesnt't even ensure the contents will arrive in the same order, everything needs to be checked "manually". It is connectionless, meaning no connection needs to be established before transfering data. UDP is used in real time apps, like streaming services or gaming, but not only, DNS or network monitoring apps use UDP.
## Implementation
Implementation logic behind both variants is similiar, but there are several differences, depending on the specific needs of the given transport protocol.

Client's implementation is based on two classes from the `System.Net.Sockets` which are `UDPClient` and `TCPClient`, which provide access to functions for handling the connection, no need of socket compilation is needed. It uses asynchronous tasks, so more than one operation can be running at given time. 

The app is split into several files for better transparency, structure and logical separation.

`ArgParser.cs` parses the given arguments from command line and stores them for future use.

`ClientStates.cs` has all the possible states and message types, that can occur stored inside as enums

`TransportClient.cs` is an interface for both protocol classes, contains the basic definition of functions, that will ensure the user inputs or server messages

`TCPClient.cs` and `UDPClient.cs` implement the client, contains most of the logic behind processing the user inputs, sending messages to server, ensuring correct transitioning to states 

`Messages.cs` this file is mainly for constructing messages to be send or received, both from server and client. It also ensure the right format and validation of message content, user and display names of client, secrets

### Main logic
Firstly the arguments are parsed and stored in a class `ChatOptions.cs`. Depending on the communication protocol, either UDP or TCP socket is constructed with the right arguments. Afterwards connection with the server is established and jumps into loop, where it starts listening for both server messages and user inputs. When a message occurs, another task processes it, depending on the current state. If an user input is registered, checks if it is a valid command and processes it accordingly or prints a message. If at any given time, either by completing the tasks, losing connection or exiting, properly disconnects from the server and exits.

## Testing
Testing was done using several methods. For testing TCP connection, command `nc` was used, for creating a local server and sending messages and commands, back and forth. Wireshark was also used for checking the communication over network, as well as public reference server on discord, that was given to us, as means to test our communication.
Lastly few students tests were used, they were used mainly for my peace of mind, so I can be sure the outputs are in the right format, they were not done by me, and are available on github [https://github.com/Vlad6422/VUT_IPK_CLIENT_TESTS].

Here are few test examples that I've run on local pseudo-server with command `nc -C -l 127.0.0.1 4567` and then connecting to it from terminal with `./ipk25chat-client -t tcp -p 4567 -s 127.0.0.1`. These are only few of all test cases, that I've concluded to test the functionality of my TCP implementation.

### First test
Client input in terminal:
<pre>./ipk25chat-client -t tcp -p 4567 -s 127.0.0.1
Connected to server. Type /help for available commands.
/auth a b c
Server message: REPLY OK IS ano
Action Success: ano
/rename kvetinka
test test
/exit
Exiting...
Server message: BYE</pre>

Server output in terminal using `nc -C -l 127.0.0.1 4567`:
<pre>AUTH a AS c USING b
REPLY OK IS ano
MSG FROM kvetinka IS test test
BYE FROM kvetinka
BYE
BYE FROM kvetinka</pre>

### Second test
Client input in terminal:
<pre>./ipk25chat-client -t tcp -p 4567 -s 127.0.0.1
Connected to server. Type /help for available commands.
ahoj
ERROR: unknown command, use /help
/auth a b c
/auth y o p
/authh y o k
ERROR: unknown command, /help to see needed parameters
/joinn j
ERROR: unknown command, /help to see needed parameters
/renamee f
ERROR: unknown command, /help to see needed parameters
/helppp
ERROR: unknown command, /help to see needed parameters
/help
Commands:
/help - prints out help
/auth {Username} {Secret} {DisplayName} - Sends AUTH message with the data provided
/join {ChannelID} - Sends JOIN message with channel name
/rename {DisplayName} - Locally changes the display name
/exit - Exits
/exit
Exiting...</pre>

Server output in terminal using `nc -C -l 127.0.0.1 4567`:
<pre>AUTH a AS c USING b
AUTH y AS p USING o
BYE FROM p</pre>
### Third test
Client input in terminal:
<pre>./ipk25chat-client -t tcp -p 4567 -s 127.0.0.1
Connected to server. Type /help for available commands.
/aUtH jedna dva tri
ERROR: unknown command, /help to see needed parameters
/auth jedna dva tri
Server message: rEplY Ok IS Vitej
Action Success: Vitej
Hiiii
/rEnAMe deset
ERROR: unknown command, /help to see needed parameters
/rename pekny
Ahoj
Server message: ByE
Exiting...
ERROR: Client not connected</pre>

Server output in terminal using `nc -C -l 127.0.0.1 4567`:
<pre>AUTH jedna AS tri USING dva
rEplY Ok IS Vitej
MSG FROM tri IS Hiiii
MSG FROM pekny IS Ahoj
ByE</pre>

## Bibliography
[1] Ed, W. E. (2022, August 1). RFC 9293: Transmission Control Protocol (TCP). IETF Datatracker. https://datatracker.ietf.org/doc/html/rfc9293

[2] Nesfit. (n.d.). IPK-Projects. FIT - VUT Brno - Git. https://git.fit.vutbr.cz/NESFIT/IPK-Projects/src/branch/master/Project_2

[3] RFC 768: User Datagram Protocol. (n.d.). IETF Datatracker. https://datatracker.ietf.org/doc/html/rfc768

[4] MALASHCHUK Vladyslav, Tomáš HOBZA, et al. VUT_IPK_CLIENT_TESTS [online]. GitHub, 2025 [cit. 2025-04-16]. Available at: https://github.com/Vlad6422/VUT_IPK_CLIENT_TESTS

[5] Karelz. (n.d.). Socket třída (System.Net.Sockets). Microsoft Learn. https://learn.microsoft.com/cs-cz/dotnet/api/system.net.sockets.socket?view=net-8.0

[6] Wikipedia contributors. (2025, March 21). User Datagram Protocol. Wikipedia. https://en.wikipedia.org/wiki/User_Datagram_Protocol
 
