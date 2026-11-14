using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Webvoto.VotingSystem.Auditing;

public static class OptionEncoding {

	public static readonly int LatestVersion = 2; // itentionally not a const!

	public static byte[] Encode(OptionRecord op, int version)
		=> Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(getFields(op, version)));

	private static List<string> getFields(OptionRecord op, int version) => version switch {

		/*
		 * DO NOT CHANGE EXISTING VERSIONS!
		 * 
		 * When a new field is added to OptionRecord, create a new version with a new field list containing the new field and update the LastestVersion above
		 */

		1 => [
			op.Id.ToString(),
			op.QuestionId.ToString(),
			op.Name,
			op.Identifier,
			op.TypeCode,
			op.Order.ToString(),
			op.IsEnabled.ToString(),
			op.ImageContentType,
			op.ImageThumbprint,
			op.Description,
			op.DateDeletedUtc?.ToString("u"),
		],

		2 => [
			.. getFields(op, 1),
			op.InscriptionNumber,
			op.IsParent.ToString(),
			op.ParentOptionId?.ToString(),
			op.RoundStatement,
			op.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			op.InscriptionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			op.TieBreakPriority?.ToString(CultureInfo.InvariantCulture),
		],

		_ => throw new NotImplementedException()
	};
}
