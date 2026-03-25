using System.ComponentModel;
using DiscordPantheonGuildBot.Commands;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;

namespace DiscordPantheonGuildBot;

public class HelpModule 
{
    [Command("help")]
    [Description("Displays help for bot commands.")]
    // This attribute prevents the command from being registered as a Slash Command
    [AllowedProcessors(typeof(TextCommandProcessor))] 
    public async Task HelpAsync(CommandContext ctx, string? commandName = null)
    {
        var embed = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Azure) // Your custom color
            .WithTitle("Bot Help Menu");

        if (string.IsNullOrWhiteSpace(commandName))
        {
            // List all commands
            var extension = ctx.Extension;
            foreach (var cmd in extension.Commands.Values)
            {
                switch (cmd.Name) {
                    case "update":
                        var uex = Environment.NewLine +Environment.NewLine + "Ex: You have an existing character in the roster, !update <name> <class> <level>" + Environment.NewLine + " or " + Environment.NewLine + "!update Horatio 5" + Environment.NewLine + "!update Horatio Monk";
                        embed.AddField(cmd.Name, cmd.Description + uex ?? "No description provided.");
                        break;
                    case "add":
                        var aex = Environment.NewLine +Environment.NewLine + "Ex: !add <name> <class> <level>" + Environment.NewLine + " or " + Environment.NewLine + "!add Horatio Monk 5" + Environment.NewLine + " or " + Environment.NewLine + "!add Horatio 5" + Environment.NewLine + "!update Horatio Monk";
                        embed.AddField(cmd.Name, cmd.Description + aex ?? "No description provided.");
                        break;
                    default:
                        embed.AddField(cmd.Name, cmd.Description ?? "No description provided.");
                        break;
                }
                
            }
        }
        else
        {
            // Detail a specific command
            // Note: In v5, you find commands via ctx.Extension.Commands
            if (ctx.Extension.Commands.TryGetValue(commandName, out var command))
            {
                switch (command.Name) {
                    case "update":
                        var uex = Environment.NewLine +Environment.NewLine + "Ex: You have an existing character in the roster, !update <name> <class> <level>" + Environment.NewLine + " or " + Environment.NewLine + "!update Horatio 5" + Environment.NewLine + "!update Horatio Monk";
                        embed.WithTitle($"Help: {command.Name}")
                            .WithDescription(command.Description + uex);
                        break;
                    case "add":
                        var aex = Environment.NewLine +Environment.NewLine + "Ex: !add <name> <class> <level>" + Environment.NewLine + " or " + Environment.NewLine + "!add Horatio Monk 5" + Environment.NewLine + " or " + Environment.NewLine + "!add Horatio 5" + Environment.NewLine + "!update Horatio Monk";
                        embed.WithTitle($"Help: {command.Name}")
                            .WithDescription(command.Description + aex);
                        break;
                    default:
                        embed.WithTitle($"Help: {command.Name}")
                            .WithDescription(command.Description);
                        break;
                }

            }
            else
            {
                await ctx.RespondAsync("Command not found.");
                return;
            }
        }

        try {
            var content = new DiscordMessageBuilder().AddEmbed(embed);

            ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
        //await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}