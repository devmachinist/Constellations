## **Constellations**

The **Constellation** class offers a highly customizable system to manage distributed systems. It supports communication, orchestration, and secure connections between various system components through routing and messaging mechanisms.

---

### **Features of Constellation**

- **Message Routing**: Direct, server-routed, and broadcast messaging options.
- **Process Management**: Start, manage, and monitor processes either programmatically or using configuration files.
- **Event-Driven**: Automatically respond to events such as connections, disconnections, and message receipt.
- **Secure Communication**: Utilize `X509Certificate` for encrypted connections.
- **Configurable**: Load settings from YAML files or define them programmatically.
- **Server/Client Flexibility**: Act as either a server, a client, or both within the same instance.

---

### **Installation**

To use **Constellation**, add the NuGet package to your project:

```bash
dotnet add package Constellations
```

---

### **Getting Started**

Below is a basic example of setting up a **Constellation** instance:

```csharp
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

var constellation = new Constellation("MyConstellation")
    .SetX509Cert(new X509Certificate("path/to/certificate"))
    .AsServer()
    .ListenOn(null, "127.0.0.1", 8080)
    .AddProcesses(new List<ProcessStartInfo>
    {
		new ProcessStartInfo
		{
			FileName = "notepad.exe",
			Arguments = "",
			UseShellExecute = true
		},

		// Add ProcessStartInfo for Calculator
		new ProcessStartInfo
		{
			FileName = "calc.exe",
			UseShellExecute = true
		}
    })
    .AllowBroadcasting()
    .Run();
```

---

### **Core Methods**

**1. Configuration**

- **SetName(string name)**: Sets the name of the constellation.
- **UseYamlConfig(string yamlFile)**: Configures the constellation using a YAML file.
- **SetX509Cert(X509Certificate cert)**: Configures a certificate for encrypted connections.

---

**2. Server and Listener**

- **AsServer()**: Configures the constellation to function as a server.
- **ListenOn(string name, string? ipv4, int port)**: Sets up the server's listener, including IP and port.
- **AddEndpoint(string name, string? ipv4, int port)**: Adds another endpoint for client-server communication.

---

**3. Processes and Orchestration**

- **AddProcesses(List**\*\* processes)\*\*: Adds processes to be started by the constellation.
- **Run()**: Starts the constellation and all configured processes.

---

### **Messaging**

- **AllowBroadcasting()**: Enables the ability to send broadcast messages.
- **NoBroadcasting()**: Disables the broadcasting feature.
- **AddMessageHandlers(Type[] types)**: Add handlers for different message types.
- **SendMessage(string recipient, string message, string? server, string? name)**: Send messages to other constellations.

---

### **Events**

**Message Events**

- **NewMessage**: Triggered when a new message is received.
- **SentMessage**: Triggered when a message is successfully sent.
- **SendMessageFailed**: Triggered if a message fails to send.

**Connection Events**

- **NewConnection**: Triggered when a new client connects to the constellation.
- **ConnectionClosed**: Triggered when a client disconnects.

**Server Events**

- **SentServerMessage**: Triggered when a server routes a message to its recipient.
- **SentServerBroadcast**: Triggered when the server broadcasts a message to multiple clients.

---

### **Advanced Features**

**Custom Message Handlers**

You can define your own custom message handler classes to handle incoming or outgoing messages:

```csharp
public class MyCustomHandler : IMessageHandler
{
    public Message Handle(Message message)
    {
        // Your custom logic for message handling
        return message;
    }
}

var constellation = new Constellation("CustomHandlers")
    .AddMessageHandlers(new[] { typeof(MyCustomHandler) })
    .Run();
```

**Standalone Mode**

You can configure **Constellation** to run without relying on external hosts, keeping it alive in a standalone mode:

```csharp
var constellation = new Constellation()
    .WillStandAlone()
    .Run();
```

---

### **Broadcasting**

To send messages to all connected clients, enable broadcasting with this method:

```csharp
var constellation = new Constellation()
    .AllowBroadcasting()
    .Run();
```

---

### **Graceful Shutdown**

To shut down the constellation and stop all running processes:

```csharp
constellation.Shutdown();
```

---

### **Configuration with YAML**

```csharp
var constellation = new Constellation()
    .UseYamlConfig("Constellation.yml")
    .Run();
```
