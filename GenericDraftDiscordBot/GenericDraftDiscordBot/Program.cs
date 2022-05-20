// See https://aka.ms/new-console-template for more information

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GenericDraftDiscordBot.Modules;
using GenericDraftDiscordBot.Modules.Draft;
using GreetingsBot.Common;
using GreetingsBot.Init;
using GreetingsBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddJsonFile($"appsettings.json")
    .AddEnvironmentVariables()
    .Build();
var client = new DiscordShardedClient();


var commands = new CommandService(new CommandServiceConfig
{
    // Again, log level:
    LogLevel = LogSeverity.Info,
            
    // There's a few more properties you can set,
    // for example, case-insensitive commands.
    CaseSensitiveCommands = false,
});

// Setup your DI container.
Bootstrapper.Init();
Bootstrapper.RegisterInstance(client);
Bootstrapper.RegisterInstance(commands);
Bootstrapper.RegisterInstance(config);
Bootstrapper.RegisterType<ICommandHandler, CommandHandler>();
Bootstrapper.RegisterType<IDraftStateManager, DraftStateManager>();
Bootstrapper.RegisterType<IPassphraseGenerator, PassphraseGenerator>();


await MainAsync();

async Task MainAsync()
{
    await Bootstrapper.ServiceProvider.GetRequiredService<ICommandHandler>().InitializeAsync();
    
    client.ShardReady += async shard =>
    {
        Logger.Log(LogSeverity.Info, "ShardReady", $"Shard Number {shard.ShardId} is connected and ready!");
    };
        
    // Login and connect.
    var token = config.GetRequiredSection("Settings")["DiscordBotToken"];
    if (string.IsNullOrWhiteSpace(token))
    {
        Logger.Log(LogSeverity.Error, $"{nameof(Program)} | {nameof(MainAsync)}", "Token is null or empty.");
        return;
    }
        
    await client.LoginAsync(TokenType.Bot, token);
    await client.StartAsync();

    // Wait infinitely so your bot actually stays connected.
    await Task.Delay(Timeout.Infinite);
}