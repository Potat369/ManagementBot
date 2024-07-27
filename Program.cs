using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ManagementBot;

class Program
{
    private static DiscordSocketClient _client;
    private static readonly List<ulong> Channels = [];
    private const string ChannelName = "channel-name";
    private const string UserCount = "user-count";
    
    public static async Task Main()
    {
        _client = new DiscordSocketClient();

        _client.Log += Log;
        _client.Ready += ClientOnReady;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;
        
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (token == null)
        {
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        
        await Task.Delay(-1);
    }
    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static async Task ClientOnReady()
    {
        List<ApplicationCommandProperties> applicationCommandProperties = [];
        try
        {
            var createChannelCommand = new SlashCommandBuilder()
                .WithName("create-channel")
                .WithDescription("Creates a new temporary channel.")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("user-count")
                    .WithDescription("User count for the channel.")
                    .WithType(ApplicationCommandOptionType.Integer)
                    .WithMinValue(1)
                    .WithMaxValue(99)
                ).AddOption(new SlashCommandOptionBuilder()
                    .WithName("channel-name")
                    .WithDescription("The name of the channel.")
                    .WithType(ApplicationCommandOptionType.String)
                );
            
            applicationCommandProperties.Add(createChannelCommand.Build());

            await _client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
        }
        catch (HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
    
    private static async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch(command.Data.Name)
        {
            case "create-channel":
                await HandleCreateChannelCommand(command);
                break;
        }
    }

    private static async Task HandleCreateChannelCommand(SocketSlashCommand command)
    {
        var guild = _client.GetGuild(command.GuildId.GetValueOrDefault());
        var userCount = 0;
        var channelName = "Temporary";

        foreach (var option in command.Data.Options)
        {
            switch (option.Name)
            {
                case ChannelName:
                    channelName = (string)option.Value;
                    break;
                case UserCount:
                    userCount = Convert.ToInt32(option.Value);
                    break;
            }
        }

        var restVoiceChannel = await guild.CreateVoiceChannelAsync("\ud83d\udd0a，" + channelName, properties =>
        {
            if (userCount is > 0)
            {
                properties.UserLimit = userCount;
            }

            properties.CategoryId = 1266497081095229592;
        });
        
        Channels.Add(restVoiceChannel.Id);
        await command.RespondAsync(text: "Succeed!", ephemeral: true);
        HandleChannel(restVoiceChannel);
    }

    private static async Task HandleChannel(RestVoiceChannel restVoiceChannel)
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        if (await _client.GetChannelAsync(restVoiceChannel.Id) is SocketVoiceChannel voiceChannel && voiceChannel.ConnectedUsers.Count == 0)
        {
            await voiceChannel.DeleteAsync();
            Channels.Remove(restVoiceChannel.Id);
        }
    }
    
    private static async Task ClientOnUserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
    {
        if (arg2.VoiceChannel != null && Channels.Contains(arg2.VoiceChannel.Id) && arg2.VoiceChannel.ConnectedUsers.Count == 0)
        {
            await arg2.VoiceChannel.DeleteAsync();
            Channels.Remove(arg2.VoiceChannel.Id);
        }
    }
}