using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using Spectre.Console;
using System.Globalization;
using System.Security.Cryptography;
using YamlDotNet.Serialization.NamingConventions;
using System.Reflection;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.ComponentModel.Design;
using System.Net.WebSockets;
using System.Buffers.Text;

namespace Constellations;
/// <summary>
/// A named tcp server/client that can use encrypted communication to send and recieve messages.
/// Constellations also handle websockets. The JavaScript client library can be found on npm @devmachinist/constellations.
/// </summary>
public partial class Constellation
{
    public string Name { get; set; } = "Constellation";
    public X509Certificate X509Cert { get; set; }
    private SemaphoreSlim ShutdownSignal = new SemaphoreSlim(0, 1);
    public Constellation SetName(string name)
    {
        Name = name;
        return this;
    }
    private bool IsServer { get; set; } = false;
    public List<string> SafeOrigins = new List<string>();
    /// <summary>
    /// A program cluster that runs all of the ProcessStartInfos in the Processes list.
    /// </summary>
    private Cluster Orchestration { get; set; } = new Cluster();
    private List<IMessageHandler> messageHandlers = new List<IMessageHandler>();
    private List<IMessageSenderHandler> messageSenderHandlers = new List<IMessageSenderHandler>();
    private List<IServerMessageHandler> serverMessageHandlers = new List<IServerMessageHandler>();
    private List<IClientHandler> clientHandlers = new List<IClientHandler>();
    private List<IFailedMessageHandler> failedMessageHandlers = new List<IFailedMessageHandler>();
    public List<ProcessStartInfo> Processes { get; set; } = new List<ProcessStartInfo>();
    /// <summary>
    /// Your endpoints must be known to route a message to them.
    /// However, if you successfully sent a message from a client,
    /// the server instance will add it's name to list of incomingClients.
    /// This will allow you to route to them, from one client through the server to another client.
    /// </summary>
    public Dictionary<string, Endpoint> Endpoints { get; set; } = new Dictionary<string, Endpoint>();
    public delegate void MessageReceived(Message message);
    public delegate void FinishedBuilding();
    public delegate void MessageSent(Message message);
    public delegate void ConstellationConnected(NamedClient namedClient);
    public delegate void ConstellationDisconnected(NamedClient namedClient);
    public delegate void ServerSentMessage(Message message);
    public delegate void ServerBroadcastSent(Message message);
    public delegate void MessageFailure(string reason, Message message);
    public delegate void ClientAcceptionFailed(string info);
    public event ClientAcceptionFailed AcceptClientFailed;
    public event MessageFailure SendMessageFailed;
    /// <summary>
    /// This event is invoked when you recieve a message from another constellation.
    /// </summary>
    public event MessageReceived NewMessage;
    /// <summary>
    /// This event is invoked when you send a direct message from one constellation to another.
    /// </summary>
    public event MessageSent SentMessage;
    private event FinishedBuilding Built;
    public Endpoint Listener {get;set;}
    /// <summary>
    /// This is a list of all clients that sent a message to the server.
    /// </summary>
    public ConcurrentBag<NamedClient> incomingClients = new ConcurrentBag<NamedClient>();
    /// <summary>
    /// This is a list of all clients that the server is connected to as a client.
    /// </summary>
    public ConcurrentBag<NamedClient> outgoingClients = new ConcurrentBag<NamedClient>();
    /// <summary>
    /// This event is invoked when a NamedClient has connected to your server instance.
    /// </summary>
    public event ConstellationConnected NewConnection;
    /// <summary>
    /// This event is invoked when a NamedClient has disconnected.
    /// </summary>
    public event ConstellationDisconnected ConnectionClosed;
    /// <summary>
    /// This event fires when the server was used to route a message to one of it's connected clients.
    /// Your handler should receive a type of Constellation.Message as it's parameter.
    /// </summary>
    public event ServerSentMessage SentServerMessage;
    /// <summary>
    /// To create a broadcast Constellation.Message you must set Message.Broadcast to true. When sent to a server it will broadcast it to all connected clients.
    /// 
    /// Broadcasts are enabled by default and can be disabled by changing the value of {constellation server instance}.CanBroadcast = false;
    /// </summary>
    private bool CanBroadcast { get; set; } = true;
    /// <summary>
    /// Sets up a task that blocks the main thread from closing when using Run().
    /// </summary>
    private bool KeepAlive { get; set; }
    private string ConfigFile { get; set; } = "Constellation.yml";
    public object Route { get; private set; }

