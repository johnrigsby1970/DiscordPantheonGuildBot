using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DiscordPantheonGuildBot.Data;
using DiscordPantheonGuildBot.Models;
using System.Text;
using DSharpPlus.Interactivity;
using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordPantheonGuildBot.Commands;

[Command("game")]
[Description("Commands to manage games in the guild.")]
public class GameCommands {
    public DatabaseService Database { get; }
    private ILogger<GameCommands> Logger { get; set; }
    
    public GameCommands(DatabaseService database, ILogger<GameCommands> logger) {
        Database = database;
        Logger = logger;
    }
    
    private async Task<bool> IsAuthorized(CommandContext ctx) {
        if (ctx.Guild!.OwnerId == ctx.User.Id) return true;
        if (ctx.Member!.Permissions.HasPermission(DiscordPermission.Administrator)) return true;
        return false;
    }

    private async Task<Game?> ResolveGame(CommandContext ctx, string gameIdOrName) {
        if (int.TryParse(gameIdOrName, out int gameId)) {
            return await Database.GetGame(ctx.Guild!.Id, gameId);
        }

        return await Database.GetGameByName(ctx.Guild!.Id, gameIdOrName);
    }

    [Command("create")]
    [Description("Creates a new game.")]
    public async Task CreateGame(CommandContext ctx,
        [Description("Name of a new game to add to the system.")]
        [RemainingText] string name) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can create games.", Constants.LongResponseDelay).Forget();
                return;
            }

            var result = await Database.CreateGame(ctx.Guild!.Id, name);
            if (result == -2) {
                ctx.TimedMessageAsync($"A game named '{name}' already exists.", Constants.LongResponseDelay).Forget();
            }
            else if (result > 0) {
                ctx.TimedMessageAsync($"Game '{name}' created with ID {result}.", Constants.LongResponseDelay).Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to create game.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error CreateGame.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
    
    
    [Command("updategamename")]
    [Description("Updates game name for current channel game.")]
    public async Task UpdateGameName(CommandContext ctx, 
        [Description("Name of a new game to add to the system.")]
        [RemainingText] string name) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can create games.", Constants.LongResponseDelay).Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame || game is null) return;
            
            bool success = await Database.UpdateGameName(game.Id, name);
            if (success) {
                ctx.TimedMessageAsync($"Name updated for game '{game.Id}' to '{name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update game name.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error UpdateGameNameById.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
    
    [Command("updategamenamebyid")]
    [Description("Updated game name for game with id.")]
    public async Task UpdateGameNameById(CommandContext ctx, 
        [Description("Game Id to update.")]
        int gameId,
        [Description("Name of a new game to add to the system.")]
        [RemainingText] string name) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can create games.", Constants.LongResponseDelay).Forget();
                return;
            }

            bool success = await Database.UpdateGameName(gameId, name);
            if (success) {
                ctx.TimedMessageAsync($"Name updated for game '{gameId}' to '{name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update game name.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error UpdateGameNameById.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("setdescriptionbygamename")]
    [Description("Sets the description of a game.")]
    public async Task SetDescriptionByGameName(CommandContext ctx,
        [Description("The game name and description (e.g., !game setdescription<game> <description>)")]
        [RemainingText] string args) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can set game descriptions.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count < 2) {
                ctx.TimedMessageAsync("Invalid format. Expected: `!game setdescription <name> <description>`",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            // Game names cannot start with a number for this command.
            if (char.IsDigit(parts[0][0])) {
                ctx.TimedMessageAsync("Game names cannot start with a number. Use `setdescriptionid` to use a game ID.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            var games = await Database.ListGames(ctx.Guild!.Id);
            Game? game = null;
            string description = "";

            // Try to match game name from the start of the args
            // We'll iterate through all games and see if args starts with their name
            var matchedGames = games.Where(g => args.StartsWith(g.Name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(g => g.Name.Length).ToList();

            foreach (var g in matchedGames) {
                // Ensure it's a full word match
                if (args.Length == g.Name.Length || args[g.Name.Length] == ' ') {
                    game = g;
                    description = args.Substring(g.Name.Length).Trim();
                    break;
                }
            }

            if (game == null) {
                ctx.TimedMessageAsync("Game not found. Ensure the game name is correct and does not start with a number.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            if (string.IsNullOrWhiteSpace(description)) {
                ctx.TimedMessageAsync("Description cannot be empty.", Constants.LongResponseDelay).Forget();
                return;
            }

            try {
                var chn = await ctx.Client.GetChannelAsync(ctx.Channel.Id);
                await chn.ModifyAsync(x => x.Topic = description);
            }
            catch (Exception ex) {
                ctx.TimedMessageAsync($"Failed to update channel topic: {ex.Message}", Constants.LongResponseDelay)
                    .Forget();
                return;
            }

            bool success = await Database.UpdateGameDescription(game.Id, description);
            if (success) {
                ctx.TimedMessageAsync($"Description updated for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update description.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error SetDescription.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("setdescriptionbygameid")]
    [Description("Sets the description of a game using its ID.")]
    public async Task SetDescriptionByGameId(CommandContext ctx,
        [Description("ID of the game to set description for.")]
        int gameId,
        [Description("New description for the game.")]
        [RemainingText] string description) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can set game descriptions.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            var game = await Database.GetGame(ctx.Guild!.Id, gameId);
            if (game == null) {
                ctx.TimedMessageAsync($"Game with ID {gameId} not found.", Constants.LongResponseDelay).Forget();
                return;
            }

            if (string.IsNullOrWhiteSpace(description)) {
                ctx.TimedMessageAsync("Description cannot be empty.", Constants.LongResponseDelay).Forget();
                return;
            }

            try {
                var chn = await ctx.Client.GetChannelAsync(ctx.Channel.Id);
                await chn.ModifyAsync(x => x.Topic = description);
            }
            catch (Exception ex) {
                ctx.TimedMessageAsync($"Failed to update channel topic: {ex.Message}", Constants.LongResponseDelay)
                    .Forget();
                return;
            }

            bool success = await Database.UpdateGameDescription(game.Id, description);
            if (success) {
                ctx.TimedMessageAsync($"Description updated for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update description.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error SetDescriptionById.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("setdescription")]
    [Description("Sets the description of a game using its ID.")]
    public async Task SetDescription(CommandContext ctx,
        [Description("New description for the game asociated with current channel.")]
        [RemainingText] string description) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can set game descriptions.",
                    Constants.LongResponseDelay).Forget();
                return;
            }
            
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame || game is null) return;

            if (string.IsNullOrWhiteSpace(description)) {
                ctx.TimedMessageAsync("Description cannot be empty.", Constants.LongResponseDelay).Forget();
                return;
            }

            try {
                var chn = await ctx.Client.GetChannelAsync(ctx.Channel.Id);
                await chn.ModifyAsync(x => x.Topic = description);
            }
            catch (Exception ex) {
                ctx.TimedMessageAsync($"Failed to update channel topic: {ex.Message}", Constants.LongResponseDelay)
                    .Forget();
                return;
            }

            bool success = await Database.UpdateGameDescription(game.Id, description);
            if (success) {
                ctx.TimedMessageAsync($"Description updated for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update description.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error SetDescriptionById.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
    
    private async Task<(bool, Game?)> EnsureGame(CommandContext ctx) {
        var game = await Database.GetGameByChannel(ctx.Channel.Id);
        if (game == null) {
            ctx.TimedMessageAsync(
                "This channel is not assigned to any game. Use `!game assignchannel <name>` first.",
                Constants.LongResponseDelay).Forget();
            return (false, null);
        }

        return (true, game);
    }
    
     [Command("setwelcome")]
    [Description("Sets the welcome message of a game.")]
    public async Task SetWelcome(CommandContext ctx,
        [Description("The welcome message for current game (e.g., !game setwelcome Welcome to the team!)")]
        [RemainingText] string? welcomeMessage) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can set game welcome messages.",
                    Constants.LongResponseDelay).Forget();
                return;
            }
            
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame || game is null) return;

            if (string.IsNullOrWhiteSpace(welcomeMessage)) {
                ctx.TimedMessageAsync("Welcome message cannot be empty.", Constants.LongResponseDelay).Forget();
                return;
            }

            bool success = await Database.UpdateGameWelcomeMessage(game.Id, welcomeMessage);
            if (success) {
                ctx.TimedMessageAsync($"Welcome message updated for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update welcome message.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error SetWelcome.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
    
    [Command("setwelcomebygamename")]
    [Description("Sets the welcome message of a game.")]
    public async Task SetWelcomeByGameName(CommandContext ctx,
        [Description("The game name and welcome message (e.g., !game setwelcome My Game Welcome Msg)")]
        [RemainingText] string args) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can set game welcome messages.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count < 2) {
                ctx.TimedMessageAsync("Invalid format. Expected: `!game setwelcome <name> <message>`",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            // Game names cannot start with a number for this command.
            if (char.IsDigit(parts[0][0])) {
                ctx.TimedMessageAsync("Game names cannot start with a number. Use `setwelcomeid` to use a game ID.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            var games = await Database.ListGames(ctx.Guild!.Id);
            Game? game = null;
            string welcomeMessage = "";

            // Try to match game name from the start of the args
            var matchedGames = games.Where(g => args.StartsWith(g.Name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(g => g.Name.Length).ToList();

            foreach (var g in matchedGames) {
                if (args.Length == g.Name.Length || args[g.Name.Length] == ' ') {
                    game = g;
                    welcomeMessage = args.Substring(g.Name.Length).Trim();
                    break;
                }
            }

            if (game == null) {
                ctx.TimedMessageAsync("Game not found. Ensure the game name is correct and does not start with a number.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            if (string.IsNullOrWhiteSpace(welcomeMessage)) {
                ctx.TimedMessageAsync("Welcome message cannot be empty.", Constants.LongResponseDelay).Forget();
                return;
            }

            bool success = await Database.UpdateGameWelcomeMessage(game.Id, welcomeMessage);
            if (success) {
                ctx.TimedMessageAsync($"Welcome message updated for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update welcome message.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error SetWelcome.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("setwelcomebygameid")]
    [Description("Sets the welcome message of a game using its ID.")]
    public async Task SetWelcomeById(CommandContext ctx,
        [Description("ID of the game to set welcome message for.")]
        int gameId,
        [Description("New welcome message for the game.")]
        [RemainingText] string welcomeMessage) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can set game welcome messages.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            var game = await Database.GetGame(ctx.Guild!.Id, gameId);
            if (game == null) {
                ctx.TimedMessageAsync($"Game with ID {gameId} not found.", Constants.LongResponseDelay).Forget();
                return;
            }

            if (string.IsNullOrWhiteSpace(welcomeMessage)) {
                ctx.TimedMessageAsync("Welcome message cannot be empty.", Constants.LongResponseDelay).Forget();
                return;
            }

            bool success = await Database.UpdateGameWelcomeMessage(game.Id, welcomeMessage);
            if (success) {
                ctx.TimedMessageAsync($"Welcome message updated for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to update welcome message.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error SetWelcomeById.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
    
    [Command("showwelcome")]
    [Description("Shows the welcome message for a game.")]
    public async Task ShowWelcome(CommandContext ctx, 
        [Description("Game name or id to show welcome message for. If left empty, will use current channel's game.")]
        [RemainingText] string? gameIdOrName = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame || game is null) return;
            
            if (string.IsNullOrEmpty(game.WelcomeMessage)) {
                ctx.TimedMessageAsync($"No welcome message set for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"**Welcome Message for {game.Name}:**\n{game.WelcomeMessage}",
                    Constants.ListResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowWelcome.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
    
    [Command("showwelcomebygameiforname")]
    [Description("Shows the welcome message for a game.")]
    public async Task ShowWelcomeByGameIfOrName(CommandContext ctx, 
        [Description("Game name or id to show welcome message for. If left empty, will use current channel's game.")]
        [RemainingText] string? gameIdOrName = null) {
        try {
            Game? game;
            if (string.IsNullOrEmpty(gameIdOrName)) {
                game = await Database.GetGameByChannel(ctx.Channel.Id);
                if (game == null) {
                    ctx.TimedMessageAsync("This channel is not assigned to any game.").Forget();
                    return;
                }
            }
            else {
                game = await ResolveGame(ctx, gameIdOrName);
                if (game == null) {
                    ctx.TimedMessageAsync("Game not found.").Forget();
                    return;
                }
            }

            if (string.IsNullOrEmpty(game.WelcomeMessage)) {
                ctx.TimedMessageAsync($"No welcome message set for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"**Welcome Message for {game.Name}:**\n{game.WelcomeMessage}",
                    Constants.ListResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowWelcome.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("assignchannel")]
    [Description("Assigns the current channel to a game.")]
    public async Task AssignChannel(CommandContext ctx, 
        [Description("Game name or id to assign to current channel.")]
        [RemainingText] string gameIdOrName) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can assign channels.").Forget();
                return;
            }

            var game = await ResolveGame(ctx, gameIdOrName);
            if (game == null) {
                ctx.TimedMessageAsync("Game not found.").Forget();
                return;
            }

            var existingGame = await Database.GetGameByChannel(ctx.Channel.Id);
            if (existingGame != null) {
                ctx.TimedMessageAsync(
                    $"This channel is already assigned to game '{existingGame.Name}'. Unassign it first.").Forget();
                return;
            }

            bool success = await Database.AssignChannelToGame(ctx.Channel.Id, game.Id);
            if (success) {
                ctx.TimedMessageAsync($"Channel assigned to game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Failed to assign channel. It might already be assigned.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error AssignChannel.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("unassignchannel")]
    [Description("Unassigns a channel from its game.")]
    public async Task UnassignChannel(CommandContext ctx, 
        [Description("Channel to unassign. If left empty, will unassign the current channel.")]
        DiscordChannel? channel = null) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can unassign channels.", Constants.LongResponseDelay).Forget();
                return;
            }

            ulong channelId = channel?.Id ?? ctx.Channel.Id;
            bool success = await Database.UnassignChannel(channelId);
            if (success) {
                ctx.TimedMessageAsync("Channel unassigned from game.", Constants.LongResponseDelay).Forget();
            }
            else {
                ctx.TimedMessageAsync("Channel was not assigned to any game.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error UnassignChannel.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("showchannelgame")]
    [Description("Shows the game assigned to the current channel.")]
    public async Task ShowChannelGame(CommandContext ctx) {
        try {
            var game = await Database.GetGameByChannel(ctx.Channel.Id);
            if (game == null) {
                ctx.TimedMessageAsync("This channel is not assigned to any game.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"This channel is assigned to game '{game.Name}' (ID: {game.Id}).",
                    Constants.LongResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowChannelGame.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("list")]
    [Description("Lists all games.")]
    public async Task ListGames(CommandContext ctx) {
        try {
            var games = await Database.ListGames(ctx.Guild!.Id);
            if (games.Count == 0) {
                ctx.TimedMessageAsync("No games found.").Forget();
                return;
            }

            var sb = new StringBuilder("**Games:**\n");
            foreach (var game in games) {
                sb.AppendLine($"- {game.Name} (ID: {game.Id})");
            }

            ctx.TimedMessageAsync(sb.ToString(), Constants.ListResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ListGames.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("describegame")]
    [Description("Shows game description.")]
    public async Task DescribeGame(CommandContext ctx, 
        [Description("Game name or id to describe. If left empty, will describe channel game.")]
        [RemainingText] string? gameIdOrName = null) {
        try {
            Game? game;
            if (string.IsNullOrEmpty(gameIdOrName)) {
                game = await Database.GetGameByChannel(ctx.Channel.Id);
                if (game == null) {
                    ctx.TimedMessageAsync("This channel is not assigned to any game.").Forget();
                    return;
                }
            }
            else {
                game = await ResolveGame(ctx, gameIdOrName);
                if (game == null) {
                    ctx.TimedMessageAsync("Game not found.").Forget();
                    return;
                }
            }

            var sb = new StringBuilder($"**Game: {game.Name}** (ID: {game.Id})\n");
            sb.AppendLine($"**Description:** {game.Description ?? "No description set."}");
            ctx.TimedMessageAsync(sb.ToString(), Constants.ListResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error DescribeGame.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("delete")]
    [Description("Deletes a game.")]
    public async Task DeleteGame(CommandContext ctx, 
        [Description("Game name or id to remove from the list of games.")]
        [RemainingText] string gameIdOrName) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can delete games.").Forget();
                return;
            }

            var game = await ResolveGame(ctx, gameIdOrName);
            if (game == null) {
                ctx.TimedMessageAsync("Game not found.").Forget();
                return;
            }

            var interactivity = ctx.Client.ServiceProvider.GetRequiredService<InteractivityExtension>();

            await ctx.RespondAsync($"Are you sure you want to remove game '{game!.Name}'? Type `yes` to confirm.");
            var response = await interactivity.WaitForMessageAsync(x => x.Author is not null && x.Author.Id == ctx.User.Id, TimeSpan.FromSeconds(30));

            if (!response.TimedOut && response.Result.Content.ToLower() == "yes") {
                await Database.DeleteGame(game.Id);
                ctx.TimedMessageAsync($"Game '{game.Name}' deleted.").Forget();
            }
            else {
                ctx.TimedMessageAsync("Action cancelled or timed out. No game removed.").Forget();
            }

            await response.Result.SafeDeleteAsync();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error DeleteGame.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("listchannels")]
    [Description("Lists all channels assigned to a game.")]
    public async Task ListChannels(CommandContext ctx,  [RemainingText] string gameIdOrName) {
        try {
            var game = await ResolveGame(ctx, gameIdOrName);
            if (game == null) {
                ctx.TimedMessageAsync("Game not found.").Forget();
                return;
            }

            var channelIds = await Database.GetChannelsByGame(game.Id);
            if (channelIds.Count == 0) {
                ctx.TimedMessageAsync($"No channels assigned to game '{game.Name}'.").Forget();
                return;
            }

            var channelNames = new List<string>();
            foreach (var id in channelIds) {
                var channel = ctx.Guild!.Roles.Values.SelectMany(r => ctx.Guild.Channels.Values).FirstOrDefault(c => c.Id == id) ?? ctx.Guild.Channels.Values.FirstOrDefault(c => c.Id == id);
                channelNames.Add(channel?.Mention ?? $"Unknown Channel ({id})");
            }

            ctx.TimedMessageAsync($"**Channels assigned to {game.Name}:**\n{string.Join(", ", channelNames)}",
                Constants.ListResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ListChannels.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("unassignallchannels")]
    [Description("Unassigns all channels from a game.")]
    public async Task UnassignAllChannels(CommandContext ctx, [RemainingText] string gameIdOrName) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only administrators can unassign channels.").Forget();
                return;
            }

            var game = await ResolveGame(ctx, gameIdOrName);
            if (game == null) {
                ctx.TimedMessageAsync("Game not found.").Forget();
                return;
            }

            int count = await Database.UnassignAllChannelsFromGame(game.Id);
            ctx.TimedMessageAsync($"Unassigned {count} channels from game '{game.Name}'.").Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error UnassignAllChannels.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
}