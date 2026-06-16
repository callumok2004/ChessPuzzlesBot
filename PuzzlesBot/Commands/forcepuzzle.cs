using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace PuzzlesBot;

public partial class Interactions {
	[SlashCommand("forcepuzzle", "Force a daily puzzle now")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task ForcePuzzle() {
		await DeferAsync(ephemeral: true);

		if (Context.User is not SocketGuildUser user || !user.GuildPermissions.Administrator) {
			await FollowupAsync("You must be a server administrator to use this command.", ephemeral: true);
			return;
		}

		var server = db.Servers.FirstOrDefault(s => s.ServerId == (long)Context.Guild.Id);
		if (server == null || server.PuzzlesChannel == null) {
			await FollowupAsync("Please set a puzzle channel first using `/config setchannel`.", ephemeral: true);
			return;
		}

		await DailyPuzzleService.TriggerDailyPuzzleNow((long)Context.Guild.Id);

		await Program.Log("Config", $"Daily puzzle force-triggered for guild {Context.Guild.Id} by {Context.User.Id}", LogSeverity.Info);
		await FollowupAsync("Daily puzzle triggered.", ephemeral: true);
	}
}
