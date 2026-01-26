using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace DiscordPantheonGuildBot.Commands;

public static class Extensions {
    public static void Forget(this Task task) {
        // This is a simple fire-and-forget helper that suppresses compiler warnings
        // and ensures exceptions are observed if they happen.
        _ = Task.Run(async () => {
            try {
                await task;
            }
            catch (Exception ex) {
                Console.WriteLine($"Fire-and-forget task failed: {ex.Message}");
            }
        });
    }

    public static string MemberServerName(this DiscordMember member) {
        return member.DisplayName;
    }

    public static async Task TimedMessageAsync(this CommandContext ctx, string message,
        int delayInSeconds = Constants.ShortResponseDelay) {
        await ctx.RespondAsync(message);
        var response = await ctx.GetResponseAsync();
        if (response is not null) {
            await Task.Delay(delayInSeconds * 1000);
            await response.SafeDeleteAsync();
        }
    }

    public static async Task TimedMessageAsync(this CommandContext ctx, DiscordMessageBuilder embed,
        int delayInSeconds = Constants.ShortResponseDelay) {
        await ctx.RespondAsync(embed);
        var response = await ctx.GetResponseAsync();
        if (response is not null) {
            await Task.Delay(delayInSeconds * 1000);
            await response.SafeDeleteAsync();
        }
    }

    public static async Task SafeDeleteAsync(this DiscordMessage message) {
        try {
            await message.DeleteAsync();
            // Optional: Log success or continue with other actions
        }
        catch (DSharpPlus.Exceptions.NotFoundException) {
            // The message might have already been deleted by another process or due to a race condition.
            // Since the desired outcome (deletion) is achieved, you can safely ignore this specific error.
            Console.WriteLine(
                $"Message {message.Id} was already deleted or not found at the API endpoint, but the operation proceeded.");
        }
        catch (Exception ex) {
            // Handle other potential errors (e.g., lack of permissions, rate limits)
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
        }
    }

    public static async Task UpdatePinnedMessageWithFile(this DiscordMessage message, MemoryStream dataStream,
        string fileName, DiscordMessageBuilder builder) {
        // 1. Ensure the stream is at the beginning
        dataStream.Position = 0;

        // 2. Build the new message content
        // var builder = new DiscordMessageBuilder()
        //     .WithContent(content)
        builder.AddFile(fileName, dataStream, true);

        await message.ModifyAsync(builder);
    }

    public static async Task UpdatePinnedMessageWithXlsxAndGeneratedTxtFile(this DiscordMessage message,
        MemoryStream dataStream, string fileName, DiscordMessageBuilder builder) {
        // 1. Ensure the stream is at the beginning
        dataStream.Position = 0;

        var textStream = ExcelConverter.XlsxToFixedLength(dataStream); //  XlsxToTabDelimited(dataStream);
        dataStream.Position = 0;
        textStream.Position = 0;

        var attachments = new Dictionary<string, Stream>() {
            { fileName, dataStream }, // Filename in Discord : Stream
            { fileName.Replace(".xlsx", ".txt"), textStream } // Another file
        };

        // 2. Build the new message content
        // var builder = new DiscordMessageBuilder()
        //     .WithContent(content);
        builder.AddFiles(attachments,
            true);

        await message.ModifyAsync(builder);
    }
}