    /// <summary>
    /// An event that tells you a broadcast message was received by the server and sent to all connected clients.
    /// Your event handler should receive a type of Constellation.Message.
    /// </summary>
    public event ServerBroadcastSent SentServerBroadcast;
    public Constellation()
    {
    }
    public Constellation(string name)
    {
        Name = name;
    }
    public Constellation(Constellation constellation)
    {
        this.Name = constellation.Name;
        this.Orchestration = constellation.Orchestration;
        this.Processes = constellation.Processes;
        this.CanBroadcast = constellation.CanBroadcast;
        this.ConfigFile = constellation.ConfigFile;
        this.EncryptionKey = constellation.EncryptionKey;
        this.Endpoints = constellation.Endpoints;
        this.Listener = constellation.Listener;
        this.IsServer = constellation.IsServer;
        this.KeepAlive = constellation.KeepAlive;
    }
    /// <summary>
    /// Sets the listener for the server instance if AsServer() has been called.
    /// </summary>
    /// <param name="name">The name of the container. It can be null but must have the ipv4 parameter</param>
    /// <param name="ipv4">The ip address. It can be null but must have the name parameter</param>
    /// <param name="port">The port to listen on. It cannot be null</param>
    /// <returns></returns>
    public Constellation ListenOn(string? name, string? ipv4, int port)
    {
        Listener = new Endpoint()
        {
            Address = ipv4,
            Port = port,
            Name = name
        };
        return this;
    }
    public Constellation ListenOn(string? prefix)
    {
        IPEndPoint ipep = CreateIPEndPoint(prefix);
        Listener = new Endpoint()
        {
            Address = ipep.Address.ToString(),
            Port = ipep.Port,
        };
        return this;
    }
    /// <summary>
    /// Sets the listener for the server instance if AsServer() has been called.
    /// </summary>
    /// <param name="name">The name of the server. It cannot be null</param>
    /// <param name="ipv4">The ip address. It can be null but must have the name parameter</param>
    /// <param name="port">The port to listen on. It cannot be null</param>
    /// <returns></returns>
    public Constellation AddEnpoint(string name, string? ipv4, int port)
    {
        Endpoints.Add(name, new Endpoint()
        {
            Address = ipv4,
            Port = port,
            Name = name
        });
        return this;
    }
    /// <summary>
    /// This is a constructor that recieves a yaml config file name that represents the constellation object instance.
    /// The path is relative to the Environment.CurrentDirectory.
    /// </summary>
    /// <param name="yamlFile">A path string representing the location of the yaml configuration file</param>
    public Constellation UseYamlConfig(string yamlFile)
    {
        ConfigFile = yamlFile;
        var constellation = Parser.Parse(yamlFile);
        this.Listener = constellation.Listener;
        this.Name = constellation.Name;
        this.Processes = constellation.Processes;
        this.SafeOrigins = constellation.SafeOrigins;
        this.CanBroadcast = constellation.CanBroadcast;
        this.Endpoints = constellation.Endpoints;
        return this;
    }
    /// <summary>
    /// Set the constellation to run continously without allowing any new hosts.
    /// Only use this if you aren't running the constellation without an application host.
    /// It will keep the application alive in a stand alone manner.
    /// </summary>
    /// <returns>The current instance of the constellation</returns>
    public Constellation WillStandAlone()
    {
        this.KeepAlive = true;
        Built += RunAsStandAlone;
        return this;
    }
    /// <summary>
    /// Constellation will be a Server and a Client
    /// </summary>
    /// <returns></returns>
    public Constellation AsServer()
    {
        this.IsServer = true;
        return this;
    }
    /// <summary>
    /// This method will cancel any broadcast sent to the constellation.
    /// </summary>
    /// <returns>The current instance of the constellation</returns>
    public Constellation NoBroadcasting()
    {
        this.CanBroadcast = false;
        return this;
    }
    /// <summary>
    /// Allow broadcasting by adding setting the message.Broadcast to true.
    /// </summary>
    /// <returns>The current instance of the constellation</returns>
    public Constellation AllowBroadcasting()
    {
        this.CanBroadcast = true;
        return this;
    }
    /// <summary>
    /// Set the allowed origins individually or allow all origins by adding "*"
    /// </summary>
    /// <param name="origins">A string containing all safe origins allowed by the constellation separated by "," </param>
    /// <returns>The current instance of the constellation</returns>
    public Constellation AllowOrigins(string origins)
    {
        string[] strings = origins.Split(',');
        foreach (string s in strings)
        {
            SafeOrigins.Add(s.Trim());
        }
        return this;
    }
    /// <summary>
    /// Adds a List<ProcessStartInfo> to the current Processes. These will be run on StartOrchestration() or when using Run().
    /// </summary>
    /// <param name="processes">List<ProcessStartInfo></param>
    /// <returns>The current instance of the constellation</returns>
    public Constellation AddProcesses(List<ProcessStartInfo> processes)
    {
        Processes.AddRange(processes);
        return this;
    }
    /// <summary>
    /// Pass in an Type[] of IMessageSenderHandler derivatives to intercept all outgoing messages
    /// </summary>
    /// <param name="types">A Type Array of IMessageSenderHandler</param>
    /// <returns>The current instance of the constellation after the handlers have been added</returns>
    public Constellation AddMessageSenderHandlers(Type[] types)
    {
        foreach(var type in types)
        {
            IMessageSenderHandler handlerInstance = (IMessageSenderHandler)Activator.CreateInstance(type);
            if(handlerInstance != null)
            {
                messageSenderHandlers.Add(handlerInstance);
            }
        }
        return this;
    }
    /// <summary>
    /// Pass in an Type[] of IMessageHandler derivatives to intercept all incoming messages
    /// </summary>
    /// <param name="types"></param>
    /// <returns>The current instance of the constellation after the handlers have been added</returns>
    public Constellation AddMessageHandlers(Type[] types)
    {
        foreach(var type in types)
        {
            IMessageHandler handlerInstance = (IMessageHandler)Activator.CreateInstance(type);
            if(handlerInstance != null)
            {
                messageHandlers.Add(handlerInstance);
            }
        }
        return this;
    }
    /// <summary>
    /// Pass in an Type[] of IFailedMessageHandler derivatives to intercept all messages that failed sending
    /// </summary>
    /// <param name="types"></param>
    /// <returns>The current instance of the constellation after the handlers have been added</returns>
    public Constellation AddFailedMessageHandlers(Type[] types)
    {
        foreach(var type in types)
        {
            IFailedMessageHandler handlerInstance = (IFailedMessageHandler)Activator.CreateInstance(type);
            if(handlerInstance != null)
            {
                failedMessageHandlers.Add(handlerInstance);
            }
        }
        return this;
    }
    /// <summary>
    /// Pass in an Type[] of IServerMessageHandler derivatives to intercept outgoing
    /// Server Messages that are passing through the server on their way to the recipient.
    /// </summary>
    /// <param name="types"></param>
    /// <returns>The current instance of the constellation after the handlers have been added</returns>
    public Constellation AddServerMessageHandlers(Type[] types)
    {
        foreach(var type in types)
        {
            IServerMessageHandler handlerInstance = (IServerMessageHandler)Activator.CreateInstance(type);
            if(handlerInstance != null)
            {
                serverMessageHandlers.Add(handlerInstance);
            }
        }
        return this;
    }
    /// <summary>
    /// Add custom client handlers to handle clients. Must inherit from IClientHandler
    /// </summary>
    /// <param name="types">A type array of IClientHandler derivatives</param>
    /// <returns>The current instance of the constellation after the handlers have been added</returns>
    public Constellation AddClientHandlers(Type[] types)
    {
        foreach (var type in types)
        {
            IClientHandler clientHandler = (IClientHandler)Activator.CreateInstance(type);
            if (clientHandler != null)
            {
                clientHandlers.Add(clientHandler);
            }
        }
        return this;
    }

