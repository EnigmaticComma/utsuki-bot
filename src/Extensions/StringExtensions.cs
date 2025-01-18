using System;
using System.Linq;
using System.Text;

namespace App.Extensions {
	public static class StringExtensions {
		
		private static readonly StringBuilder sb = new StringBuilder();
		
		
		
		
		public static string ReplaceAt(this string input, int index, char newChar)  {
			if (input == null) {
				throw new ArgumentNullException(nameof(input));
			}
			char[] chars = input.ToCharArray();
			chars[index] = newChar;
			return new string(chars);
		}
		
		public static string FirstCharToUpper(this string input) {
			switch (input)
			{
				case null: throw new ArgumentNullException(nameof(input));
				case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
				default: return input.First().ToString().ToUpper() + input.Substring(1);
			}
		}
		
		public static string CJoin(this string[] input, char separator = ' ') {
			return string.Join(separator, input);
		}
		
		public static bool AreAllCharactersTheSame(this string str) {
			if (string.IsNullOrEmpty(str)) return false;
			if (str.Length == 1) return true;
			var firstChar = str[0];
			for (int i = 1; i < str.Length; i++) {
				if (str[i] != firstChar) {
					return false;
				}
			}
			return true;
		}
		
		
		/// <summary>
		/// Remove special characters from string.
		/// </summary>
		/// <param name="text"></param>
		/// <returns>Return normalized string.</returns>
		public static string RemoveDiacritics(this string text) {
			var normalizedString = text.Normalize(NormalizationForm.FormD);
			sb.Clear();

			foreach (var c in normalizedString) {
				var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark) {
					sb.Append(c);
				}
			}

			return sb.ToString().Normalize(NormalizationForm.FormC);
		}
		
		public static string SubstringSafe(this string input, int length) {
			if (string.IsNullOrEmpty(input)) return null;
			if (length >= input.Length) {
				return input;
			}
			return input.Substring(0, length);
		}
	}
}
