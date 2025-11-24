using Discord;
using Discord.Interactions;

namespace PuzzlesBot;
partial class Interactions
{
	[SlashCommand("help", "View a list of all available commands")]
	public async Task Help()
	{
		IReadOnlyList<SlashCommandInfo> _interactions = interactions.SlashCommands;

		EmbedBuilder embed = new()
		{
			Title = "Puzzles • Commands",
			Color = Color.Blue
		};

		if (_interactions.Any())
			foreach (SlashCommandInfo command in _interactions)
				if (command.Name != "help")
				{
					string commandName = command.Module.SlashGroupName != null ? $"/{command.Module.SlashGroupName} {command.Name}" : $"/{command.Name}";
					string description = command.Description?.Replace("(", "**(").Replace(")", ")**") ?? "No description available";
					embed.AddField(commandName, description);
				}

		await RespondAsync(embed: embed.Build());
	}
}