    /// <summary>
    /// Builds the constellation with the configuration set 
    /// and starts it if it recieves messages as a server.
    /// </summary>
    public Constellation Run()
    {
        Init();
        StartOrchestration();
        if (this.Listener != null && IsServer == true)
        {
            _ = StartReceivingMessages();
        }
        if (this.KeepAlive)
        {
            Built?.Invoke();    
        }
        return this;
    }
    private void RunAsStandAlone()
    {
        ShutdownSignal.Wait();
    }
    public void Shutdown()
    {
        ShutdownSignal.Release();
    }
    /// <summary>
    /// Add all processes from the list Processes to the orchestration. Must be called before calling constellation.start().
    /// </summary>
    private void Init()
    {
            if (Processes != null)
            {
                foreach (var p in Processes)
                {
                    var x = new Process();
                    x.StartInfo = p;
                    Orchestration.Processes.Add(x);
                }
            }
    }
    /// <summary>
    /// Start all processes in your Processes list. Remember, you can add these using a yaml file or just creating a ProcessStartInfo instance and adding it to the list.
    /// </summary>
    private void StartOrchestration()
    {
        Orchestration.Start();
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }
    
    private void OnProcessExit(object? sender, EventArgs e)
    {
        Orchestration.Stop();
        System.Threading.Thread.Sleep(1000);
    }
    /// <summary>
    /// Stop all processes running in the orchestration.
    /// </summary>
    private void StopOrchestration()
    {
        Orchestration.Stop();
    }
    /// <summary>
    /// Set the X509Certificate for the incoming wss:// connections;
    /// </summary>
    /// <param name="cert"></param>
    /// <returns></returns>
    public Constellation SetX509Cert(X509Certificate cert)
    {
        X509Cert = cert;
        return this;
    }
    /// <summary>
    /// Send a message to another constellation. Your keys must match in order to decipher the encrypted message on the other end.
    /// </summary>
    /// <param name="recipient">The constellation Name of the recipient constellation or a string using a comma ',' to separate each recipient</param>
    /// <param name="message"> The text you want to send as the Message.payload</param>
    /// <param name="server"> The constellation server you want to route through. Can be set to null for a direct message.</param>
    /// <param name="name"> The name of the sender constellation. This is typically used to send a server message to a client that has already connected</param>
    public async Task SendMessage(string recipient, string message, string? server = null, string? name = null, bool broadcast = false, string? senderId = null, string route = "")
    {
        await Task.Run(async () =>
        {
            var thisMessage = new Message()
            {
                SenderId = senderId,
                Sender = name ?? Name,
                Recipient = recipient,
                Payload = message,
                Server = server,
                Broadcast = broadcast,
                Timestamp = DateTimeOffset.Now
            };
            foreach (var handler in messageSenderHandlers)
            {
                thisMessage = handler.Handle(thisMessage);
            }
            var jsonString = (senderId != null)? $"GET /{route}?id={senderId} HTTP/1.1\r\nConstellation: true\r\n\r\n":""+ JsonSerializer.Serialize(thisMessage) + "::ENDOFMESSAGE::";
            var wsString = JsonSerializer.Serialize(thisMessage) + "::ENDOFMESSAGE::";
            if (server == null)
            {
                server = recipient;
            }
            var endpoint = GetEndpoint(server);
            var clients = new List<NamedClient>();
            clients.AddRange(outgoingClients.Where(p => p.Name == server));
            clients.AddRange(incomingClients.Where(p => p.Name == server));

            if (endpoint == null)
            {
                NetworkStream stream;
                foreach (var client in clients)
                {
                    if (client.TcpClient.Connected)
                    {
                        stream = client.TcpClient.GetStream();
                        client.semaphore.Wait();
                        client.IsStreaming = true;
                        byte[] data = Encoding.UTF8.GetBytes(Encrypt(jsonString, thisMessage.SenderId));
                        if (client.IsWebsocket)
                        {
                            data = Encoding.UTF8.GetBytes(Encrypt(wsString, thisMessage.SenderId));
                            try
                            {
                                if(client.SslStream is not null)
                                {
                                    await client.WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                                else
                                {
                                    await client.WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.ToString());
                                
                                SendMessageFailed?.Invoke(ex.ToString(),thisMessage);
                            }
                        }
                        else
                        {
                            stream.Write(data, 0, data.Length);
                        }
                        client.IsStreaming = false;
                        client.semaphore.Release();
                    }
                    else
                    {
                        client.semaphore.Release();
                        var reason = "Client not connected";
                        thisMessage = HandleFailedMessage(reason, thisMessage);
                        SendMessageFailed?.Invoke(reason, thisMessage);
                        RemoveClient(client);
                    }
                }
                if (clients.Count() <= 0)
                {
                    var reasonForFailure = $"{server} isn't connected";
                    foreach (var handler in failedMessageHandlers)
                    {
                        thisMessage = handler.Handle(reasonForFailure, thisMessage);
                    }
                    SendMessageFailed?.Invoke(reasonForFailure, thisMessage);
                }
                else
                {
                    SentMessage?.Invoke(thisMessage);
                }
            }
            else
            {
                NetworkStream stream;
                clients.ForEach(async (client) =>
                    {
                        if (client.TcpClient.Connected)
                        {
                            stream = client.TcpClient.GetStream();
                            client.semaphore.Wait();
                            client.IsStreaming = true;
                            byte[] data = Encoding.UTF8.GetBytes(Encrypt(jsonString, thisMessage.SenderId));
                            if (client.IsWebsocket)
                            {
                                data = EncodeWebSocketFrame(Encrypt(wsString, thisMessage.SenderId));
                                if(client.SslStream is not null)
                                {
                                    client.SslStream.Write(data, 0, data.Length);
                                }
                                else
                                {
                                    stream.Write(data, 0, data.Length);
                                }
                            }
                            else
                            {
                                stream.Write(data, 0, data.Length);
                            }
                            client.IsStreaming = false;
                            client.semaphore.Release();
                        }
                        else
                        {
                            client.semaphore.Release();
                            var reasonForFailure = "Client not connected";
                            thisMessage = HandleFailedMessage(reasonForFailure, thisMessage);
                            SendMessageFailed?.Invoke(reasonForFailure, thisMessage);
                            RemoveClient(client);
                        }
                    });
                if (clients.Count() <= 0)
                {
                    NamedClient? client = null;
                    try
                    {

                        client = new NamedClient(new TcpClient(endpoint.Address ?? server, endpoint.Port))
                        {
                            Name = server
                        };
                        outgoingClients.Add(client);
                        client.semaphore.Wait();
                        _ = HandleClientAsync(client);
                        client.IsStreaming = true;
                        client.MessageQueue.Enqueue(jsonString);
                        stream = client.TcpClient.GetStream();
                        byte[] data = Encoding.UTF8.GetBytes(Encrypt(jsonString, thisMessage.SenderId));
                        stream.Write(data, 0, data.Length);
                        client.IsStreaming = false;
                        while (!client.MessageQueue.TryDequeue(out string result)) { }
                        client.semaphore.Release();

                        SentMessage?.Invoke(thisMessage);
                    }
                    catch (Exception ex)
                    {
                        if (client != null)
                        {
                            client.semaphore.Release();
                        }
                        SendMessageFailed?.Invoke(ex.ToString(), thisMessage);
                    }
                }
            }
        });
    }
    public void RemoveClient(NamedClient client)
    {
        // Create a new ConcurrentBag with clients that are not the one to remove
        incomingClients = new ConcurrentBag<NamedClient>(incomingClients.Where(c => c != client));
        outgoingClients = new ConcurrentBag<NamedClient>(outgoingClients.Where(c => c != client));
    }

    private Message HandleFailedMessage(string reason, Message thisMessage)
    {
        failedMessageHandlers.ForEach(handler =>
        {
            thisMessage = handler.Handle(reason, thisMessage);
        });
        return thisMessage;
    }

    public async Task SendMessage(Message message, string route = "")
    {
        _ = SendMessage(message.Recipient, message.Payload, message.Server, message.Sender, message.Broadcast, message.SenderId, route);
    }
    private Endpoint? GetEndpoint(string name)
    {
        try
        {
            var endpoint = GetEndpoints()[name];
            return endpoint;
        }
        catch (KeyNotFoundException ex)
        {
            return null;
        }
    }
    private IEnumerable<IPAddress> GetLocalAddresses()
    {
        return Dns.GetHostAddresses(Dns.GetHostName())
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
    }
    private Dictionary<string, Endpoint> GetEndpoints()
    {
        return Endpoints;
    }
    private async Task StartReceivingMessages()
    {
        var point = CreateIPEndPoint(Listener.ToString());
        Console.WriteLine(point.ToString());
        var listener = new TcpListener(point);
        listener.Start();

        await AcceptClientsAsync(listener);
    }

    private async Task AcceptClientsAsync(TcpListener listener)
    {
        if (!IsListenerAlive(listener))
        {
            listener.Start();
        }

        try
        {
                var client = await listener.AcceptTcpClientAsync();
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var namedClient = new NamedClient(client);
                NewConnection?.Invoke(namedClient);
                incomingClients.Add(namedClient);
                _ = HandleClientAsync(namedClient); // Fire-and-forget to handle the client
            await AcceptClientsAsync(listener);
        }
        catch (Exception ex)
        {
            // Log the error and restart if necessary
            
            Console.WriteLine($"Error accepting client: {ex.ToString()}");
            listener.Start(); // Restart the listener after a failure

            // Restart the process
            await AcceptClientsAsync(listener);
        }
    }
    public bool IsListenerAlive(TcpListener listener)
    {
        try
        {
            // Try to check if it's still listening by performing a basic operation
            if (listener.Server != null && listener.Server.IsBound)
            {
                return true;
            }
        }
        catch (SocketException)
        {
            // If an exception occurs, it indicates the listener is no longer valid
            return false;
        }

        return false;
    }
    private static IPEndPoint CreateIPEndPoint(string endPoint)
    {
        endPoint = endPoint.Replace("js://", "");
        if (string.IsNullOrWhiteSpace(endPoint))
        {
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(endPoint));
        }

        string[] ep = endPoint.Split(':');
        if (ep.Length < 2)
        {
            throw new FormatException("Invalid endpoint format");
        }

        string host = string.Join(":", ep, 0, ep.Length - 1); // Handle cases like IPv6 with colons
        string portStr = ep[^1]; // Get the last part as the port

        // Resolve the hostname or IP address
        IPAddress ip;
        if (!IPAddress.TryParse(host, out ip))
        {
            // If it's not a valid IP, attempt DNS resolution
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
            {
                throw new FormatException("Unable to resolve the hostname to an IP address");
            }

            ip = addresses[0]; // Use the first resolved address
        }

        // Parse the port
        if (!int.TryParse(portStr, NumberStyles.None, NumberFormatInfo.CurrentInfo, out int port))
        {
            throw new FormatException("Invalid port");
        }

        return new IPEndPoint(ip, port);
    }
    private async Task HandleClientAsync(NamedClient namedClient)
    {
        NetworkStream networkStream = namedClient.TcpClient.GetStream();
        SslStream? sslStream = null;
        var protocol = "";
        string? id = null;

        // Read initial data from the client
        byte[] buffer = new byte[1024];
        int received = 0;
        try
        {

            received = await networkStream.ReadAsync(buffer, 0, buffer.Length);
            if (buffer[0] != 0x16)
            {
                throw new Exception("Not wss://");
            }
            sslStream = new SslStream(networkStream, true);
            await sslStream.AuthenticateAsServerAsync(X509Cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
            buffer = new byte[1024];
            namedClient.SslStream = sslStream;
            received = await sslStream.ReadAsync(buffer, 0, buffer.Length);
            protocol = "wss://";
        }
        catch (Exception ex)
        {
            protocol = "ws://";
        }

        string request = Encoding.UTF8.GetString(buffer, 0, received);
        Debug.WriteLine(request);
        if (request.StartsWith("GET /?id="))
        {
            id = request.Split(" ")[1].Replace("/?id=", "");
            namedClient.EncryptionId = id;
        }
        string? origin;
        if (request.Contains("Upgrade: websocket"))
        {
            namedClient.IsWebsocket = true;
            origin = GetWebSocketOrigin(request);

            if (SafeOrigins.Contains(origin) || SafeOrigins.Contains("*"))
            {
                // Handle WebSocket connection
                string key = GetWebSocketKey(request);
                string acceptKey = ComputeWebSocketAcceptKey(key);

                string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                                  "Connection: Upgrade\r\n" +
                                  "Upgrade: websocket\r\n" +
                                  "Sec-WebSocket-Accept: " + acceptKey + "\r\n\r\n";

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                WebSocket? ws = null;
                if (protocol == "wss://")
                {
                    await sslStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    ws = WebSocket.CreateFromStream(sslStream, true, null, TimeSpan.FromDays(1));
                }
                else
                {
                    await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    ws = WebSocket.CreateFromStream(networkStream, true, null, TimeSpan.FromDays(1));
                }
                namedClient.WebSocket = ws;

                var remainder = "";
                var cantDecode = true;
                int length = 0;
                var countLength = true;
                string partialDecodedChunk = "";

                // Create a buffer to accumulate received data
                byte[] webbuffer = new byte[8192]; // Chunk size for reading
                await checkclient();
                async Task checkclient()
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        cantDecode = false;
                        int retries = 0;
                        await checkmessage();
                        async Task checkmessage()
                        {
                            MemoryStream memoryStream = new MemoryStream();

                            // Read WebSocket frames in chunks
                            await read();
                            async Task read()
                            {
                                int bytesRead = 0;
                                //if (protocol == "wss://")
                                //{
                                //    bytesRead = await sslStream.ReadAsync(webbuffer, 0, webbuffer.Length, new CancellationToken());
                                //}
                                //else
                                //{
                                //    bytesRead = await networkStream.ReadAsync(webbuffer, new CancellationToken());
                                //}
                                var result = await ws.ReceiveAsync(new ArraySegment<byte>(webbuffer), CancellationToken.None);
                                bytesRead = result.Count;
                                // Accumulate received bytes into the memory stream
                                if (bytesRead > 0)
                                {
                                    await memoryStream.WriteAsync(webbuffer, 0, bytesRead);
                                }
                                if (bytesRead == 0)
                                {
                                }
                                else if (bytesRead < webbuffer.Length)
                                {
                                }
                                else
                                {
                                    await read();
                                }
                            }
                            // Try to decode and decrypt the accumulated data
                            string decodedChunk = "not decoded";
                            try
                            {
                                if (memoryStream.Length > 0)
                                {
                                    byte[] accumulatedBytes = memoryStream.ToArray();
                                    //string decodedFrame = DecodeWebSocketFrame(accumulatedBytes);
                                    string decodedFrame = Encoding.UTF8.GetString(accumulatedBytes);
                                    if (countLength)
                                    {
                                        length = Convert.ToInt32(decodedFrame.Split("]]]")[0].Replace("[[[", ""));
                                        decodedFrame = Regex.Replace(decodedFrame, "\\[\\[\\[(\\d+)\\]\\]\\]", "");
                                        countLength = false;
                                    }
                                    partialDecodedChunk += decodedFrame;
                                    decodedFrame = partialDecodedChunk;
                                    var check = decodedFrame.Length;

                                    if (length == decodedFrame.Length)
                                    {
                                        countLength = true;
                                        length = 0;
                                        // Regular expression to keep only valid Base64 characters
                                        string pattern = @"[^A-Za-z0-9+/=]";

                                        // Replace all invalid characters with an empty string
                                        decodedFrame = Regex.Replace(decodedFrame, pattern, string.Empty).Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\0","");
                                        decodedChunk = Decrypt(decodedFrame, id ?? null);
                                        cantDecode = false;

                                        if (decodedChunk.Contains("::ENDOFMESSAGE::"))
                                        {
                                            // Split messages if multiple are present
                                            var messages = decodedChunk.Split("::ENDOFMESSAGE::");
                                            for (int i = 0; i < messages.Length - 1; i++)
                                            {
                                                string message = remainder + messages[i];
                                                if (message.Length > 0)
                                                {
                                                    HandleWebsocketMessage(namedClient, message); // Process each message individually
                                                    partialDecodedChunk = "";
                                                }
                                            }

                                        }
                                    }
                                    else
                                    {
                                        memoryStream.Close();
                                        memoryStream = new MemoryStream();
                                    }
                                }
                                else
                                {
                                    cantDecode = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to decode or decrypt message: {ex.ToString()}");
                                cantDecode = false;
                            }
                            if (cantDecode)
                            {
                                memoryStream.Close();
                                await checkmessage();
                            }
                        }
                        if(namedClient.TcpClient.Connected) await checkclient();
                        
                    }
                    else
                    {
                        // Clean up resources after connection closes
                        if (id != null)
                        {
                            Console.WriteLine("Connection was closed");
                        }
                        ConnectionClosed?.Invoke(namedClient);
                    }
                }
            }
            else
            {
                namedClient.TcpClient.Close();
                ConnectionClosed?.Invoke(namedClient);
            }
        }
        else
        {
            // Raw TCP handling
            origin = GetDomainNameFromIp((namedClient.TcpClient.Client.RemoteEndPoint as IPEndPoint).Address.ToString());
            if (SafeOrigins.Contains(origin) || SafeOrigins.Contains("*"))
            {
                bool streamStarted = false;
                while (true)
                {
                    byte[] frame = new byte[8024];
                    StringBuilder messageBuilder = new StringBuilder();
                    int bytesRead;
                    int offset = 0;
                    int remaining = frame.Length;
                    var remainder = "";
                    var endOfMessage = false;

                    bytesRead = frame.Length;
                    if (streamStarted)
                    {
                    }
                    else
                    {
                    }
                    while (true)
                    {
                        if (streamStarted)
                        {
                            offset = 0;
                            remaining = frame.Length;
                            bytesRead = await networkStream.ReadAsync(frame, offset, remaining, new CancellationToken());
                        }
                        else
                        {
                        }
                        if (bytesRead == 0)
                        {
                            break;
                        }
                        var queries = new List<string>();
                        var rawChunk = Encoding.UTF8.GetString(frame, 0, bytesRead).Trim('\0');
                        string chunk = rawChunk;
                        if (rawChunk.StartsWith("GET "))
                        {
                            var queryChunk = rawChunk.Split("\r\n").ToList()[0];
                            var httpProto = queryChunk.Split(" ")[2];
                            var headerChunk = rawChunk.Take(new Range(rawChunk.IndexOf("\r\n"), rawChunk.IndexOf("\r\n\r\n"))).ToString().Split("\r\n");
                            var headers = headerChunk.Take(new Range(1, headerChunk.Length - 1));
                            var rawQueries = queryChunk.Split(" ")[1].Split("?").Take(new Range(1, queryChunk.Split(" ")[1].Split("?").Length));
                            var path = rawQueries.FirstOrDefault();
                            foreach (var query in rawQueries.Take(new Range(1, rawQueries.Count())))
                            {
                                if (query.StartsWith("id="))
                                {
                                    id = query.Split("id=").Last();
                                }
                            }
                            rawChunk = rawChunk.Split("\r\n\r\n").Last();
                        }

                        chunk = Decrypt(rawChunk, id);
                        if (streamStarted)
                        {
                            if (chunk.Contains("::ENDOFMESSAGE::"))
                            {
                                var splitter = chunk.Split("::ENDOFMESSAGE::");
                                chunk = splitter[0];
                                splitter = splitter.Where(p => !p.Contains(splitter[0])).ToArray();
                                remainder = String.Join("::ENDOFMESSAGE::", splitter);
                                messageBuilder.Append(chunk);
                                bytesRead = 0;
                            }
                            else
                            {
                                messageBuilder.Append(remainder + chunk);
                                remainder = "";
                            }
                        }
                        else
                        {
                            messageBuilder.Append(Decrypt(request).Trim('\0'));
                            streamStarted = true;
                            bytesRead = 0;
                        }

                        if (bytesRead < frame.Length)
                        {
                            break;
                        }
                    }

                    string messageJson = messageBuilder.ToString();
                    if (messageJson != "")
                    {
                        await HandleMessageAsync(namedClient, messageJson);
                    }
                }
            }
        }
    }

