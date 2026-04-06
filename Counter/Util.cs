using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Counter {
	
	public static class Util {

		public static byte[] DecodeHex(string s) {
			if (s == null) {
				throw new ArgumentNullException(nameof(s));
			}
			if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
				s = s[2..];
			}
			if (s.Length % 2 != 0) {
				throw new Exception("Invalid hex string: bad length");
			}
			var result = new byte[s.Length / 2];
			for (var i = 0; i < result.Length; i++) {
				result[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
			}
			return result;
		}

		public static byte[] DecodePem(byte[] pem) => DecodePem(Encoding.ASCII.GetString(pem));

		public static byte[] DecodePem(string pem) {
			var lines = pem.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var base64 = string.Join("", lines.Where(l => !l.StartsWith("---")));
			return Convert.FromBase64String(base64);
		}

		public static ECDsa GetPublicKey(byte[] encodedPublicKey) {
			ECDsa publicKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
			publicKey.ImportSubjectPublicKeyInfo(encodedPublicKey, out _);
			return publicKey;
		}

		public static bool VerifyServerSignature(ECDsa serverPublicKey, byte[] data, byte[] signature)
			=> serverPublicKey.VerifyData(data, signature, HashAlgorithmName.SHA256);

	}
}
