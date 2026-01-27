// See https://aka.ms/new-console-template for more information

using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DiscordPantheonGuildBot.Commands;
using DiscordPantheonGuildBot.Data;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordPantheonGuildBot;

internal class Program {
    private static DiscordClient? Client { get; set; }
    
    static async Task Main(string[] args) {
        var settingsReader = new SettingsReader();
        try {
            await settingsReader.ReadConfigJson();
        } catch (Exception ex) {
            Console.WriteLine($"[ERROR] Failed to load configuration: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(settingsReader.Token) || settingsReader.Token == "<TOKEN_HERE>") {
            Console.WriteLine($"[ERROR] Failed to load configuration: Token not initialized.");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(settingsReader.Prefix)) {
            Console.WriteLine($"[ERROR] Failed to load configuration: Prefix not initialized.");
            return;
        }
        
        var builder = DiscordClientBuilder.CreateDefault(settingsReader.Token, DiscordIntents.AllUnprivileged 
                                                                              | DiscordIntents.GuildMembers 
                                                                              | DiscordIntents.MessageContents 
                                                                              | DiscordIntents.GuildPresences)
            .ConfigureLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .ConfigureServices(s => s.AddSingleton(new DatabaseService()))
            .ConfigureEventHandlers
            (
                b => b.HandleGuildDownloadCompleted(GuildDownloadCompleted)
                    .HandleSessionCreated(SessionCreated)

            )
            .UseInteractivity(new InteractivityConfiguration { 
                Timeout = TimeSpan.FromSeconds(30) 
            })
            .UseCommands((s, e) => {
                e.AddCommands(typeof(Program).Assembly);
                e.CommandErrored += async (s, e) => {
                    Console.WriteLine($"[ERROR] Command '{e.Context.Command.FullName}' failed: {e.Exception.Message}");
                    if (e.Exception.InnerException != null) {
                        Console.WriteLine($"[INNER ERROR] {e.Exception.InnerException.Message}");
                    }
                    await e.Context.TimedMessageAsync($"❌ An error occurred in Bastion Guild Manager Bot: {e.Exception.Message}", Constants.ShortResponseDelay);
                };
                // e.AddCommands(typeof(GuildCommands));
                // e.AddCommands(typeof(ConfigCommands));
                // e.AddCommands(typeof(AttendanceCommands));
                // e.AddCommands(typeof(GameCommands));
                var textProcessor = new TextCommandProcessor(new TextCommandConfiguration {
                    PrefixResolver = new DefaultPrefixResolver(true, settingsReader.Prefix).ResolvePrefixAsync
                });
                e.AddProcessor(textProcessor);
            });


        
        Client = builder.Build();
        
        try {
            await Client.ConnectAsync();
        } catch (Exception ex) {
            Console.WriteLine($"[ERROR] Failed to connect: {ex.Message}");
        }
        await Task.Delay(-1); //says to keep running
    }
    
    private static Task SessionCreated(DiscordClient sender, DSharpPlus.EventArgs.SessionCreatedEventArgs e) {
        Console.WriteLine("[INFO] Bot session created!");
        return Task.CompletedTask;
    }
    
    private static Task GuildDownloadCompleted(DiscordClient sender, DSharpPlus.EventArgs.GuildDownloadCompletedEventArgs e) {
        Console.WriteLine("[INFO] Bot download complete!");
        return Task.CompletedTask;
    }
}