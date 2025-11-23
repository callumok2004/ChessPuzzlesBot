using Discord;
using Discord.Interactions;

namespace PuzzlesBot;

public partial class Interactions
{
	[SlashCommand("createboard", "create test board from fen")]
	public async Task PingAsync(string fen) {
		ChessBoard chessBoard = new("default");

		MemoryStream boardStream = chessBoard.Render(fen, "h1h4", true);
		boardStream.Position = 0;

		EmbedBuilder embed = new() {
			Title = "test board",
			ImageUrl = "attachment://board.png"
		};

		await RespondWithFileAsync(boardStream, "board.png", embed: embed.Build());
	}
}
