using Newtonsoft.Json;
using System;

namespace Counter {

	public class WebVaultKeyParameters {

		public string Endpoint { get; set; }

		public string ApiKey { get; set; }

		public Guid KeyId { get; set; }

		public static WebVaultKeyParameters Deserialize(string s) => JsonConvert.DeserializeObject<WebVaultKeyParameters>(s);
	}
}
