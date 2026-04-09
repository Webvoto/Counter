using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Webvoto.VotingSystem.Auditing;

public static class OptionEncoding {

	public static readonly int LatestVersion = 1; // itentionally not a const!

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

		_ => throw new NotImplementedException()
	};
}
