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
			var request = new DecryptBatchRequest {
				Ciphers = ciphers.Select(c => new CipherAlgorithmAndValueModel { Algorithm = EncryptionAlgorithms.RsaOaepSha256, Value = c }).ToList(),
			};
			var response = await sendAsync<DecryptBatchResponse>(HttpMethod.Put, $"/api/keys/{keyId}/ciphertext-batch", request);
			return response.Plaintexts;
		}

		public async Task<byte[]> SignCadesAsync(Guid keyId, byte[] data, byte[] certificate) {
			var request = new SignCadesRequest {
				Data = data,
				Certificate = certificate,
			};
			var response = await sendAsync<SignCadesResponse>(HttpMethod.Put, $"/api/keys/{keyId}/cades", request);
			return response.Cms;
		}

		private async Task<TResponse> sendAsync<TResponse>(HttpMethod method, string relativeUri, object request) {
			var httpRequest = new HttpRequestMessage(method, new Uri(new Uri(endpoint), relativeUri));
			httpRequest.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
			httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
			var httpResponse = await httpClient.SendAsync(httpRequest);
			httpResponse.EnsureSuccessStatusCode();
			var responseJson = await httpResponse.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<TResponse>(responseJson);
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

		public class SignCadesRequest {

			public byte[] Data { get; set; }

			public byte[] Certificate { get; set; }
		}

		public class SignCadesResponse {

			public byte[] Cms { get; set; }
		}

		#endregion
	}
}
