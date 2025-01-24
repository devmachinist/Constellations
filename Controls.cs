using Spectre.Console;
using System.Collections.Concurrent;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Devmachinist.Constellations
{
    public partial class Constellation
    {
        public void Control()
        {
            Dictionary<string, Color> UserColors = new Dictionary<string, Color>();
            object lockObj = new object();
            int consoleHeight = Console.WindowHeight;
            int inputLine = 4;
            string currentInput = "";
            int selectedOption = 0;
            int totalOptions = 6;
            bool quit = false;
            bool selectingHub = false;
            bool hubJoined = false;
            var addingMessages = false;
            var backToStart = false;
            List<Message> Log = new List<Message>();
            List<Message> ConsoleLog = new List<Message>();
            ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();
            Endpoint? hub = null;
            this.NewMessage += addMessageToLog;
            void addMessageToLog(Constellation.Message message)
            {
                messageQueue.Enqueue(message);
            }
            ConsoleKeyInfo keyInfo;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;
            runControl();
            void runControl()
            {
                while (!quit)
                {
                    backToStart = false;
                    if (hubJoined == true)
                    {

                        Console.Clear();
                        var messageInput = string.Empty;
                        var cts = new CancellationTokenSource();
                        Task.Run(async () => _ = DisplayMessages());

                        while (!backToStart)
                        {
                            // Position the cursor at the current input line
                                Console.SetCursorPosition(0, inputLine);
                                Console.Write(new string(' ', Console.WindowWidth));  // Clear the line
                                Console.SetCursorPosition(0, inputLine);
                                Console.Write(currentInput); // Redraw the current input text

                            // Read user input one key at a time to avoid interruptions
                            var key = Console.ReadKey(intercept: true);
                                if (key.Key == ConsoleKey.Escape)
                                {
                                    hubJoined = false;
                                    hub = null;
                                backToStart = true;
                                    return;
                                }
                                else if (key.Key == ConsoleKey.Enter && !string.IsNullOrWhiteSpace(currentInput))
                                {
                                Console.SetCursorPosition(0, inputLine);
                                Console.Write(new string(' ', Console.WindowWidth));
                                var message = new Message
                                    {
                                        Sender = "You",
                                        Recipient = "Server",
                                        Payload = currentInput,
                                        Server = "Localhost",
                                        Timestamp = DateTimeOffset.Now
                                    };
                                    _ = this.SendMessage("All", currentInput, hub.Name, Name, true);
                                    messageQueue.Enqueue(message);
                                    currentInput = string.Empty;
                                }
                                else if (key.Key == ConsoleKey.Backspace && currentInput.Length > 0)
                                {
                                    currentInput = currentInput.Substring(0, currentInput.Length - 1);
                                }
                                else if (key.Key != ConsoleKey.Backspace)
                                {
                                    currentInput += key.KeyChar;
                                }
                        }
                        cts.Cancel();


                        async Task DisplayMessages()
                        {
                            await Task.Run(() =>
                            {
                                Console.SetCursorPosition(0, 0);
                                var HeaderPanel = new Panel(new Markup($"[green][bold] HUB: {hub.Name}[/] [/]"));
                                AnsiConsole.Write(HeaderPanel);
                                while (true)
                                {
                                    if (!messageQueue.IsEmpty)
                                    {
                                        while (messageQueue.TryDequeue(out Message message))
                                        {
                                            // Clear the input line before moving it down
                                            Console.SetCursorPosition(0, inputLine);
                                            Console.Write(new string(' ', Console.WindowWidth));

                                            // Move the input line down
                                            inputLine++;

                                            // Write the message above the input line
                                            Console.SetCursorPosition(0, inputLine - 1);
                                            var color = GetUserColor(message.Sender);
                                            AnsiConsole.Write(new Markup($"[{color}]{message.Sender}[/]: {message.Payload}\r\n"));

                                            // If input line reaches the bottom, scroll up
                                            if (inputLine >= consoleHeight)
                                            {
                                                Console.MoveBufferArea(0, 1, Console.WindowWidth, consoleHeight - 1, 0, 0);
                                                inputLine = consoleHeight - 1;
                                            }

                                            // Redraw the input
                                            Console.SetCursorPosition(0, inputLine);
                                            Console.Write(new string(' ', Console.WindowWidth));
                                            Console.SetCursorPosition(0, inputLine);
                                            Console.Write(currentInput);
                                        }
                                    }
                                    Thread.Sleep(100); // Small delay to avoid high CPU usage
                                }
                            });
                        }

                        Color GetUserColor(string sender)
                        {
                            if (!UserColors.ContainsKey(sender))
                            {
                                var availableColors = new[]
                                {
                Color.Red, Color.Blue, Color.Green, Color.Yellow,
                Color.Aqua, Color.Fuchsia, Color.Lime, Color.Maroon
                                };
                                UserColors[sender] = availableColors[UserColors.Count % availableColors.Length];
                            }
                            return UserColors[sender];
                        }
                    }
                    else
                    {
                        do
                        {
                            Console.Clear();
                            Console.WriteLine("Select an action:");
                            Console.WriteLine();

                            for (int i = 1; i <= totalOptions; i++)
                            {
                                if (i == selectedOption)
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                                    Console.BackgroundColor = ConsoleColor.Gray;
                                }
                                switch (i)
                                {
                                    case 1:
                                        Panel panel = new Panel("Join a hub listed in your endpoints. Must be a live control hub.");
                                        panel.Header = new PanelHeader("Join a hub");
                                        panel.Border = BoxBorder.Rounded;
                                        AnsiConsole.Write(panel);
                                        Console.WriteLine();
                                        break;

                                    case 2:
                                        Panel panel2 = new Panel("Call a this by name");
                                        panel2.Header = new PanelHeader("Get Constellation");
                                        panel2.Border = BoxBorder.Rounded;
                                        AnsiConsole.Write(panel2);
                                        Console.WriteLine();

                                        break;

                                    case 3:
                                        Panel panel3 = new Panel("Start the current this");
                                        panel3.Header = new PanelHeader("Start");
                                        panel3.Border = BoxBorder.Rounded;
                                        AnsiConsole.Write(panel3);
                                        Console.WriteLine();
                                        break;

                                    case 4:
                                        Panel panel4 = new Panel("Stop the current this");
                                        panel4.Header = new PanelHeader("Stop");
                                        panel4.Border = BoxBorder.Rounded;
                                        AnsiConsole.Write(panel4);
                                        Console.WriteLine();
                                        break;

                                    case 5:
                                        Panel panel5 = new Panel("Send a message to another this");
                                        panel5.Header = new PanelHeader("Send Message");
                                        panel5.Border = BoxBorder.Rounded;
                                        AnsiConsole.Write(panel5);
                                        Console.WriteLine();
                                        break;

                                    case 6:
                                        Console.WriteLine("End");
                                        break;
                                }

                                Console.ForegroundColor = ConsoleColor.Gray;
                                Console.BackgroundColor = ConsoleColor.Black;
                            }

                            keyInfo = Console.ReadKey(true);

                            if (keyInfo.Key == ConsoleKey.UpArrow && selectedOption > 1)
                            {
                                selectedOption--;
                            }
                            else if (keyInfo.Key == ConsoleKey.DownArrow && selectedOption < totalOptions)
                            {
                                selectedOption++;
                            }
                            else if (keyInfo.Key == ConsoleKey.Escape)
                            {
                                quit = true;
                            }
                            else if (keyInfo.Key == ConsoleKey.Enter)
                            {
                                Console.Clear();
                                switch (selectedOption)
                                {
                                    case 1:
                                        selectingHub = true;

                                        while (selectingHub)
                                        {
                                            AnsiConsole.Clear();
                                            var selectedAction = AnsiConsole.Prompt(
                                                new SelectionPrompt<string>()
                                                    .Title("Select an action:")
                                                    .AddChoices(new[] { "Select Hub", "Add New Hub", "Cancel" }));

                                            switch (selectedAction)
                                            {
                                                case "Select Hub":
                                                    SelectEndpoint();
                                                    break;
                                                case "Add New Hub":
                                                    AddEndpoint();
                                                    break;
                                                case "Cancel":
                                                    selectingHub = false;
                                                    break;
                                            }
                                        }
                                        break;

                                    case 2:
                                        Console.WriteLine("Enter node name:");
                                        string failoverNodeName = Console.ReadLine();
                                        break;

                                    case 3:
                                        this.StartOrchestration();
                                        break;

                                    case 4:
                                        this.StopOrchestration();
                                        break;

                                    case 5:
                                        Console.WriteLine("Enter target node name:");
                                        string targetNodeName = Console.ReadLine();

                                        Console.WriteLine("Enter message:");
                                        string message = Console.ReadLine();
                                        _ = this.SendMessage(targetNodeName, message);
                                        break;

                                    case 6:
                                        quit = true;
                                        break;
                                }
                                if (hubJoined)
                                {
                                    Console.WriteLine("Press any key to join the hub...Press ESC to exit");
                                }
                                else
                                {
                                    Console.WriteLine("Press any key to restart...Press ESC to exit");
                                }
                                keyInfo = Console.ReadKey(true);
                                if (keyInfo.Key == ConsoleKey.Escape)
                                {
                                    hubJoined = false;
                                    hub = null;
                                    quit = true;
                                }

                                selectedOption = 1;
                            }
                        } while (!hubJoined && !quit);
                    }
                }
            }
            void HandleConsoleResize()
            {
                while (true)
                {
                    int newHeight = Console.WindowHeight;

                    if (newHeight != consoleHeight)
                    {
                        lock (lockObj)
                        {
                            // If console height increases, adjust the input line down
                            if (newHeight > consoleHeight)
                            {
                                inputLine += (newHeight - consoleHeight);
                                inputLine = Math.Min(inputLine, newHeight - 1);
                            }
                            else // If console height decreases, move input line up
                            {
                                inputLine -= (consoleHeight - newHeight);
                                if (inputLine < 0) inputLine = 0;
                            }

                            consoleHeight = newHeight;

                            // Redraw current input
                            Console.SetCursorPosition(0, inputLine);
                            Console.Write(new string(' ', Console.WindowWidth));
                            Console.SetCursorPosition(0, inputLine);
                            Console.Write(currentInput);
                        }
                    }

                    Thread.Sleep(100); // Check periodically for resizing
                }
            }

            void SelectEndpoint()
            {
                try
                {
                    var selectingEndpoint = true;
                    if (this.Endpoints.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[red]No endpoints available.[/]");
                        AnsiConsole.Prompt(new TextPrompt<string>("Press [green]Enter[/] to return to the main menu."));
                        return;
                    }
                    while (selectingEndpoint)
                    {
                        var choices = new List<string>();
                        choices.AddRange(this.Endpoints.Keys);
                        choices.Add("Cancel");
                        var selectedEndpoint = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Select an [green]Endpoint[/]:")
                                .AddChoices(choices));
                        if (selectedEndpoint == "Cancel")
                        {
                            selectingEndpoint = false;
                            return;
                        }
                        var endpoint = this.Endpoints[selectedEndpoint];
                        AnsiConsole.MarkupLine($"[green]Selected Endpoint:[/]\nAddress: {endpoint.Address}\nPort: {endpoint.Port}\nName: {endpoint.Name}");
                        var key = AnsiConsole.Ask<string>("Enter the [green]Key[/]:");
                        this.SetKey(key);
                        selectingHub = false;
                        selectingEndpoint = false;
                        hubJoined = true;
                        hub = endpoint;
                        hub.Name = selectedEndpoint;
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            }

            void AddEndpoint()
            {
                var address = AnsiConsole.Ask<string>("Enter the [green]Address[/]:");
                var port = AnsiConsole.Ask<int>("Enter the [green]Port[/]:");
                var name = AnsiConsole.Ask<string>("Enter the [green]Name[/]:");
                var key = AnsiConsole.Ask<string>("Enter the [green]Key[/]:");
                this.SetKey(key);

                var endpoint = new Endpoint { Address = address, Port = port, Name = name };
                this.Endpoints[name] = endpoint;

                AnsiConsole.MarkupLine($"[green]Endpoint '{name}' added successfully.[/]");
                SaveConstellation();
                AnsiConsole.Prompt(new TextPrompt<string>("Press [green]Enter[/] to return to the main menu."));
            }

        }
        public void SaveConstellation()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(this);
            File.WriteAllText(this.ConfigFile, yaml);
            AnsiConsole.MarkupLine($"[green]Constellation saved to {this.ConfigFile}.[/]");
        }

    }
}
