using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DiscordPantheonGuildBot.Data;
using DiscordPantheonGuildBot.Models;
using System.Globalization;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace DiscordPantheonGuildBot.Commands;

[Command("config")]
[Description("Commands to configure the bot for this server.")]
public class ConfigCommands {
    public DatabaseService Database { get; }
    private ILogger<GameCommands> Logger { get; set; }

    public ConfigCommands(DatabaseService database, ILogger<GameCommands> logger) {
        Database = database;
        Logger = logger;
    }

    private async Task<bool> IsAuthorized(CommandContext ctx) {
        if (ctx.Guild!.OwnerId == ctx.User.Id) return true;
        if (ctx.Member!.Permissions.HasPermission(DiscordPermission.Administrator)) return true;
        return false;
    }

    private async Task<(bool, Game?)> EnsureGame(CommandContext ctx) {
        var game = await Database.GetGameByChannel(ctx.Channel.Id);
        if (game == null) {
            ctx.TimedMessageAsync("This channel is not assigned to any game. Use `!game assignchannel <name>` first.",
                Constants.LongResponseDelay).Forget();
            return (false, null);
        }

        return (true, game);
    }

    [Command("addclass")]
    [Description("Adds a new allowed class to the game.")]
    public async Task AddClass(
        CommandContext ctx,         
        [Description("Name of the class to add to list of approved classes")]
        string className) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure allowed classes.")
                    .Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            string formattedName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(className.ToLower());
            bool success = await Database.AddAllowedClass(game!.Id, formattedName);
            if (success) {
                ctx.TimedMessageAsync($"Added '{formattedName}' to the list of allowed classes for game '{game.Name}'.")
                    .Forget();
            }
            else {
                ctx.TimedMessageAsync($"'{formattedName}' is already an allowed class or an error occurred.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error AddClass.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("removeclass")]
    [Description("Removes an allowed class from the game.")]
    public async Task RemoveClass(CommandContext ctx, 
        [Description("Name of the class to remove from the list of approved classes")]
        string className) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure allowed classes.")
                    .Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            string formattedName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(className.ToLower());
            bool success = await Database.RemoveAllowedClass(game!.Id, formattedName);
            if (success) {
                ctx.TimedMessageAsync(
                    $"Removed '{formattedName}' from the list of allowed classes for game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"'{formattedName}' was not found in the list of allowed classes.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error RemoveClass.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("listclasses")]
    [Description("Lists all allowed classes for this game.")]
    public async Task ListClasses(CommandContext ctx) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            var classes = await Database.GetAllowedClasses(game!.Id);
            ctx.TimedMessageAsync($"**Allowed Classes for {game.Name}:**\n{string.Join(", ", classes)}",
                Constants.ListResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ListClasses.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("clearclasses")]
    [Description("Removes all allowed classes for this game.")]
    public async Task ClearClasses(CommandContext ctx) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure allowed classes.")
                    .Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            int count = await Database.ClearAllowedClasses(game!.Id);
            ctx.TimedMessageAsync($"Removed all {count} allowed classes from game '{game.Name}'.").Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ClearClasses.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("resetclasses")]
    [Description("Resets allowed classes to the default list.")]
    public async Task ResetClasses(CommandContext ctx) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure allowed classes.")
                    .Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            await Database.ResetAllowedClasses(game!.Id);
            ctx.TimedMessageAsync($"Reset allowed classes to defaults for game '{game.Name}'.").Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ResetClasses.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("maprole")]
    [Description("Maps a Discord role to an internal status (member, officer, leader, raid-leader).")]
    public async Task MapRole(CommandContext ctx,         
        [Description("Discord role to map to an internal game status or role")]
        DiscordRole role, 
        [Description("Internal game status or role to map to Discord role")]
        string status) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure role mappings.").Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            string normalizedStatus = status.ToLower();
            if (normalizedStatus != "member" && normalizedStatus != "officer" && normalizedStatus != "leader" &&
                normalizedStatus != "raid-leader") {
                ctx.TimedMessageAsync(
                    "Invalid status. Possible statuses are: member, officer, leader, raid-leader. Note, raid-leader is also any one of the others at the same time.",
                    Constants.LongResponseDelay).Forget();
                return;
            }

            bool success = await Database.SetRoleMapping(game!.Id, role.Id, normalizedStatus);
            if (success) {
                ctx.TimedMessageAsync($"Mapped role {role.Name} to status '{normalizedStatus}' for game '{game.Name}'.")
                    .Forget();
            }
            else {
                ctx.TimedMessageAsync($"Failed to map role {role.Name}.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error MapRole.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("unmaprole")]
    [Description("Removes a Discord role mapping.")]
    public async Task UnmapRole(CommandContext ctx, 
        [Description("Discord role to remove from list of mapped roles")]
        DiscordRole role) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure role mappings.").Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            bool success = await Database.RemoveRoleMapping(game!.Id, role.Id);
            if (success) {
                ctx.TimedMessageAsync($"Removed mapping for role {role.Name} in game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync($"Role {role.Name} was not mapped.").Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error UnmapRole.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("unmapallroles")]
    [Description("Removes all role mappings for this game.")]
    public async Task UnmapAllRoles(CommandContext ctx) {
        try {
            if (!await IsAuthorized(ctx)) {
                ctx.TimedMessageAsync("Only the server owner or administrators can configure role mappings.").Forget();
                return;
            }

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            int count = await Database.ClearRoleMappings(game!.Id);
            ctx.TimedMessageAsync($"Removed all {count} role mappings for game '{game.Name}'.").Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error UnmapAllRoles.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("listroles")]
    [Description("Lists all role mappings for this game.")]
    public async Task ListRoles(CommandContext ctx) {
        try {
            if (ctx.Guild is null) return;

            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            var mappings = await Database.GetRoleMappings(game!.Id);
            if (mappings.Count == 0) {
                ctx.TimedMessageAsync($"No roles are currently mapped for game '{game.Name}'.").Forget();
                return;
            }

            var mappingList = new List<string>();
            foreach (var mapping in mappings) {
                var role = ctx.Guild.Roles.Values.FirstOrDefault(r => r.Id == mapping.Key);
                string roleName = role?.Name ?? $"Unknown Role ({mapping.Key})";
                mappingList.Add($"{roleName}: {mapping.Value}");
            }

            ctx.TimedMessageAsync($"**Role Mappings for {game.Name}:**\n{string.Join("\n", mappingList)}",
                Constants.ListResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ListRoles.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
}