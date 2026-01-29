using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DiscordPantheonGuildBot.Data;
using DiscordPantheonGuildBot.Models;
using System.Text;
using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using Microsoft.Extensions.Logging;

namespace DiscordPantheonGuildBot.Commands;

[Command("attendance")]
[Description("Commands for logging and listing event attendance.")]
public class AttendanceCommands {
    public DatabaseService Database { get;  }
    private ILogger<GameCommands> Logger { get; set; }

    public AttendanceCommands(DatabaseService database, ILogger<GameCommands> logger) {
        Database = database;
        Logger = logger;
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

    private async Task<bool> IsAuthorized(CommandContext ctx, string requiredStatus, int gameId) {
        if (ctx.Guild!.OwnerId == ctx.User.Id) return true;
        if (ctx.Member!.Permissions.HasPermission(DiscordPermission.Administrator)) return true;

        var roleMappings = await Database.GetRoleMappings(gameId);
        var userRoles = ctx.Member.Roles.Select(r => r.Id).ToList();

        // Check for raid-leader role mapping specifically as requested for additional authorization
        if (roleMappings.Any(m => m.Value == "raid-leader" && userRoles.Contains(m.Key))) {
            return true;
        }

        var status = await Database.GetUserStatus(ctx.Guild.Id, ctx.User.Id);

        if (string.IsNullOrEmpty(status)) {
            if (roleMappings.Count > 0) {
                var matchingMappings = roleMappings.Where(m => userRoles.Contains(m.Key)).ToList();

                if (matchingMappings.Any()) {
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

    [Command("log")]
    [Description("Logs attendance for everyone online, in voice, or in a specific voice channel.")]
    public async Task LogAttendance(CommandContext ctx,
        [Description("Scope: online, voice, or channel")]
        string scope,
        [Description("Name for the attendance record")]
        [RemainingText] string name,
        [Description("Voice channel (if scope is channel)")]
        DiscordChannel? channel = null) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "officer", game!.Id)) {
                ctx.TimedMessageAsync("Only officers, leaders, and raid leaders can can log attendance.").Forget();
                return;
            }

            List<ulong> userIds = new List<ulong>();
            scope = scope.ToLower();

            // Handle if someone swaps scope and name? No, we follow the new order.
            // But what if they wanted to use channel?
            // CommandContext handles DiscordChannel if it's a mention.

            switch (scope) {
                case "online":
                    await foreach (var m in ctx.Guild!.GetAllMembersAsync()) {
                        if (m.Presence != null && m.Presence.Status != DiscordUserStatus.Offline) {
                            userIds.Add(m.Id);
                        }
                    }

                    break;
                case "voice":
                    userIds = ctx.Guild!.Channels.Values
                        .Where(c => c.Type == DiscordChannelType.Voice)
                        .SelectMany(c => c.Users)
                        .Select(u => u.Id).Distinct().ToList();
                    break;
                case "channel":
                    if (channel is null || channel.Type != DiscordChannelType.Voice) {
                        await ctx.RespondAsync("Please specify a valid voice channel for the 'channel' scope.");
                        return;
                    }

                    userIds = channel.Users.Select(u => u.Id).ToList();
                    break;
                default:
                    await ctx.RespondAsync("Invalid scope. Use: online, voice, or channel.");
                    return;
            }

            if (userIds.Count == 0) {
                ctx.TimedMessageAsync("No members found within the specified scope.").Forget();
                return;
            }

            bool success = await Database.CreateAttendanceSession(game.Id, name, userIds);
            if (success) {
                ctx.TimedMessageAsync(
                    $"Attendance logged for '{name}' in game '{game.Name}'. Total present: {userIds.Count}",
                    Constants.ListResponseDelay).Forget();
            }
            else {
                ctx.TimedMessageAsync(
                    $"Failed to log attendance. Maybe a session with name '{name}' already exists for this game?")
                    .Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error LogAttendance.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("addattendance")]
    [Description("Logs attendance for specific user.")]
    public async Task AddUserAttendance(CommandContext ctx, DiscordMember member,
        [Description("Name for the attendance record")]
        [RemainingText] string name) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "officer", game!.Id)) {
                ctx.TimedMessageAsync("Only officers, leaders, and raid leaders can can log attendance.").Forget();
                return;
            }

            bool success = await Database.AddUserToAttendanceSession(game.Id, name, member.Id);
            if (success) {
                ctx.TimedMessageAsync($"Attendance logged for '{name}' in game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync(
                        $"Failed to log attendance. Maybe a session with name '{name}' does not exist for this game?")
                    .Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error AddUserAttendance.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("removeattendance")]
    [Description("Removes attendance for specific user.")]
    public async Task RemoveUserAttendance(CommandContext ctx, DiscordMember member,
        [Description("Name for the attendance record")]
        [RemainingText] string name) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "officer", game!.Id)) {
                ctx.TimedMessageAsync("Only officers, leaders, and raid leaders can log attendance.").Forget();
                return;
            }

            bool success = await Database.RemoveUserFromAttendanceSession(game.Id, name, member.Id);
            if (success) {
                ctx.TimedMessageAsync($"Attendance logged for '{name}' in game '{game.Name}'.").Forget();
            }
            else {
                ctx.TimedMessageAsync(
                        $"Failed to log attendance. Maybe a session with name '{name}' does not exist for this game?")
                    .Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error RemoveUserAttendance.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("list")]
    [Description("Lists all attendance sessions for this game.")]
    public async Task ListAttendance(CommandContext ctx) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            var sessions = await Database.GetAttendanceSessions(game!.Id);
            if (sessions.Count == 0) {
                ctx.TimedMessageAsync($"No attendance sessions found for game '{game.Name}'.").Forget();
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"**Attendance Sessions for {game.Name}:**");
            foreach (var session in sessions) {
                sb.AppendLine($"- {session.Name} ({session.Timestamp:yyyy-MM-dd HH:mm})");
            }

            ctx.TimedMessageAsync(sb.ToString(), Constants.ListResponseDelay).Forget();
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ListAttendance.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("show")]
    [Description("Shows details for a specific attendance session.")]
    public async Task ShowAttendance(
        CommandContext ctx, 
        [RemainingText] string name) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            var userIds = await Database.GetAttendanceSessionDetails(game!.Id, name);
            if (userIds.Count == 0) {
                ctx.TimedMessageAsync($"No records found for session '{name}' in game '{game.Name}'.").Forget();
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"**Attendance for '{name}' ({game.Name}):**");

            foreach (var userId in userIds) {
                try {
                    var member = await ctx.Guild!.GetMemberAsync(userId);
                    sb.AppendLine($"- {member.MemberServerName()}");
                }
                catch {
                    sb.AppendLine($"- Unknown User ({userId})");
                }

                if (sb.Length > 1900) {
                    await ctx.RespondAsync(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0) {
                await ctx.RespondAsync(sb.ToString());
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error ShowAttendance.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }

    [Command("removelog")]
    [Description("Deletes an entire attendance session and its records.")]
    public async Task DeleteSession(CommandContext ctx,
        [Description("Name of the session to delete")]
        [RemainingText] string name) {
        try {
            var (hasGame, game) = await EnsureGame(ctx);
            if (!hasGame) return;

            if (!await IsAuthorized(ctx, "officer", game!.Id)) {
                ctx.TimedMessageAsync("Only officers, leaders, and raid leaders can delete attendance sessions.")
                    .Forget();
                return;
            }

            bool success = await Database.DeleteAttendanceSession(game.Id, name);
            if (success) {
                ctx.TimedMessageAsync($"Successfully deleted attendance session '{name}' for game '{game.Name}'.")
                    .Forget();
            }
            else {
                ctx.TimedMessageAsync(
                        $"Failed to delete attendance session '{name}'. It might not exist for this game.")
                    .Forget();
            }
        }
        catch (Exception ex) {
            Logger.LogError(ex, "Error DeleteSession.");
        }
        finally {
            if (ctx is TextCommandContext textCtx) {
                await textCtx.Message.SafeDeleteAsync();
            }
        }
    }
}