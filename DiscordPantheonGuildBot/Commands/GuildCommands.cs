using System.ComponentModel;
using System.Text;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DiscordPantheonGuildBot.Data;
using DiscordPantheonGuildBot.Models;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordPantheonGuildBot.Commands;

public class GuildCommands {
    public DatabaseService Database { get; }
    private ILogger<GameCommands> Logger { get; set; }

    public GuildCommands(DatabaseService database, ILogger<GameCommands> logger) {
        Database = database;
        Logger = logger;
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

    private async Task<bool> IsAuthorized(CommandContext ctx, string requiredStatus, int gameId) {
        if (ctx.Guild!.OwnerId == ctx.User.Id) return true;
        if ((ctx.Member!.Permissions & DiscordPermission.Administrator) == DiscordPermission.Administrator) return true;

        var roleMappings = await Database.GetRoleMappings(gameId);
        var userRoles = ctx.Member.Roles.Select(r => r.Id).ToList();

        // Check internal status first
        var status = await Database.GetUserStatus(ctx.Guild.Id, ctx.User.Id);

        // If no internal status, check Discord roles
        if (string.IsNullOrEmpty(status)) {
            if (roleMappings.Count > 0) {
                var matchingMappings = roleMappings.Where(m => userRoles.Contains(m.Key)).ToList();

                if (matchingMappings.Any()) {
                    // Get highest privilege status among matching roles
                    var statuses = matchingMappings.Select(m => m.Value).ToList();
                    if (statuses.Contains("leader")) status = "leader";
                    else if (statuses.Contains("officer")) status = "officer";
                    else if (statuses.Contains("member")) status = "member";
                }
            }
        }

        if (string.IsNullOrEmpty(status)) return false;

        return requiredStatus.ToLower() switch {
            "leader" => status == "leader",
            "officer" => status == "leader" || status == "officer",
            "member" => status == "leader" || status == "officer" || status == "member",
            _ => false
        };
    }

    [Command("canconnect")]
    [Description("Checks that the backend is functioning properly.")]
    public async Task CanConnect(CommandContext ctx) {
        try {
            if (ctx.Member is null) return;

            if (!await Database.CanConnect()) {
                ctx.TimedMessageAsync(
                        $"This is not my day. I can't seem to find any of my tools. I'm sorry {ctx.Member.MemberServerName()}, but I won't be able to help right now.")
                    .Forget();
                return;
            }

            ctx.TimedMessageAsync($"Yes, my tools are available at the moment. What is it that you need?").Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error CanConnect.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("hello")]
    [Description("Checks that the bot is responding.")]
    public async Task Hello(CommandContext ctx) {
        await Ping(ctx);
    }

    [Command("ping")]
    [Description("Checks that the bot is responding.")]
    public async Task Ping(CommandContext ctx) {
        try {
            if (ctx.Guild is null) return;
            if (ctx.Member is null) return;

            if (!await Database.CanConnect()) {
                ctx.TimedMessageAsync(
                        $"This is not my day. I can't seem to find any of my tools. I'm sorry {ctx.Member.MemberServerName()}, but I won't be able to help until I find them.")
                    .Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) {
                ctx.TimedMessageAsync(
                        $"I'm here, but without knowing the game we are playing, I cannot manage a roster. Someone needs to tell me what game we are playing. !game assignchannel <gameIdOrName>")
                    .Forget();
                return;
            }

            ctx.TimedMessageAsync(
                    $"Cough, ack... Yes? Yes! Oh, sorry, its you. Yes, yes, I'm here. I'm awake. I serve {ctx.Guild.Name} in managing a roster for {game!.Name}. At your service, {ctx.Member.MemberServerName()}.")
                .Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error Ping.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !add Synn Wizard 11
    [Command("add")]
    [Description("Adds a character to your user. Ex. !add Jack Warrior 11")]
    public async Task AddCharacter(CommandContext ctx,
            [RemainingText] string args)
        // [Description("The name of the character.")]
        // string name,
        // [Description("The class of the character.")]
        // string? @class = null,
        // [Description("The level of the character.")]
        // int level = 1) 
    {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            int? level = null;
            if (!await IsAuthorized(ctx, "member", game!.Id)) {
                await ctx.TimedMessageAsync("You must be a member to add characters.");
                return;
            }

            string? @class = null;

            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (int.TryParse(parts.Last(), out _)) {
                level = int.Parse(parts.Last());
                parts.RemoveAt(parts.Count - 1);
            }

            args = string.Join(' ', parts);
            string nameArgs = args;

            var allowedClasses = await Database.GetAllowedClasses(game.Id);
            allowedClasses = allowedClasses.OrderByDescending(c => c.Length).ToList();

            foreach (var allowedClass in allowedClasses) {
                if (args.EndsWith(allowedClass, StringComparison.OrdinalIgnoreCase)) {
                    @class = allowedClass;
                    // Ensure there's a space before the class name or it's the start of the string
                    int index = args.LastIndexOf(allowedClass, StringComparison.OrdinalIgnoreCase);
                    if (index == 0 || args[index - 1] == ' ') {
                        nameArgs = args.Substring(0, index).Trim();
                        break;
                    }

                    @class = null; // Reset if it was a false match
                }
            }

            var name = nameArgs.Trim();

            // If @class is a number, it means the class was omitted and the number is the level
            if (!string.IsNullOrEmpty(@class) && int.TryParse(@class, out int parsedLevel)) {
                level = parsedLevel;
                @class = null;
            }

            // if (!string.IsNullOrEmpty(@class)) {
            //     var allowedClasses = await Database.GetAllowedClasses(game.Id);
            //     var matchingClass =
            //         allowedClasses.FirstOrDefault(c => string.Equals(c, @class, StringComparison.OrdinalIgnoreCase));
            //
            //     if (matchingClass == null) {
            //         ctx.TimedMessageAsync(
            //             $"'{@class}' is not an allowed class for game '{game.Name}'. Use `!config listclasses` to see allowed classes.",
            //             Constants.LongResponseDelay).Forget();
            //         return;
            //     }
            //
            //     @class = matchingClass; // Use the properly cased version from DB
            // }

            if (!level.HasValue) level = 1;
            var character = new Character {
                GuildId = ctx.Guild!.Id,
                GameId = game.Id,
                UserId = ctx.User.Id,
                CharacterName = name,
                Class = @class,
                Level = level ?? 1
            };

            // Check if this is their first character in this game
            var existingCharacters = await Database.GetUserCharacters(game.Id, ctx.User.Id);
            var isFirstCharacter = existingCharacters.Count == 0;

            var success = await Database.AddCharacter(character);
            if (success) {
                await SetRoster(ctx);
                if (isFirstCharacter && !string.IsNullOrEmpty(game.WelcomeMessage)) {
                    ctx.TimedMessageAsync(
                            $"{game.WelcomeMessage}\n\nRoster updated for {name} ({@class ?? "No Class"}, Level {level}).")
                        .Forget();
                }
                else {
                    ctx.TimedMessageAsync($"Roster updated for {name} ({@class ?? "No Class"}, Level {level}) .")
                        .Forget();
                }
            }
            else {
                ctx.TimedMessageAsync(
                    $"We encountered a problem adding or updating {name} in '{game.Name}'.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error AddCharacter.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !update Synn Wizard 11
    [Command("update")]
    [Description("Updates a character for your user. Will also Add if not found.")]
    public async Task UpdateCharacter(CommandContext ctx,
        [RemainingText] string args
        // [Description("The name of the character.")]
        // string name,
        // [Description("The class of the character.")]
        // string? @class = null,
        // [Description("The level of the character.")]
        // int level = 1
    ) {
        await AddCharacter(ctx, args);
        //await AddCharacter(ctx, name, @class, level);
        // try {
        //     var (hasGame, game) = await EnsureGame(ctx);
        //     if (!hasGame) return;
        //
        //     if (!await IsAuthorized(ctx, "member", game!.Id)) {
        //         await ctx.RespondAsync("You must be a member to add characters.");
        //         return;
        //     }
        //
        //     // If @class is a number, it means the class was omitted and the number is the level
        //     if (!string.IsNullOrEmpty(@class) && int.TryParse(@class, out int parsedLevel)) {
        //         level = parsedLevel;
        //         @class = null;
        //     }
        //
        //     if (!string.IsNullOrEmpty(@class)) {
        //         var allowedClasses = await Database.GetAllowedClasses(game.Id);
        //         var matchingClass =
        //             allowedClasses.FirstOrDefault(c => string.Equals(c, @class, StringComparison.OrdinalIgnoreCase));
        //
        //         if (matchingClass == null) {
        //             ctx.TimedMessageAsync(
        //                 $"'{@class}' is not an allowed class for game '{game.Name}'. Use `!config listclasses` to see allowed classes.",
        //                 Constants.LongResponseDelay).Forget();
        //             return;
        //         }
        //
        //         @class = matchingClass; // Use the properly cased version from DB
        //     }
        //
        //     var character = new Character {
        //         GuildId = ctx.Guild.Id,
        //         GameId = game.Id,
        //         UserId = ctx.User.Id,
        //         CharacterName = name,
        //         Class = @class,
        //         Level = level
        //     };
        //
        //     // Check if this is their first character in this game
        //     var existingCharacters = await Database.GetUserCharacters(game.Id, ctx.User.Id);
        //     bool isFirstCharacter = existingCharacters.Count == 0;
        //
        //     bool success = await Database.AddCharacter(character);
        //     if (success) {
        //         await SetRoster(ctx);
        //         if (isFirstCharacter && !string.IsNullOrEmpty(game.WelcomeMessage)) {
        //             ctx.TimedMessageAsync(
        //                 $"{game.WelcomeMessage}\n\nRoster updated for {name} ({@class ?? "No Class"}, Level {level}).",
        //                 Constants.LongResponseDelay).Forget();
        //         }
        //         else {
        //             ctx.TimedMessageAsync($"Roster updated for {name} ({@class ?? "No Class"}, Level {level}) .").Forget();
        //         }
        //     }
        //     else {
        //         ctx.TimedMessageAsync(
        //             $"Character {name} already exists for you in '{game.Name}', or an error occurred.").Forget();
        //     }
        // }
        // catch (Exception ex) {
        //     _logger.LogError(ex, "Error AddCharacter.");
        // }
        // finally {
        //     if (ctx is TextCommandContext textCtx) {
        //         await textCtx.Message.SafeDeleteAsync();
        //     }
        // }
    }

    // !level Synn 11
    [Command("level")]
    [Description("Changes a character's level. Ex. !level Synn 11")]
    public async Task ChangeLevel(CommandContext ctx,
        [Description("The character name and new level (e.g., !level Synn 11 or !level Great Name 11)")] [RemainingText]
        string args) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count < 2) {
                ctx.TimedMessageAsync(
                    "Invalid format. Expected: `!level <name> <level>` (e.g., `!level Synn 11`)",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            if (!int.TryParse(parts.Last(), out int level) || level <= 0) {
                ctx.TimedMessageAsync(
                    "Invalid level. Level must be a positive number at the end of the command.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            parts.RemoveAt(parts.Count - 1);
            string name = string.Join(' ', parts);

            var success = await Database.UpdateCharacterLevel(game!.Id, ctx.User.Id, name, level);

            if (success) {
                await SetRoster(ctx);
                ctx.TimedMessageAsync($"Changed {name}'s level to {level} in game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"Character {name} not found for your user in game '{game.Name}'.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ChangeLevel.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("describe")]
    [Description("Shows game description.")]
    public async Task DescribeGame(CommandContext ctx) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);

            if (!hasGame || game is null) {
                ctx.TimedMessageAsync("This channel is not assigned to any game.").Forget();
                return;
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

    // !remove Synn or !remove
    [Command("remove")]
    [Description("Removes one or all of your characters for the current game.")]
    public async Task RemoveCharacter(CommandContext ctx,
        [Description("The name of the character to remove. If omitted, prompts to remove all characters.")]
        [RemainingText]
        string? name = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            var interactivity = ctx.Client.ServiceProvider.GetRequiredService<InteractivityExtension>();

            if (string.IsNullOrEmpty(name)) {
                var builder = new DiscordMessageBuilder()
                    .WithContent(
                        $"Are you sure you want to remove ALL your characters in game '{game!.Name}'? Type `yes` to confirm."); // Specify who to mention

                await ctx.RespondAsync(builder);
                var challenge = await ctx.GetResponseAsync();

                var response =
                    await interactivity.WaitForMessageAsync(x => x.Author is not null && x.Author.Id == ctx.User.Id,
                        TimeSpan.FromSeconds(30));

                if (!response.TimedOut && response.Result.Content.ToLower() == "yes") {
                    if (challenge is not null) await challenge.DeleteAsync();
                    var count = await Database.RemoveAllCharacters(game.Id, ctx.User.Id);
                    ctx.TimedMessageAsync(
                        $"Removed all {count} characters for you in game '{game.Name}'.").Forget();
                }
                else {
                    if (challenge is not null) await challenge.DeleteAsync();
                    ctx.TimedMessageAsync("Action cancelled or timed out. No characters removed.").Forget();
                }

                await response.Result.DeleteAsync();
            }
            else {
                // Create a builder to specify mentions
                var builder = new DiscordMessageBuilder()
                    .WithContent(
                        $"Are you sure you want to remove character {name} in game '{game!.Name}'? Type `yes` to confirm."); // Specify who to mention

                await ctx.RespondAsync(builder);
                var challenge = await ctx.GetResponseAsync();
                var response =
                    await interactivity.WaitForMessageAsync(x => x.Author is not null && x.Author.Id == ctx.User.Id,
                        TimeSpan.FromSeconds(30));

                if (!response.TimedOut && response.Result.Content.ToLower() == "yes") {
                    if (challenge is not null) await challenge.DeleteAsync();
                    var success = await Database.RemoveCharacter(game.Id, ctx.User.Id, name);
                    if (success) {
                        await SetRoster(ctx);
                        ctx.TimedMessageAsync(
                            $"Removed character {name} for you in game '{game.Name}'.").Forget();
                    }
                    else {
                        ctx.TimedMessageAsync($"Character {name} not found in game '{game.Name}'.").Forget();
                    }
                }
                else {
                    if (challenge is not null) await challenge.DeleteAsync();
                    ctx.TimedMessageAsync("Action cancelled or timed out. No characters removed.").Forget();
                }

                await response.Result.DeleteAsync();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error RemoveCharacter.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !list or !list Synn
    [Command("list")]
    [Description("Lists your characters or characters with a specific name for the current game.")]
    public async Task ListCharacters(CommandContext ctx,
        [Description("The name of the character to search for. If omitted, lists all your characters.")] [RemainingText]
        string? name = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (string.IsNullOrEmpty(name)) {
                var characters = await Database.GetUserCharacters(game!.Id, ctx.User.Id);
                if (characters.Count == 0) {
                    ctx.TimedMessageAsync($"You have no characters in game '{game.Name}'.").Forget();
                    return;
                }

                var characterList = string.Join("\n",
                    characters.Select(c => $"- {c.CharacterName}: {c.Class ?? "No Class"} (Level {c.Level})"));
                ctx.TimedMessageAsync($"**Your Characters in {game.Name}:**\n{characterList}",
                    Constants.ListResponseDelay).Forget();
            }
            else {
                var characters = await Database.GetCharactersByName(game!.Id, name);
                if (characters.Count == 0) {
                    ctx.TimedMessageAsync($"No characters found named {name} in game '{game.Name}'.").Forget();
                    return;
                }

                var characterList = string.Join("\n",
                    characters.Select(c =>
                        $"- {c.CharacterName}: {c.Class ?? "No Class"} (Level {c.Level}) [User ID: {c.UserId}]"));
                ctx.TimedMessageAsync($"**Characters named {name} in {game.Name}:**\n{characterList}",
                    Constants.ListResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ListCharacters.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !roster, !roster class, !roster class Cleric, !roster level, !roster level 10-20
    [Command("setroster")]
    [Description("Shows the guild roster with various filters and ordering for the current game.")]
    public async Task ShowRoster(CommandContext ctx) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "leader", game!.Id)) {
                ctx.TimedMessageAsync("You must be a leader to clean the roster.").Forget();
                return;
            }

            await SetRoster(ctx);
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowRoster.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !roster, !roster class, !roster class Cleric, !roster level, !roster level 10-20
    [Command("roster")]
    [Description("Shows the guild roster with various filters and ordering for the current game.")]
    public async Task ShowFilteredRoster(CommandContext ctx,
        [Description("The filter to apply (e.g., 'class', 'level').")]
        string? filter = null,
        [Description("The value for the filter (e.g., 'Cleric', '10-20').")]
        string? value = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            string? orderBy = null;
            string? filterClass = null;
            int? minLevel = null;
            int? maxLevel = null;

            if (filter == "class") {
                orderBy = "class";
                filterClass = value;
            }
            else if (filter == "level") {
                orderBy = "level";
                if (!string.IsNullOrEmpty(value) && value.Contains("-")) {
                    var parts = value.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int min) &&
                        int.TryParse(parts[1], out int max)) {
                        minLevel = min;
                        maxLevel = max;
                    }
                }
            }

            var roster = await Database.GetRoster(game!.Id, orderBy, filterClass, minLevel, maxLevel);
            if (roster.Count == 0) {
                ctx.TimedMessageAsync($"Roster is empty for game '{game.Name}'.").Forget();
                return;
            }

            var guild = ctx.Guild;

            var all = guild!.GetAllMembersAsync();
            var members = await all.Select(m => m).ToListAsync();

            var rosterLines = FormatRosterText(roster, members)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var line in rosterLines) {
                sb.AppendLine(line);

                if (sb.Length > (1900 - $"**Guild Roster for {game.Name}".Count())) {
                    var embed = new DiscordEmbedBuilder()
                        .WithTitle($"**Guild Roster for {game.Name}")
                        .WithDescription(sb.ToString()) // Show the first 10, for example
                        .WithColor(DiscordColor.CornflowerBlue)
                        .Build();
                    var content = new DiscordMessageBuilder().AddEmbed(embed);

                    ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
                    sb.Clear();
                }
            }

            if (sb.Length > 0) {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"**Guild Roster for {game.Name}")
                    .WithDescription(sb.ToString()) // Show the first 10, for example
                    .WithColor(DiscordColor.CornflowerBlue)
                    .Build();
                var content = new DiscordMessageBuilder().AddEmbed(embed);

                ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowFilteredRoster.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !roster, !roster class, !roster class Cleric, !roster level, !roster level 10-20
    [Command("levelroster")]
    [Description("Shows the guild roster for the current game filtered by level.")]
    public async Task ShowFilteredRosterByLevel(CommandContext ctx,
        [Description("The value for the filter (e.g., '10-20').")]
        string? value = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            string? filterClass = null;
            int? minLevel = null;
            int? maxLevel = null;

            var orderBy = "level";
            if (!string.IsNullOrEmpty(value) && value.Contains("-")) {
                var parts = value.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int min) &&
                    int.TryParse(parts[1], out int max)) {
                    minLevel = min;
                    maxLevel = max;
                }
            }

            var roster = await Database.GetRoster(game!.Id, orderBy, filterClass, minLevel, maxLevel);
            if (roster.Count == 0) {
                ctx.TimedMessageAsync($"Roster is empty for game '{game.Name}'.").Forget();
                return;
            }

            var guild = ctx.Guild;

            var all = guild!.GetAllMembersAsync();
            var members = await all.Select(m => m).ToListAsync();

            var rosterLines = FormatRosterText(roster, members)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var line in rosterLines) {
                sb.AppendLine(line);

                if (sb.Length > (1900 - $"**Guild Roster for {game.Name}".Count())) {
                    var embed = new DiscordEmbedBuilder()
                        .WithTitle($"**Guild Roster for {game.Name}")
                        .WithDescription(sb.ToString()) // Show the first 10, for example
                        .WithColor(DiscordColor.CornflowerBlue)
                        .Build();
                    var content = new DiscordMessageBuilder().AddEmbed(embed);

                    ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
                    sb.Clear();
                }
            }

            if (sb.Length > 0) {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"**Guild Roster for {game.Name}")
                    .WithDescription(sb.ToString()) // Show the first 10, for example
                    .WithColor(DiscordColor.CornflowerBlue)
                    .Build();
                var content = new DiscordMessageBuilder().AddEmbed(embed);

                ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowFilteredRoster.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !roster, !roster class, !roster class Cleric, !roster level, !roster level 10-20
    [Command("classroster")]
    [Description("Shows the guild roster for the current game filtered by class.")]
    public async Task ShowFilteredRosterByClass(CommandContext ctx,
        [Description("The value for the filter (e.g., 'Cleric').")] [RemainingText]
        string? value = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            int? minLevel = null;
            int? maxLevel = null;

            var orderBy = "class";
            var filterClass = value;

            var roster = await Database.GetRoster(game!.Id, orderBy, filterClass, minLevel, maxLevel);
            if (roster.Count == 0) {
                ctx.TimedMessageAsync($"Roster is empty for game '{game.Name}'.").Forget();
                return;
            }

            var guild = ctx.Guild;

            var all = guild!.GetAllMembersAsync();
            var members = await all.Select(m => m).ToListAsync();

            var rosterLines = FormatRosterText(roster, members)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var line in rosterLines) {
                sb.AppendLine(line);

                if (sb.Length > (1900 - $"**Guild Roster for {game.Name}".Count())) {
                    var embed = new DiscordEmbedBuilder()
                        .WithTitle($"**Guild Roster for {game.Name}")
                        .WithDescription(sb.ToString()) // Show the first 10, for example
                        .WithColor(DiscordColor.CornflowerBlue)
                        .Build();
                    var content = new DiscordMessageBuilder().AddEmbed(embed);

                    ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
                    sb.Clear();
                }
            }

            if (sb.Length > 0) {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"**Guild Roster for {game.Name}")
                    .WithDescription(sb.ToString()) // Show the first 10, for example
                    .WithColor(DiscordColor.CornflowerBlue)
                    .Build();
                var content = new DiscordMessageBuilder().AddEmbed(embed);

                ctx.TimedMessageAsync(content, Constants.ListResponseDelay).Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowFilteredRoster.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    private string FormatRosterText(List<Character> roster, List<DiscordMember> members) {
        var rows = new List<string[]>();
        var columnWidths = new int[4]; // CharacterName, Class, Level, MemberServerName

        foreach (var character in roster) {
            var guildMember = members.SingleOrDefault(m => m.Id == character.UserId);
            var guildmemberLink = guildMember is not null ? guildMember.MemberServerName() : "Unknown";

            var row = new[] {
                character.CharacterName,
                character.Class ?? "No Class",
                character.Level.ToString(),
                guildmemberLink
            };
            rows.Add(row);

            for (int i = 0; i < row.Length; i++) {
                if (row[i].Length > columnWidths[i]) {
                    columnWidths[i] = row[i].Length;
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append("```");
        foreach (var row in rows) {
            for (var i = 0; i < row.Length; i++) {
                if (i < row.Length - 1) {
                    int width = columnWidths[i] + 5;
                    sb.Append(row[i].PadRight(width));
                }
                else {
                    sb.Append(row[i]);
                }
            }

            sb.AppendLine();
        }

        sb.Append("```");
        return sb.ToString();
    }

    private async Task SetRoster(CommandContext ctx) {
        var (hasGame, game) = await EnsureGame(ctx);
        if (!hasGame) return;

        var roster = await Database.GetRoster(game!.Id);
        var guild = ctx.Guild;
        var all = guild!.GetAllMembersAsync();
        var members = await all.Select(m => m).ToListAsync();

        var rosterEntries = new List<RosterEntry>();

        foreach (var character in roster) {
            var guildMember = members.SingleOrDefault(m => m.Id == character.UserId);
            var guildmemberLink = guildMember is not null ? "" + guildMember.MemberServerName() : "Unknown";
            rosterEntries.Add(new RosterEntry() {
                CharacterName = character.CharacterName, Member = guildmemberLink, Class = character.Class ?? "",
                Level = character.Level.ToString()
            });
        }

        var rosterText = FormatRosterText(roster, members);

        if (rosterEntries.Count > 100) {
            rosterText = "Roster is too large to display in a single message. See attached file.";
        }

        // Create the Excel file in a MemoryStream
        using (MemoryStream stream = ExcelHelper.CreateExcelStream(rosterEntries, "Roster")) {
            var embed = new DiscordEmbedBuilder()
                .WithTitle($"**Guild Roster for {game.Name}")
                .WithDescription(rosterText) // Show the first 10, for example
                .WithColor(DiscordColor.CornflowerBlue)
                .Build();
            var content = new DiscordMessageBuilder().AddEmbed(embed);

            var pins = await ctx.Channel.GetPinnedMessagesAsync();
            var found = false;
            foreach (var pin in pins) {
                if (!found && (pin.Content.Contains($"**Guild Roster for {game.Name}") ||
                               pin.Embeds.Any(e =>
                                   !string.IsNullOrWhiteSpace(e.Title) &&
                                   e.Title.Contains($"**Guild Roster for {game.Name}")))) {
                    found = true;
                    await pin.ModifyAsync(content);
                    await pin.UpdatePinnedMessageWithXlsxAndGeneratedTxtFile(stream, "roster.xlsx", content);
                }
                else if ((pin.Content.Contains($"**Guild Roster for {game.Name}") ||
                          pin.Embeds.Any(e =>
                              !string.IsNullOrWhiteSpace(e.Title) &&
                              e.Title.Contains($"**Guild Roster for {game.Name}")))) {
                    //we already found the pinned message and only support one pinned message with the roster
                    await pin.DeleteAsync();
                }
            }

            if (!found) {
                await ctx.RespondAsync(content);
                var message = await ctx.GetResponseAsync();
                if (message is not null) {
                    await message.PinAsync(); // Then pin
                    await message.UpdatePinnedMessageWithXlsxAndGeneratedTxtFile(stream, "roster.xlsx", content);
                }
            }
        }
    }

    // !removeuser $username
    [Command("removeuser")]
    [Description("Removes all characters for a specific user in the current game. Requires leader permissions.")]
    public async Task RemoveUser(CommandContext ctx,
        [Description("The user whose characters should be removed.")]
        DiscordMember member) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "leader", game!.Id)) {
                ctx.TimedMessageAsync("You must be a leader to remove other users' characters.").Forget();
                return;
            }

            var interactivity = ctx.Client.ServiceProvider.GetRequiredService<InteractivityExtension>();
            var builder = new DiscordMessageBuilder()
                .WithContent(
                    $"Are you sure you want to remove ALL characters in game '{game.Name}' for user '{member.MemberServerName()}'? Type `yes` to confirm."); // Specify who to mention

            await ctx.RespondAsync(builder);

            var challenge = await ctx.GetResponseAsync();
            var response =
                await interactivity.WaitForMessageAsync(x => x.Author is not null && x.Author.Id == ctx.User.Id,
                    TimeSpan.FromSeconds(30));

            if (!response.TimedOut && response.Result.Content.ToLower() == "yes") {
                if (challenge is not null) await challenge.DeleteAsync();
                var count = await Database.RemoveUserCharacters(game.Id, member.Id);
                await SetRoster(ctx);
                ctx.TimedMessageAsync(
                    $"Removed all {count} characters for user {member.MemberServerName()} in game '{game.Name}'.",
                    Constants.LongResponseDelay).Forget();
            }
            else {
                if (challenge is not null) await challenge.DeleteAsync();
                ctx.TimedMessageAsync("Action cancelled or timed out. No characters removed.").Forget();
            }

            await response.Result.DeleteAsync();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error RemoveUser.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    // !cleanroster
    [Command("cleanroster")]
    [Description("Removes all characters not flagged as members in the current game. Requires leader role.")]
    public async Task CleanRoster(CommandContext ctx) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "leader", game!.Id)) {
                ctx.TimedMessageAsync("You must be a leader to clean the roster.").Forget();
                return;
            }

            var members = ctx.Guild!.GetAllMembersAsync();
            var memberIds = await members.Select(m => m.Id).ToListAsync();

            int count = await Database.CleanRoster(game.Id, memberIds);
            await SetRoster(ctx);
            ctx.TimedMessageAsync(
                $"Cleaned roster for game '{game.Name}'. Removed {count} characters whose users are no longer members.",
                Constants.LongResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error CleanRoster.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("promote")]
    [Description("Promotes or demotes a user. Requires officer permissions.")]
    public async Task PromoteUser(CommandContext ctx,
        [Description("The user to promote or demote.")]
        DiscordMember member,
        [Description("The role to assign (member, officer, leader).")]
        string role) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "officer", game!.Id)) {
                ctx.TimedMessageAsync("You must be an officer to promote or demote users.").Forget();
                return;
            }

            string normalizedRole = role.ToLower();
            if (normalizedRole != "member" && normalizedRole != "officer" && normalizedRole != "leader") {
                ctx.TimedMessageAsync("Invalid role. Possible roles are: member, officer, leader.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            // Only leaders can promote to leader or demote leaders (except guild owner)
            if (normalizedRole == "leader" && !await IsAuthorized(ctx, "leader", game.Id)) {
                ctx.TimedMessageAsync("Only leaders can promote someone to leader.").Forget();
                return;
            }

            var targetStatus = await Database.GetUserStatus(ctx.Guild!.Id, member.Id);
            if (targetStatus == "leader" && !await IsAuthorized(ctx, "leader", game.Id)) {
                ctx.TimedMessageAsync("Only leaders can demote other leaders.").Forget();
                return;
            }

            bool success = await Database.SetUserStatus(ctx.Guild.Id, member.Id, normalizedRole);
            if (success) {
                await SetRoster(ctx);
                ctx.TimedMessageAsync($"Updated {member.MemberServerName()} status to {normalizedRole}.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"Failed to update {member.MemberServerName()} status.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error PromoteUser.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
}