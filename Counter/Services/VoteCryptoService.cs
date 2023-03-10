﻿using Counter.Classes;
using Counter.Database;
using Counter.Entities;
using Counter.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Counter.Services {
	public class VoteCryptoService {
		private readonly RsaService voteSignatureService;
		private readonly RsaService voteEncryptionService;
		private readonly ServerInstanceCache serverInstanceRsaCache;
		private readonly AppDbContext appDbContext;

		public VoteCryptoService(ServerInstanceCache serverInstanceRsaCache, AppDbContext appDbContext) {
			voteSignatureService = new RsaService();
			voteEncryptionService = new RsaService();
			this.serverInstanceRsaCache = serverInstanceRsaCache;
			this.appDbContext = appDbContext;
		}

		public X509Certificate2 VoteSigningCertificate { get; private set; }

		public async Task InitializeAsync(string password, byte[] privateKey) {
			var voteSignatureKey = await appDbContext.ElectionKeys.Where(e => e.PurposeCode == ElectionKey.VoteSignaturePurpose).FirstOrDefaultAsync();

			VoteSigningCertificate = new X509Certificate2(voteSignatureKey.Certificate);

			ImportVoteSignaturePublicKey(voteSignatureKey.PublicKey);

			ImportVoteEncryptionPrivateKey(password, privateKey);
		}

		public void ImportVoteSignaturePublicKey(byte[] publicKey)
			=> voteSignatureService.ImportPublicKey(publicKey);

		public void ImportVoteEncryptionPrivateKey(string password, byte[] privateKey)
			=> voteEncryptionService.ImportPrivateKey(password, privateKey);

		public bool VerifyVoteSignature(Vote vote)
			=> voteSignatureService.VerifyDataSignature(getSignedAttributesDer(SHA256.HashData(encodeVoteContent(vote)), VoteSigningCertificate), vote.Signature);

		private byte[] encodeVoteContent(Vote vote)
			=> new EncodedVoteContent(vote.PoolId, vote.Slot, vote.EncryptedChoices).ToAsn1().GetDerEncoded();

		public bool VerifyServerSignature(Vote vote, int serverInstanceId)
			=> serverInstanceRsaCache.Rsa[serverInstanceId].VerifyDataSignature(vote.Signature, vote.ServerSignature);

		public string DecryptVoteChoice(VoteItem vote)
			=> Encoding.UTF8.GetString(voteEncryptionService.DecryptData(vote.EncryptedChoice));

		private static readonly byte[] SignedAttributesDerPrefix = new byte[] {
			0x31, 0x82, 0x01, 0xA9, 0x30, 0x18, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09,
			0x03, 0x31, 0x0B, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x01, 0x30, 0x2F,
			0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x04, 0x31, 0x22, 0x04, 0x20,
		};

		private static readonly byte[] SignatureAttributesDerSuffix = new byte[] {
			0x30,
			0x81, 0x94, 0x06, 0x0B, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x02, 0x0F, 0x31,
			0x81, 0x84, 0x30, 0x81, 0x81, 0x06, 0x08, 0x60, 0x4C, 0x01, 0x07, 0x01, 0x01, 0x02, 0x02, 0x30,
			0x2F, 0x30, 0x0B, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x04, 0x20,
			0x0F, 0x6F, 0xA2, 0xC6, 0x28, 0x19, 0x81, 0x71, 0x6C, 0x95, 0xC7, 0x98, 0x99, 0x03, 0x98, 0x44,
			0x52, 0x3B, 0x1C, 0x61, 0xC2, 0xC9, 0x62, 0x28, 0x9C, 0xDA, 0xC7, 0x81, 0x1F, 0xEE, 0xE2, 0x9E,
			0x30, 0x44, 0x30, 0x42, 0x06, 0x0B, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x05,
			0x01, 0x16, 0x33, 0x68, 0x74, 0x74, 0x70, 0x3A, 0x2F, 0x2F, 0x70, 0x6F, 0x6C, 0x69, 0x74, 0x69,
			0x63, 0x61, 0x73, 0x2E, 0x69, 0x63, 0x70, 0x62, 0x72, 0x61, 0x73, 0x69, 0x6C, 0x2E, 0x67, 0x6F,
			0x76, 0x2E, 0x62, 0x72, 0x2F, 0x50, 0x41, 0x5F, 0x41, 0x44, 0x5F, 0x52, 0x42, 0x5F, 0x76, 0x32,
			0x5F, 0x32, 0x2E, 0x64, 0x65, 0x72, 0x30, 0x81, 0xC4, 0x06, 0x0B, 0x2A, 0x86, 0x48, 0x86, 0xF7,
			0x0D, 0x01, 0x09, 0x10, 0x02, 0x2F, 0x31, 0x81, 0xB4, 0x30, 0x81, 0xB1, 0x30, 0x81, 0xAE, 0x30,
			0x81, 0xAB, 0x04, 0x20, 0xA0, 0xE2, 0x54, 0xB5, 0xD5, 0xA6, 0xD6, 0x2D, 0x73, 0x52, 0x8F, 0xF7,
			0x97, 0x6C, 0xBE, 0x3E, 0xD8, 0x23, 0xC8, 0xF3, 0x05, 0xD2, 0x8B, 0xC4, 0x37, 0x90, 0x9D, 0x81,
			0xBD, 0x0F, 0x98, 0x29, 0x30, 0x81, 0x86, 0x30, 0x7A, 0xA4, 0x78, 0x30, 0x76, 0x31, 0x0B, 0x30,
			0x09, 0x06, 0x03, 0x55, 0x04, 0x06, 0x13, 0x02, 0x42, 0x52, 0x31, 0x13, 0x30, 0x11, 0x06, 0x03,
			0x55, 0x04, 0x0A, 0x13, 0x0A, 0x49, 0x43, 0x50, 0x2D, 0x42, 0x72, 0x61, 0x73, 0x69, 0x6C, 0x31,
			0x36, 0x30, 0x34, 0x06, 0x03, 0x55, 0x04, 0x0B, 0x13, 0x2D, 0x53, 0x65, 0x63, 0x72, 0x65, 0x74,
			0x61, 0x72, 0x69, 0x61, 0x20, 0x64, 0x61, 0x20, 0x52, 0x65, 0x63, 0x65, 0x69, 0x74, 0x61, 0x20,
			0x46, 0x65, 0x64, 0x65, 0x72, 0x61, 0x6C, 0x20, 0x64, 0x6F, 0x20, 0x42, 0x72, 0x61, 0x73, 0x69,
			0x6C, 0x20, 0x2D, 0x20, 0x52, 0x46, 0x42, 0x31, 0x1A, 0x30, 0x18, 0x06, 0x03, 0x55, 0x04, 0x03,
			0x13, 0x11, 0x41, 0x43, 0x20, 0x53, 0x41, 0x46, 0x45, 0x57, 0x45, 0x42, 0x20, 0x52, 0x46, 0x42,
			0x20, 0x76, 0x35, 0x02, 0x08, 0x4C, 0xC3, 0xD3, 0xDC, 0x80, 0x62, 0x84, 0x6D,
		};

		private byte[] getSignedAttributesDer(byte[] messageDigest, X509Certificate2 signingCertificate) {
			if (signingCertificate.Thumbprint != Constants.VoteSigningCertificateThumbprint) {
				throw new Exception("Can't accept the certificate");
			}
			return SignedAttributesDerPrefix.Concat(messageDigest).Concat(SignatureAttributesDerSuffix).ToArray();
		}
	}
}
