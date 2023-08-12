using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counter {

	public class WebVaultKeyParameters {

		public string Endpoint { get; set; }

		public string ApiKey { get; set; }

		public Guid KeyId { get; set; }

		public static WebVaultKeyParameters Deserialize(string s) => JsonConvert.DeserializeObject<WebVaultKeyParameters>(s);
	}
}
