using Org.BouncyCastle.Asn1;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;
using CmsAttribute = Org.BouncyCastle.Asn1.Cms.Attribute;
using Org.BouncyCastle.Asn1.Ess;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.Esf;
using Org.BouncyCastle.Asn1.X509;

namespace Counter {

	public static class CmsEncoding {

		private static class Oids {
			public static readonly DerObjectIdentifier Sha256DigestAlgorithm = new("2.16.840.1.101.3.4.2.1");
			public static readonly DerObjectIdentifier ContentTypeAttribute = new("1.2.840.113549.1.9.3");
			public static readonly DerObjectIdentifier DataContentType = new("1.2.840.113549.1.7.1");
			public static readonly DerObjectIdentifier MessageDigestAttribute = new("1.2.840.113549.1.9.4");
			public static readonly DerObjectIdentifier SigningCertificateV2Attribute = new("1.2.840.113549.1.9.16.2.47");
			public static readonly DerObjectIdentifier SignaturePolicyIdentifierAttribute = new("1.2.840.113549.1.9.16.2.15");
			public static readonly DerObjectIdentifier SignaturePolicyUri = new("1.2.840.113549.1.9.16.5.1");
		}

		private static class AdRBPolicyInfo {
			public static readonly DerObjectIdentifier Id = new("2.16.76.1.7.1.1.2.2");
			public static readonly byte[] Sha256Hash = Convert.FromBase64String("D2+ixigZgXFslceYmQOYRFI7HGHCyWIonNrHgR/u4p4=");
			public const string Uri = "http://politicas.icpbrasil.gov.br/PA_AD_RB_v2_2.der";
		}

		public static byte[] EncodeSignedAttributes(byte[] messageDigest, X509Certificate2 signingCertificate)
			=> new DerSet(generateSignedAttributes(messageDigest, signingCertificate)).GetDerEncoded();

		private static List<CmsAttribute> generateSignedAttributes(byte[] messageDigest, X509Certificate2 signingCertificate) => [
			new(Oids.ContentTypeAttribute, new DerSet(Oids.DataContentType)),
			new(Oids.MessageDigestAttribute, new DerSet(new DerOctetString(messageDigest))),
			new(Oids.SignaturePolicyIdentifierAttribute, new DerSet(generateSignaturePolicyIdentifierAttributeValue())),
			new(Oids.SigningCertificateV2Attribute, new DerSet(generateSigningCertificateV2AttributeValue(signingCertificate))),
		];

		private static Asn1Encodable generateSignaturePolicyIdentifierAttributeValue() {
			var sigPolicyHash = new OtherHashAlgAndValue(new AlgorithmIdentifier(Oids.Sha256DigestAlgorithm), AdRBPolicyInfo.Sha256Hash);
			var sigPolicyQualifier = new SigPolicyQualifierInfo(Oids.SignaturePolicyUri, new DerIA5String(AdRBPolicyInfo.Uri));
			var signaturePolicyId = new SignaturePolicyId(AdRBPolicyInfo.Id, sigPolicyHash, [sigPolicyQualifier]);
			return new SignaturePolicyIdentifier(signaturePolicyId);
		}

		private static Asn1Encodable generateSigningCertificateV2AttributeValue(X509Certificate2 signingCertificate) {
			var cert = X509CertificateStructure.GetInstance(new Asn1InputStream(signingCertificate.RawData).ReadObject());
			var issuerSerial = new IssuerSerial(cert.Issuer, cert.SerialNumber);
			var certId = new EssCertIDv2(new AlgorithmIdentifier(Oids.Sha256DigestAlgorithm), SHA256.HashData(signingCertificate.RawData), issuerSerial);
			return new SigningCertificateV2([certId]);
		}
	}
}
