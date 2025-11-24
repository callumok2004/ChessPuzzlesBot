using System.Text.RegularExpressions;

namespace PuzzlesBot;

public static class ChessUtils {
	public static string? SanToUci(string san, string fen, string? expectedMove = null) {
		san = san.Trim();

		if (san.Equals("O-O", StringComparison.OrdinalIgnoreCase) || san.Equals("0-0", StringComparison.OrdinalIgnoreCase)) return GetCastlingMove(fen, true);
		if (san.Equals("O-O-O", StringComparison.OrdinalIgnoreCase) || san.Equals("0-0-0", StringComparison.OrdinalIgnoreCase)) return GetCastlingMove(fen, false);

		var match = Regex.Match(san, @"^([NBRQKnbrqk])?([a-h])?([1-8])?x?([a-h][1-8])(=?[NBRQnbrq])?[\+#]?$");

		if (!match.Success) return null;

		char pieceType = string.IsNullOrEmpty(match.Groups[1].Value) ? 'P' : char.ToUpper(match.Groups[1].Value[0]);
		char? fromFile = string.IsNullOrEmpty(match.Groups[2].Value) ? null : match.Groups[2].Value[0];
		char? fromRank = string.IsNullOrEmpty(match.Groups[3].Value) ? null : match.Groups[3].Value[0];
		string targetSq = match.Groups[4].Value;
		string promotion = match.Groups[5].Value.Replace("=", "").ToLower();

		bool isWhiteTurn = fen.Split(' ')[1] == "w";
		char targetPieceChar = isWhiteTurn ? pieceType : char.ToLower(pieceType);

		var board = ParseFen(fen);
		var candidates = new List<string>();

		for (int r = 0; r < 8; r++) {
			for (int f = 0; f < 8; f++) {
				char p = board[r, f];
				if (p != targetPieceChar) continue;

				string currentSq = $"{(char)('a' + f)}{8 - r}";

				if (fromFile.HasValue && currentSq[0] != fromFile.Value) continue;
				if (fromRank.HasValue && currentSq[1] != fromRank.Value) continue;

				if (CanMove(board, r, f, targetSq, isWhiteTurn)) {
					candidates.Add(currentSq + targetSq + promotion);
				}
			}
		}


		if (candidates.Count == 1) return candidates[0];

		if (expectedMove != null && candidates.Contains(expectedMove))
			return expectedMove;

		return candidates.FirstOrDefault();
	}

	private static string? GetCastlingMove(string fen, bool kingside) {
		bool white = fen.Split(' ')[1] == "w";
		return white ? (kingside ? "e1g1" : "e1c1") : (kingside ? "e8g8" : "e8c8");
	}

	private static char[,] ParseFen(string fen) {
		char[,] board = new char[8, 8];
		string[] ranks = fen.Split(' ')[0].Split('/');
		for (int r = 0; r < 8; r++) {
			int f = 0;
			foreach (char c in ranks[r]) {
				if (char.IsDigit(c)) f += c - '0';
				else board[r, f++] = c;
			}
		}
		return board;
	}

	private static bool CanMove(char[,] board, int r, int f, string targetSq, bool isWhite) {
		int tr = 8 - (targetSq[1] - '0');
		int tf = targetSq[0] - 'a';
		char piece = char.ToUpper(board[r, f]);

		int dr = tr - r;
		int df = tf - f;

		if (piece == 'N') {
			return (Math.Abs(dr) == 2 && Math.Abs(df) == 1) || (Math.Abs(dr) == 1 && Math.Abs(df) == 2);
		}

		if (piece == 'B' || piece == 'R' || piece == 'Q') {
			bool straight = (dr == 0 || df == 0);
			bool diagonal = (Math.Abs(dr) == Math.Abs(df));

			if (piece == 'B' && !diagonal) return false;
			if (piece == 'R' && !straight) return false;
			if (piece == 'Q' && !straight && !diagonal) return false;

			int stepR = Math.Sign(dr);
			int stepF = Math.Sign(df);
			int curR = r + stepR;
			int curF = f + stepF;
			while (curR != tr || curF != tf) {
				if (board[curR, curF] != '\0') return false;
				curR += stepR;
				curF += stepF;
			}
			return true;
		}

		if (piece == 'K') {
			return Math.Abs(dr) <= 1 && Math.Abs(df) <= 1;
		}

		if (piece == 'P') {
			int direction = isWhite ? -1 : 1;
			if (df == 0 && dr == direction && board[tr, tf] == '\0') return true;
			if (df == 0 && dr == 2 * direction && ((isWhite && r == 6) || (!isWhite && r == 1)) && board[r + direction, f] == '\0' && board[tr, tf] == '\0') return true;
			if (Math.Abs(df) == 1 && dr == direction) {
				return true;
			}
			return false;
		}

		return false;
	}
}