    private async void HandleWebsocketMessage(NamedClient namedClient, string messageJson)
    {
        Message? message = null;
        messageJson = messageJson.Replace("::ENDOFMESSAGE::", "");
        try
        {
            message = JsonSerializer.Deserialize<Message>(messageJson);
        }
        catch(Exception ex){
            namedClient.TcpClient.Close();
            RemoveClient(namedClient);
            ConnectionClosed?.Invoke(namedClient);
        }
        if (message != null)
        {
            if (namedClient.Name == "")
            {
                namedClient.Name = message.Sender;
            }
            namedClient = RunClientHandlers(namedClient);
            if (message.Payload.EndsWith("::DISCONNECT::"))
            {
                namedClient.TcpClient.Close();
                ConnectionClosed?.Invoke(namedClient);
                RemoveClient(namedClient);
            }
            else
            {
                if (message.Broadcast)
                {
                    await BroadcastMessageAsync(namedClient, JsonSerializer.Serialize(message));
                }
                else if (message.Recipient != this.Name)
                {
                    message = RunServerMessageHandlers(message);
                    await ServerMessageAsync(namedClient, JsonSerializer.Serialize(message));
                }
                else if (message.Recipient == this.Name)
                {
                    message = RunMessageHandlers(message);
                    NewMessage?.Invoke(message);
                }
            }
        }
    }

