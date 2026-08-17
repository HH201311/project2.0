using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using BigInteger = Org.BouncyCastle.Math.BigInteger;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// RSA加密解密帮助类
    /// </summary>
    public static class RasHelper
    {
        /// <summary>
        /// RSA获取公钥私钥
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="privateKey"></param>
        /// <param name="RsaKeySize"></param>
        public static void GenerateKeys(out string publicKey, out string privateKey, int RsaKeySize = 2048)
        {
            publicKey = string.Empty;
            privateKey = string.Empty;
            using (var rsa = new RSACryptoServiceProvider(RsaKeySize))
            {
                try
                {
                    // 获取私钥和公钥。
                    publicKey = rsa.ToXmlString(false);
                    privateKey = rsa.ToXmlString(true);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
        }

        /// <summary>
        /// RSA公钥加密数据
        /// </summary>
        /// <param name="xmlPublicKey"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string RSAEncrypt(string xmlPublicKey, string content)
        {
            var encryptedContent = string.Empty;
            using (var rsa = new RSACryptoServiceProvider())
            {
                try
                {
                    rsa.FromXmlString(xmlPublicKey);
                    var bytesEncryted = Encoding.Default.GetBytes(content);
                    var encryptedData = rsa.Encrypt(bytesEncryted, false);
                    encryptedContent = Convert.ToBase64String(encryptedData);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
            return encryptedContent;
        }

        /// <summary>    
        /// RSA公钥pem-->XML格式转换， 
        /// </summary>    
        /// <param name="publicKey">pem公钥</param>    
        /// <param name="content">要加密的内容：比如密码</param>  
        /// <returns></returns>    
        public static string RSAPublicKey(string publicKey, string content)
        {
            RsaKeyParameters publicKeyParam = (RsaKeyParameters)PublicKeyFactory.CreateKey(Convert.FromBase64String(publicKey));
            string XML = string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent></RSAKeyValue>",
                Convert.ToBase64String(publicKeyParam.Modulus.ToByteArrayUnsigned()),
                Convert.ToBase64String(publicKeyParam.Exponent.ToByteArrayUnsigned()));
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            byte[] cipherbytes;
            rsa.FromXmlString(XML);
            cipherbytes = rsa.Encrypt(Encoding.UTF8.GetBytes(content), false);
            return Convert.ToBase64String(cipherbytes);
        }

        /// <summary>
        /// RSA私钥解密数据
        /// </summary>
        /// <param name="xmlPrivateKey"></param>
        /// <param name="encryptedContent"></param>
        /// <returns></returns>
        public static string RSADecrypt(string xmlPrivateKey, string encryptedContent)
        {
            var decryptedContent = string.Empty;
            using (var rsa = new RSACryptoServiceProvider())
            {
                try
                {
                    rsa.FromXmlString(xmlPrivateKey);
                    var bytesDecrypted = Convert.FromBase64String(encryptedContent);
                    var bytesPlainText = rsa.Decrypt(bytesDecrypted, false);
                    decryptedContent = Encoding.Default.GetString(bytesPlainText);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
            return decryptedContent;
        }

        /// <summary>
        /// 转换"私钥"格式
        /// </summary>
        /// <param name="xmlPrivateKey">xml格式私钥</param>
        /// <returns>base64格式私钥</returns>
        public static string ToBase64PrivateKey(string xmlPrivateKey)
        {
            string result = string.Empty;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(xmlPrivateKey);
                var param = rsa.ExportParameters(true);
                RsaPrivateCrtKeyParameters privateKeyParam = new RsaPrivateCrtKeyParameters(
                    new BigInteger(1, param.Modulus), new BigInteger(1, param.Exponent),
                    new BigInteger(1, param.D), new BigInteger(1, param.P),
                    new BigInteger(1, param.Q), new BigInteger(1, param.DP),
                    new BigInteger(1, param.DQ), new BigInteger(1, param.InverseQ));
                PrivateKeyInfo privateKey = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKeyParam);

                result = Convert.ToBase64String(privateKey.ToAsn1Object().GetEncoded());
            }
            return result;
        }

        /// <summary>
        /// 转换"公钥"格式
        /// </summary>
        /// <param name="xmlPublicKey">xml格式公钥</param>
        /// <returns>base64格式公钥</returns>
        public static string ToBase64PublicKey(string xmlPublicKey)
        {
            var result = string.Empty;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(xmlPublicKey);
                var p = rsa.ExportParameters(false);
                var keyParams = new RsaKeyParameters(
                    false, new BigInteger(1, p.Modulus), new BigInteger(1, p.Exponent));
                var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyParams);
                result = Convert.ToBase64String(publicKeyInfo.ToAsn1Object().GetEncoded());
            }
            return result;
        }

        /// <summary>
        /// 转换"私钥"格式
        /// </summary>
        /// <param name="privateKey">base64格式私钥</param>
        /// <returns>xml格式私钥</returns>
        public static string ToXmlPrivateKey(string privateKey)
        {
            var privateKeyParams = PrivateKeyFactory.CreateKey(Convert.FromBase64String(privateKey)) as RsaPrivateCrtKeyParameters;
            using (var rsa = new RSACryptoServiceProvider())
            {
                var rsaParams = new RSAParameters()
                {
                    Modulus = privateKeyParams?.Modulus.ToByteArrayUnsigned(),
                    Exponent = privateKeyParams?.PublicExponent.ToByteArrayUnsigned(),
                    D = privateKeyParams?.Exponent.ToByteArrayUnsigned(),
                    DP = privateKeyParams?.DP.ToByteArrayUnsigned(),
                    DQ = privateKeyParams?.DQ.ToByteArrayUnsigned(),
                    P = privateKeyParams?.P.ToByteArrayUnsigned(),
                    Q = privateKeyParams?.Q.ToByteArrayUnsigned(),
                    InverseQ = privateKeyParams?.QInv.ToByteArrayUnsigned()
                };
                rsa.ImportParameters(rsaParams);
                return rsa.ToXmlString(true);
            }
        }

        /// <summary>
        /// 转换"公钥"格式
        /// </summary>
        /// <param name="pubilcKey">base64格式公钥</param>
        /// <returns>xml格式公钥</returns>
        public static string ToXmlPublicKey(string pubilcKey)
        {
            var p = PublicKeyFactory.CreateKey(Convert.FromBase64String(pubilcKey)) as RsaKeyParameters;
            using (var rsa = new RSACryptoServiceProvider())
            {
                var rsaParams = new RSAParameters
                {
                    Modulus = p?.Modulus.ToByteArrayUnsigned(),
                    Exponent = p?.Exponent.ToByteArrayUnsigned()
                };
                rsa.ImportParameters(rsaParams);
                return rsa.ToXmlString(false);
            }
        }
    }
}
