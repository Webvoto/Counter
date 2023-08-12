using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Counter {

	public class WebVaultClient {

		private readonly HttpClient httpClient = new HttpClient();
		private readonly string endpoint;
		private readonly string apiKey;

		public WebVaultClient(string endpoint, string apiKey) {
			this.endpoint = endpoint;
			this.apiKey = apiKey;
		}

		public async Task<List<byte[]>> DecryptBatchAsync(Guid keyId, IEnumerable<byte[]> ciphers) {

			var uri = new Uri(new Uri(endpoint), $"/api/keys/{keyId}/ciphertext-batch");
			var request = new DecryptBatchRequest {
				Ciphers = ciphers.Select(c => new CipherAlgorithmAndValueModel { Algorithm = EncryptionAlgorithms.RsaOaepSha256, Value = c }).ToList(),
			};
			var requestJson = JsonConvert.SerializeObject(request);
			var httpRequest = new HttpRequestMessage(HttpMethod.Put, uri);
			httpRequest.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
			httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

			var httpResponse = await httpClient.SendAsync(httpRequest);

			httpResponse.EnsureSuccessStatusCode();
			var responseJson = await httpResponse.Content.ReadAsStringAsync();
			var response = JsonConvert.DeserializeObject<DecryptBatchResponse>(responseJson);

			return response.Plaintexts;
		}

		#region API models

		public enum EncryptionAlgorithms {
			RsaOaepSha1,
			RsaOaepSha256,
			RsaOaepSha384,
			RsaOaepSha512,
		}

		public class DecryptBatchRequest {

			public List<CipherAlgorithmAndValueModel> Ciphers { get; set; }
		}

		public class CipherAlgorithmAndValueModel {

			public EncryptionAlgorithms Algorithm { get; set; }

			public byte[] Value { get; set; }
		}

		public class DecryptBatchResponse {

			public List<byte[]> Plaintexts { get; set; }
		}

		#endregion
	}
}
