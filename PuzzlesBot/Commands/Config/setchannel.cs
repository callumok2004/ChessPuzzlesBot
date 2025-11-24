using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace PuzzlesBot;

public partial class ConfigInteractions
{
	[SlashCommand("setchannel", "Configure daily puzzles channel")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task SetChannel(SocketTextChannel channel) {
		var guildConfig = db.Servers.FirstOrDefault(s => s.ServerId == (long)Context.Guild.Id);
		if (guildConfig == null) {
			guildConfig = new Context.Servers {
				ServerId = (long)Context.Guild.Id,
				PuzzlesChannel = (long)channel.Id
			};
			db.Servers.Add(guildConfig);
		} else guildConfig.PuzzlesChannel = (long)channel.Id;

		await db.SaveChangesAsync();

		await RespondAsync($"Daily puzzles channel set to {channel.Mention}.");
	}
}
