using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Counter {

	public class DecryptionTable {

		private readonly HashAlgorithm hasher = SHA256.Create();
		private readonly Dictionary<string, byte[]> plaintextDictionary = new Dictionary<string, byte[]>();

		public DecryptionTable(IEnumerable<byte[]> ciphers, IEnumerable<byte[]> plaintexts) {
			var cipherList = ciphers?.ToList() ?? throw new ArgumentNullException(nameof(ciphers));
			var plaintextList = plaintexts?.ToList() ?? throw new ArgumentNullException(nameof(plaintexts));
			if (cipherList.Count != plaintextList.Count) {
				throw new ArgumentException("The same number of ciphers and plaintexts must be given");
			}
			for (var i = 0; i < cipherList.Count; i++) {
				plaintextDictionary[getCipherHash(cipherList[i])] = plaintextList[i];
			}
		}

		public byte[] GetDecryption(byte[] cipher)
			=> plaintextDictionary[getCipherHash(cipher)];

		private string getCipherHash(byte[] cipher)
			=> Convert.ToBase64String(hasher.ComputeHash(cipher));
	}
}
