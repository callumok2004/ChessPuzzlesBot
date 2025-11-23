using CsvHelper;
using CsvHelper.Configuration;

using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using PuzzlesBot.Context;

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

public partial class Interactions
{
	static CsvConfiguration CSVConfig = new(System.Globalization.CultureInfo.InvariantCulture) {
		HasHeaderRecord = true
	};
	static StreamReader CSVStreamReader => new("data/puzzles.csv");
	static CsvReader CSVReader => new(CSVStreamReader, CSVConfig);
	static List<PuzzleRecord> records = CSVReader.GetRecords<PuzzleRecord>().ToList();

	[SlashCommand("testpuzzle", "create random test puzzle")]
	public async Task TestPuzzleAsync() {
		ChessBoard chessBoard = new("default");

		var random = new Random();
		var randomRecord = records[random.Next(records.Count)];

		MemoryStream boardStream = chessBoard.Render(randomRecord.FEN, randomRecord.Moves.Split(' ')[0], randomRecord.GameUrl.Contains("/black#"));
		boardStream.Position = 0;

		long endTimestamp = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();

		EmbedBuilder embed = new() {
			Title = "Daily Puzzle (Rating: " + randomRecord.Rating + ")",
			Description = "Try to solve this puzzle!\nTime Remaining: <t:" + endTimestamp + ":R>",
			Color = Color.Blue,
			Timestamp = DateTimeOffset.UtcNow,
			Footer = new EmbedFooterBuilder {
				Text = "Good luck!"
			},
			ImageUrl = "attachment://board.png"
		};

		ButtonBuilder button = new() {
			Label = "Play",
			CustomId = "play",
			Style = ButtonStyle.Primary
		};

		var component = new ComponentBuilder()
			.WithButton(button);

		IUserMessage message = await Context.Channel.SendFileAsync(boardStream, "board.png", embed: embed.Build(), components: component.Build());
		await RespondAsync("posted", ephemeral: true);

		await db.Puzzles.AddAsync(new Puzzles {
			PuzzleId = randomRecord.PuzzleId,
			Fen = randomRecord.FEN,
			Moves = randomRecord.Moves,
			Rating = int.Parse(randomRecord.Rating),
			Url = randomRecord.GameUrl,
			Mesid = (long)message.Id,
			EndAt = DateTime.UtcNow.AddHours(24)
		});

		await db.SaveChangesAsync();
	}

	[ComponentInteraction("play")]
	public async Task PlayPuzzleAsync() {
		await RespondAsync("Loading...", ephemeral: true);

		var interaction = (IComponentInteraction)Context.Interaction;

		Puzzles? puzzle = db.Puzzles.FirstOrDefault(p => p.Mesid == (long)interaction.Message.Id);
		if (puzzle == null) {
			await FollowupAsync("Puzzle not found.", ephemeral: true);
			return;
		}

		await FollowupAsync($"found TODO: {puzzle.Url}", ephemeral: true);
	}
}
