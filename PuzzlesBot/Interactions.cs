using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

namespace PuzzlesBot;

public class InteractionHandler {
	readonly InteractionService interactions;
	readonly DiscordSocketClient client;
	readonly IServiceProvider services;

	public InteractionHandler(IServiceProvider _services) {
		interactions = _services.GetRequiredService<InteractionService>();
		client = _services.GetRequiredService<DiscordSocketClient>();
		services = _services;

		client.InteractionCreated += InteractionCreated;
		interactions.InteractionExecuted += InteractionExecuted;
	}

	public async Task InteractionCreated(SocketInteraction interaction) {
		if (interaction.IsDMInteraction) return;

		SocketInteractionContext context = new(client, interaction);

		try {

		} catch (Exception exception) {
			Console.WriteLine("Error occured executing an interaction for {0}: {1}", context.User.Username, exception.ToString());
			if (interaction.Type is InteractionType.ApplicationCommand)
				await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
		}
	}

	public async Task InteractionExecuted(ICommandInfo commandInfo, IInteractionContext context, IResult result) {
		if (result.IsSuccess || context.Interaction.HasResponded) return;

		EmbedBuilder enbed = new() {
			Color = Color.Red,
			Description = "An error has occurred."
		};

		await context.Interaction.RespondAsync(embed: enbed.Build());
	}
}