    private Message RunMessageHandlers(Message message)
    {
        foreach (var handler in messageHandlers)
        {
            message = handler.Handle(message);
        }
        return message;
    }

    private Message RunServerMessageHandlers(Message message)
    {
        foreach (var handler in serverMessageHandlers)
        {
            message = handler.Handle(message);
        }
        return message;
    }

    private string GetWebSocketKey(string request)
    {
        const string keyHeader = "Sec-WebSocket-Key: ";
        int start = request.IndexOf(keyHeader) + keyHeader.Length;
        int end = request.IndexOf("\r\n", start);
        return request.Substring(start, end - start);
    }

    private string ComputeWebSocketAcceptKey(string key)
    {
        string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        string combined = key + magicString;
        byte[] hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }

    private byte[] EncodeWebSocketFrame(string message)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(message);
        int length = responseBytes.Length;
        byte[] frame;

        if (length <= 125)
        {
            frame = new byte[2 + length];
            frame[0] = 0x81; // Indicates a final text frame
            frame[1] = (byte)length;
            Array.Copy(responseBytes, 0, frame, 2, length);
        }
        else if (length <= 65535)
        {
            frame = new byte[4 + length];
            frame[0] = 0x81; // Indicates a final text frame
            frame[1] = 126;  // Payload length indicator for 2-byte extended length
            frame[2] = (byte)(length >> 8); // Most significant byte
            frame[3] = (byte)(length & 0xFF); // Least significant byte
            Array.Copy(responseBytes, 0, frame, 4, length);
        }
        else
        {
            frame = new byte[10 + length];
            frame[0] = 0x81; // Indicates a final text frame
            frame[1] = 127;  // Payload length indicator for 8-byte extended length
                             // 8 bytes for length (we only use the lower 4 bytes for simplicity)
            frame[2] = 0;
            frame[3] = 0;
            frame[4] = 0;
            frame[5] = 0;
            frame[6] = (byte)(length >> 24);
            frame[7] = (byte)(length >> 16);
            frame[8] = (byte)(length >> 8);
            frame[9] = (byte)(length & 0xFF);
            Array.Copy(responseBytes, 0, frame, 10, length);
        }

