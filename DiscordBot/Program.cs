using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Net;
using System.Reflection;

public class Program
{

    public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    private DiscordSocketClient _client;
    public CommandService _commands;
    private IServiceProvider _services;

    private SocketGuild _guild;

    private ulong _logChannelID;
    private SocketTextChannel _logChannel;

    public bool _apiCall = false; 

    public List<dynamic> _currentList = new List<dynamic>();

    public async Task MainAsync()
    {

        string tokenURL = "C:\\Users\\Anna\\Documents\\GitHub\\discord-disruption-alert-bot\\SecretToken.txt";

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent

        };
       
        _client = new DiscordSocketClient(config);
        _commands = new CommandService();

        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_commands)
            .BuildServiceProvider();

        _client.Log += Log;

        //  You can assign your bot token to a string, and pass that in to connect.
        //  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.
        var token = File.ReadAllText(tokenURL);

        // Some alternative options would be to keep your token in an Environment Variable or a standalone file.
        // var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");
        // var token = File.ReadAllText("token.txt");
        // var token = JsonConvert.DeserializeObject<AConfigurationClass>(File.ReadAllText("config.json")).Token;

        await RegisterCommandsAsync();
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        // Block this task until the program is closed.
        await Task.Delay(-1);

    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    public async Task RegisterCommandsAsync()
    {
        _client.MessageReceived += HandleCommandAsync;
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }

    public async Task HandleCommandAsync(SocketMessage arg)
    {
        var message = arg as SocketUserMessage;
        var context = new SocketCommandContext(_client, message);
        var channel = _client.GetChannel(_logChannelID) as SocketTextChannel;

        //console log with message received and user info
        Console.WriteLine("-----------\nUser:   " + message.Author.Username + " with ID " + message.Author.Id +
                                                    "\nHave sent: " +
                                                    "\n" + message.ToString());

        if (message.Author.IsBot) return;

        int argPos = 0;

        if (message.HasStringPrefix("!", ref argPos))
        {
            Console.WriteLine("Prefix");
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            if (!result.IsSuccess) Console.WriteLine(result.ErrorReason);
            if (result.Error.Equals(CommandError.UnmetPrecondition)) await message.Channel.SendMessageAsync
                    (result.ErrorReason);
        }

        var text = message.ToString().ToLower();

        if (text == "/crea log")
        {
            ulong messageChannelId = message.Channel.Id;
            var textChannel = _client.GetChannel(messageChannelId) as SocketTextChannel;
            _guild = _client.GetGuild(textChannel.Category.GuildId);
            var newChannel = await _guild.CreateTextChannelAsync("log");

            _logChannelID = newChannel.Id;
            await textChannel.SendMessageAsync("Channel created - Open it to see the Bot's log.");
            await newChannel.SendMessageAsync("Channel created - Here you can see the Bot's log.");
        }

        if (channel !=null)
        {
            await channel.SendMessageAsync("-----------\nUser:   " + message.Author.Username + "with ID  " +
                                           message.Author.Id +
                                           "\nWrite: " +
                                           "\n" + message.ToString());
        }

        switch (text)
        {
            case "hi":
                await message.Channel.SendMessageAsync("Hello " + message.Author.Username + "!");
                Console.WriteLine("Message Received");
                break;

            case "start":

                WebClient client = new WebClient();

                _apiCall = true;

                ConsecutiveAPICalls(client, message);

                break;

            case "stop":
                _apiCall = false;

                break;
        }
    }

    public async Task ConsecutiveAPICalls(WebClient client, SocketMessage message)
    {
        while (_apiCall == true)
        {
            string json = client.DownloadString("https://api.warframestat.us/pc/");

            dynamic obj = JsonConvert.DeserializeObject<dynamic>(json);

            foreach (dynamic item in obj["fissures"])
            {
                if (item["isHard"] == true && item["missionType"] == "Disruption" && item["tier"] != "Requiem")
                {
                    if (_currentList.Count == 0 )
                    {
                        _currentList.Add(item);

                        await message.Channel.SendMessageAsync("There is a Steel Path mission Tenno!" +
                                                            "\n" + item["node"] +
                                                            "\n" + item["missionType"] +
                                                            "\n" + item["tier"] +
                                                            "\n" + item["eta"]);
                    }
                    
                    else
                    {
                        foreach (dynamic fissure in _currentList)
                        {
                            if (item["id"] != fissure["id"])
                            {
                                _currentList.Add(item);

                                await message.Channel.SendMessageAsync("There is a Steel Path mission Tenno!" +
                                                            "\n" + item["node"] +
                                                            "\n" + item["missionType"] +
                                                            "\n" + item["tier"] +
                                                            "\n" + item["eta"]);
                            }

                            if (fissure["expired"] == true)
                            {
                                _currentList.Remove(fissure);
                            }
                        }
                    }

                }
            }
            Console.WriteLine("Checked. Sadly no new disruption missions :(");

            await Task.Delay(10000);
        }
    }
}