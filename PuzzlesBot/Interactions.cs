using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

namespace PuzzlesBot;

public class InteractionHandler {
	internal readonly InteractionService interactions;
	internal readonly DiscordSocketClient client;

	public InteractionHandler(IServiceProvider _services) {
		interactions = _services.GetRequiredService<InteractionService>();
		client = _services.GetRequiredService<DiscordSocketClient>();

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

public class InteractionsBase(IServiceProvider services) : InteractionModuleBase {
	public readonly IMongoClient mongoClient = services.GetRequiredService<IMongoClient>();
	public readonly InteractionService interactions = services.GetRequiredService<InteractionService>();
}

public partial class Interactions(IServiceProvider services) : InteractionsBase(services) { }


// test
public partial class Interactions {
	[SlashCommand("ping", "Replies with Pong!")]
	public async Task PingAsync() {
		await RespondAsync("Pong!");
	}
}
