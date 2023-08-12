using Org.BouncyCastle.Asn1;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;
using CmsAttribute = Org.BouncyCastle.Asn1.Cms.Attribute;
using Org.BouncyCastle.Asn1.Ess;
using System.Linq;
using ContentInfo = Org.BouncyCastle.Asn1.Cms.ContentInfo;
using SignedData = Org.BouncyCastle.Asn1.Cms.SignedData;
using SignerInfo = Org.BouncyCastle.Asn1.Cms.SignerInfo;

namespace Counter {

	public record DigestAlgorithmAndValue(
		HashAlgorithmName Algorithm,
		byte[] Value
	);

	public record CmsInfo(
		DigestAlgorithmAndValue MessageDigest,
		DigestAlgorithmAndValue SigningCertificateDigest,
		DigestAlgorithmAndValue SignedAttributesDigest,
		byte[] Signature
	);

	public static class CmsEncoding {

		private static class Oids {
			public static readonly DerObjectIdentifier SignedDataContentType = new("1.2.840.113549.1.7.2");
			public static readonly DerObjectIdentifier MessageDigestAttribute = new("1.2.840.113549.1.9.4");
			public static readonly DerObjectIdentifier SigningCertificateV2Attribute = new("1.2.840.113549.1.9.16.2.47");
		}

		public static CmsInfo Decode(byte[] cms) {

			var contentInfo = ContentInfo.GetInstance(new Asn1InputStream(cms).ReadObject());
			if (!contentInfo.ContentType.Equals(Oids.SignedDataContentType)) {
				throw new Exception($"Unexpected content type: {contentInfo.ContentType.Id}");
			}
			var signedData = SignedData.GetInstance(contentInfo.Content);
			var signerInfo = SignerInfo.GetInstance(signedData.SignerInfos.FirstOrDefault() ?? throw new Exception($"The CMS is expected to contain at least one signer, but has none"));
			var signedAtts = signerInfo.AuthenticatedAttributes.Select(a => CmsAttribute.GetInstance(a));
			var digestAlg = HashAlgorithmName.FromOid(signerInfo.DigestAlgorithm.Algorithm.Id);

			// Message digest
			var messageDigestAtt = getRequiredCmsAttributeValue(signedAtts, Oids.MessageDigestAttribute);
			var messageDigestValue = Asn1OctetString.GetInstance(messageDigestAtt).GetOctets();

			// Signing certificate
			var signingCertAtt = SigningCertificateV2.GetInstance(getRequiredCmsAttributeValue(signedAtts, Oids.SigningCertificateV2Attribute));
			var signingCertID = signingCertAtt.GetCerts().FirstOrDefault() ?? throw new Exception("The SigningCertificateV2 object is expected to have at least one element but has none");
			var signingCertDigestAlg = HashAlgorithmName.FromOid(signingCertID.HashAlgorithm.Algorithm.Id);
			var signingCertDigestValue = signingCertID.GetCertHash();

			// Signature
			var signature = signerInfo.EncryptedDigest.GetOctets();

			// Signed attributes
			var signedAttributesDigestValue = HashAlgorithm.Create(digestAlg.Name).ComputeHash(signerInfo.AuthenticatedAttributes.GetDerEncoded());

			return new CmsInfo(
				new DigestAlgorithmAndValue(digestAlg, messageDigestValue),
				new DigestAlgorithmAndValue(signingCertDigestAlg, signingCertDigestValue),
				new DigestAlgorithmAndValue(digestAlg, signedAttributesDigestValue),
				signature
			);
		}

		private static Asn1Encodable getRequiredCmsAttributeValue(IEnumerable<CmsAttribute> attributes, DerObjectIdentifier oid)
			=> getRequiredCmsAttribute(attributes, oid).AttrValues.FirstOrDefault() ?? throw new Exception($"The CMS attribute {oid} is expected to have at least one value, but has none");

		private static CmsAttribute getRequiredCmsAttribute(IEnumerable<CmsAttribute> attributes, DerObjectIdentifier oid)
			=> attributes.FirstOrDefault(a => a.AttrType.Equals(oid)) ?? throw new Exception($"Required CMS attribute not found: {oid.Id}");
	}
}
