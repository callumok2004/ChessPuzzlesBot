using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace PuzzlesBot;

public partial class ConfigInteractions {
	[SlashCommand("setrole", "Configure role to be mentioned for daily puzzles")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task SetRole(SocketRole role) {
		var guildConfig = db.Servers.FirstOrDefault(s => s.ServerId == (long)Context.Guild.Id);
		if (guildConfig == null) {
			guildConfig = new Context.Servers {
				ServerId = (long)Context.Guild.Id,
				RoleId = (long)role.Id
			};
			db.Servers.Add(guildConfig);
		}
		else guildConfig.RoleId= (long)role.Id;

		await db.SaveChangesAsync();

		await RespondAsync($"Daily puzzles channel set to {role.Name}.");
	}
}
