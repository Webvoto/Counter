using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Counter; 

[Flags]
public enum StringNormalizations {
	None = 0,
	Trim = 1 << 0,
	TreatNullWordsAsBlank = 1 << 1,
	TreatZeroAsBlank = 1 << 2,
	CoalesceToEmptyString = 1 << 3,
	NormalizeWhiteSpaces = 1 << 4,
	RemoveDiacritics = 1 << 5,
	CoalesceToNull = 1 << 6,
}

public static class StringUtil {

	public const string SemicolonReplacement = "__";

	public const StringNormalizations CsvStringNormalizations = StringNormalizations.NormalizeWhiteSpaces | StringNormalizations.Trim | StringNormalizations.TreatNullWordsAsBlank;
	public const StringNormalizations CsvStringNormalizationsNullable = CsvStringNormalizations | StringNormalizations.CoalesceToNull;
	public const StringNormalizations CsvStringNormalizationsRequired = CsvStringNormalizationsNullable | StringNormalizations.CoalesceToEmptyString;

	public static string Normalize(string s, StringNormalizations normalizations = CsvStringNormalizationsNullable) {

		if (s != null) {

			if (normalizations.HasFlag(StringNormalizations.NormalizeWhiteSpaces)) {
				s = s.NormalizeWhiteSpaces();
			}

			if (normalizations.HasFlag(StringNormalizations.Trim)) {
				s = s.Trim();
			}

			if (normalizations.HasFlag(StringNormalizations.RemoveDiacritics)) {
				s = s.RemoveDiacritics();
			}
		
			if (normalizations.HasFlag(StringNormalizations.RemoveDiacritics)) {
				s = s.RemoveDiacritics();
			}

			if (normalizations.HasFlag(StringNormalizations.TreatNullWordsAsBlank) && nullWords.Any(nw => nw.Equals(s, StringComparison.InvariantCultureIgnoreCase))) {
				s = string.Empty;
			}

			if (normalizations.HasFlag(StringNormalizations.TreatZeroAsBlank) && s.Equals("0")) {
				s = string.Empty;
			}
		}

		if (normalizations.HasFlag(StringNormalizations.CoalesceToEmptyString)) {
			s ??= string.Empty;
		} else if (normalizations.HasFlag(StringNormalizations.CoalesceToNull)) {
			if (string.IsNullOrEmpty(s)) {
				s = null;
			}
		}

		return s;
	}

	public static string RemoveDiacritics(this string s) {
		if (s == null) {
			return null;
		}

		return new string((from c in s.Normalize(NormalizationForm.FormD)
						   where CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
						   select c).ToArray()).Normalize(NormalizationForm.FormC);
	}

	public static string NormalizeWhiteSpaces(this string s)
		=> whiteSpaceRegex.Replace(s ?? string.Empty, " ").Trim();

	public static string RemoveWhiteSpaces(this string s)
		=> whiteSpaceRegex.Replace(s ?? string.Empty, "");

	private static readonly Regex whiteSpaceRegex = new(@"\s+", RegexOptions.Compiled);

	private static readonly List<string> nullWords = ["NULL"];

}
