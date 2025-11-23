using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System.Configuration;

namespace PuzzlesBot;

public class PuzzleRecord
{
#pragma warning disable CS8618
	public string PuzzleId { get; set; }
	public string FEN { get; set; }
	public string Moves { get; set; }
	public string Rating { get; set; }
	public string GameUrl { get; set; }
#pragma warning restore CS8618
}

public class Program
{
	public DiscordSocketClient? client;
	public static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

	public async Task MainAsync() {
		using ServiceProvider services = ConfigureServices();

		client = services.GetRequiredService<DiscordSocketClient>();

		await client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings["DiscordToken"]);
		await client.StartAsync();

		//var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) {
		//	HasHeaderRecord = true,
		//};
		//using var reader = new StreamReader("data/puzzles.csv");
		//using var csv = new CsvReader(reader, config);
		//var records = csv.GetRecords<PuzzleRecord>().ToList();
		//var random = new Random();
		//var randomRecord = records[random.Next(records.Count)];

		await Task.Delay(-1);
	}

	private static ServiceProvider ConfigureServices() {
		var Intents = GatewayIntents.AllUnprivileged;
		Intents &= ~GatewayIntents.GuildScheduledEvents;
		Intents &= ~GatewayIntents.GuildInvites;

		DiscordSocketClient Client = new(
			new DiscordSocketConfig {
				GatewayIntents = Intents
			}
		);

		InteractionService Interactions = new(Client.Rest, new InteractionServiceConfig {
			LogLevel = LogSeverity.Debug,
			ThrowOnError = true,
			UseCompiledLambda = true
		});

		IServiceCollection Collection = new ServiceCollection()
			.AddSingleton(Client)
			.AddSingleton(Interactions)
			.AddSingleton<IMongoClient>(c => new MongoClient(ConfigurationManager.AppSettings["MongoURI"]));

		SetupMongo(Collection);

		return Collection.BuildServiceProvider();
	}

	private static void SetupMongo(IServiceCollection Collection) {
		Collection.AddSingleton(sp => {
			IMongoClient dbClient = sp.GetRequiredService<IMongoClient>();
			return dbClient.GetDatabase("puzzles_bot");
		});
	}
}