        return frame;
    }

    private string DecodeWebSocketFrame(byte[] bytes)
    {
        // Initializing the output text
        string text = "";
        try
        {
            if (bytes.Length > 0)
            {
                // Checking the FIN and MASK bits
                bool fin = (bytes[0] & 0b10000000) != 0;
                bool mask = (bytes[1] & 0b10000000) != 0; // Must be true, "All messages from the client to the server have this bit set"
                int opcode = bytes[0] & 0b00001111; // Expecting 1 - text message

                ulong offset = 2;
                ulong msglen = (ulong)(bytes[1] & 0b01111111); // Length of the message

                // Determine message length based on the length indicator
                if (msglen == 126)
                {
                    // Extended length (2 bytes)
                    if (offset + 2 > (ulong)bytes.Length)
                    {
                        throw new Exception("Frame is too short for extended payload length.");
                    }
                    msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                    offset += 2; // Move past the length bytes
                }
                else if (msglen == 127)
                {
                    // Extended length (8 bytes)
                    if (offset + 8 > (ulong)bytes.Length)
                    {
                        throw new Exception("Frame is too short for extended payload length.");
                    }
                    msglen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                    offset += 8; // Move past the length bytes
                }

                // Ensure the frame has enough bytes for the masking key and the message
                if (offset + 4 > (ulong)bytes.Length)
                {
                    throw new Exception("Frame is too short to include masking key.");
                }

                // The masking key
                byte[] masks = new byte[4]
                {
        bytes[offset],
        bytes[offset + 1],
        bytes[offset + 2],
        bytes[offset + 3]
                };
                offset += 4;

                // Ensure there's enough data for the payload
                if (offset + msglen > (ulong)bytes.Length)
                {
                    throw new Exception("Frame is too short to include complete payload data.");
                }

                // Decode the payload
                byte[] decoded = new byte[msglen];

                for (ulong i = 0; i < msglen; ++i)
                {
                    decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);
                }

                // Convert the decoded byte array to a string
                text = Encoding.UTF8.GetString(decoded);

                if (string.IsNullOrEmpty(text))
                {
                    Debug.WriteLine("Decoded string is empty.");
                }
                else
                {
                    Debug.WriteLine($"Decoded string length: {text.Length}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
        return text;
    }

    private async Task BroadcastMessageAsync(NamedClient sender, string messageJson)
    {
        var message = JsonSerializer.Deserialize<Message>(messageJson);
        byte[] messageFrame = EncodeWebSocketFrame(messageJson);
        var clients = new List<NamedClient>();
        clients.AddRange(incomingClients);
        foreach (var client in clients)
        {
            if (client != sender && sender.TcpClient.Connected)
            {
                _ = SendMessage(client.Name, message.Payload, null, message.Sender);
            }
        }
    }
    private async Task ServerMessageAsync(NamedClient sender, string messageJson)
    {
        var message = JsonSerializer.Deserialize<Message>(messageJson);

        foreach (var client in incomingClients)
        {
            if (client.Name == message.Recipient || message.Recipient.ToLower() == "all" || message.Recipient.Split(",").Contains(client.Name))
            {
                if (!client.IsWebsocket)
                {
                    NetworkStream stream = client.TcpClient.GetStream();
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(Encrypt(messageJson)));
                }
                else
                {

                    byte[] messageFrame = EncodeWebSocketFrame(messageJson);
                    NetworkStream stream = client.TcpClient.GetStream();
                    await stream.WriteAsync(messageFrame, 0, messageFrame.Length);
                }
            }
        }
    }
    private async Task HandleMessageAsync(NamedClient namedClient, string initialData)
    {
        try
        {
            await Task.Run(async () =>
            {
                initialData = initialData.Replace("::ENDOFMESSAGE::", "");
                Message? message = null;
                try
                {
                    message = JsonSerializer.Deserialize<Message>(initialData);
                }
                catch (Exception ex)
                {
                    namedClient.TcpClient.Close();
                    RemoveClient(namedClient);
                    ConnectionClosed?.Invoke(namedClient);
                }
                if (message != null)
                {
                    if (namedClient.Name == "")
                    {
                        namedClient.Name = message.Sender;
                    }
                    namedClient = RunClientHandlers(namedClient);

                    if (message.Payload.EndsWith("::DISCONNECT::"))
                    {
                        namedClient.TcpClient.Close();
                    }
                    else
                    {
                        if (message.Sender != Name && (message.Recipient.Split(",").Contains(Name) || message.Recipient.ToLower() == "all") && !message.Broadcast)
                        {
                            message = RunMessageHandlers(message);
                            NewMessage?.Invoke(message);
                        }
                        else if (!message.Recipient.Split(",").Contains(Name))
                        {
                            if (message.Broadcast)
                            {
                                message.Server = null;
                                message = RunServerMessageHandlers(message);
                                _ = BroadcastMessageAsync(namedClient, JsonSerializer.Serialize(message));
                                SentServerBroadcast?.Invoke(message);
                            }
                            else
                            {
                                foreach (var recipient in message.Recipient.Split(","))
                                {
                                    message = RunServerMessageHandlers(message);
                                    _ = SendMessage(recipient, message.Payload, null, message.Sender);
                                    SentServerMessage?.Invoke(message);
                                }

                            }

                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    /// <summary>
    /// Run all of the client handlers in the clientHandlers list
    /// </summary>
    /// <param name="namedClient"></param>
    /// <returns>A NamedClient instance transformed by the client handlers</returns>
    private NamedClient RunClientHandlers(NamedClient namedClient)
    {
        foreach(var client in clientHandlers)
        {
            namedClient = client.Handle(namedClient);
        }
        return namedClient;
    }
    /// <summary>
    /// A message that can be read and handled by a constellation
    /// </summary>
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? SenderId { get; set; }
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public string Payload { get; set; }
        public string? Server { get; set; }
        public bool Broadcast { get; set; } = false;
        public DateTimeOffset Timestamp { get; set; }
    }
    /// <summary>
    /// An endpoint that a constellation uses to know what servers are available on startup
    /// </summary>
    public class Endpoint
    {
        public string? Address { get; set; }
        public int Port { get; set; }
        public string? Name { get; set; }
        public Endpoint() { }
        public override string ToString()
        {
            return (this.Address?? this.Name).ToString() + ":" + this.Port.ToString();
        }
    }
    public class Parser
    {
        /// <summary>
        /// Parses a config file in the c# language
        /// </summary>
        /// <param name="filename">The full path of the file to parse</param>
        /// <returns>The config object created from the file</returns>
        public static Constellation Parse(string filename)
        {
            var ds = new DeserializerBuilder();

            if (!System.IO.File.Exists(Environment.CurrentDirectory + "/" + filename))
            {
                throw new FileNotFoundException("File not found: " + Environment.CurrentDirectory + "/" + filename);
            }
            string lines = System.IO.File.ReadAllText(Environment.CurrentDirectory + "/" + filename);
            Constellation constellation = ds.Build().Deserialize<Constellation>(lines);
            if (constellation.Processes == null)
            {
                constellation.Processes = new List<ProcessStartInfo>();
            }
            constellation.Processes.ForEach((p) =>
        {
            var directory = p.WorkingDirectory;
            var rootFolder = constellation.GetRootFolder();

#if IOS
throw new NotSupportedException();
#endif
            if (directory.Contains("./"))
            {
                p.WorkingDirectory = Path.GetFullPath(directory, rootFolder);
            }
        });
            return constellation;
        }
    }
    private string GetRootFolder()
    {
        string rootFolder = string.Empty;

#if ANDROID
    // On Android, get the external files directory
    rootFolder = Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath;
#elif IOS || MACCATALYST
    // On iOS and Mac Catalyst, use the Documents folder
    rootFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#elif WINDOWS
    // On Windows, get the directory where the app is installed
    rootFolder = AppContext.BaseDirectory;
#elif MACOS
    // On macOS, use the application bundle's resource directory
    rootFolder = NSBundle.MainBundle.ResourcePath;
#else
        // Fallback for other platforms or future support
        rootFolder = Directory.GetCurrentDirectory();
        if (IsConsoleApp())
        {
            rootFolder = AppContext.BaseDirectory;
        }
#endif

        return rootFolder;
    }
    private bool IsConsoleApp()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var entryPoint = entryAssembly.EntryPoint;
            if (entryPoint != null)
            {
                var entryPointType = entryPoint.DeclaringType;

                if (entryPointType != null)
                {
                    // Check if the entry point type is within the 'System' namespace (Console apps usually aren't)
                    if (entryPointType.Namespace == null || !entryPointType.Namespace.StartsWith("System"))
                    {
                        // Check for a method signature typical of a console application:
                        // public static void Main(string[] args)
                        var parameters = entryPoint.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
/// <summary>
/// A Named client is a tcp client wrapper that has a name, id, and a IsWebsocket bool in order to handle different client types.
/// </summary>
public class NamedClient
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public TcpClient TcpClient { get; set; }
    public bool IsWebsocket { get; set; } = false;
    public WebSocket? WebSocket { get; set; } = null;
    public bool IsStreaming { get; set; } = false;
    public ConcurrentQueue<string> MessageQueue { get; set; } = new ConcurrentQueue<string>();
    public SslStream? SslStream { get; set; }
    public SemaphoreSlim semaphore { get; set; } = new SemaphoreSlim(1);
    public string? EncryptionId { get; set; } = null;

    public NamedClient(TcpClient client){
        TcpClient = client;
    }
}