using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Services {
	public class RsaService {

		public RSA Rsa { get; private set; }

		public RsaService() {
			Rsa = RSA.Create();
		}

		public RsaService(byte[] publicKey) {
			Rsa = RSA.Create();
			ImportPublicKey(publicKey);
		}

		public void ImportPrivateKey(string password, byte[] key)
			=> Rsa.ImportEncryptedPkcs8PrivateKey(password, key, out _);

		public void ImportPublicKey(byte[] publicKey)
			=> Rsa.ImportSubjectPublicKeyInfo(publicKey, out _);

		public bool VerifyDataSignature(byte[] data, byte[] signature)
			=> Rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		public bool VerifyHashSignature(byte[] hash, byte[] signature)
			=> Rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		public byte[] DecryptData(byte[] data)
			=> Rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
	}
}
