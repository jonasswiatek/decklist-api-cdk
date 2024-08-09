using System;
using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Constructs;

namespace DecklistApiCdk
{
    public class Use1ResourceStack : Stack
    {
        public ICertificate TlsCertificateForCloudFront;

        internal Use1ResourceStack(ResourceStack resourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            TlsCertificateForCloudFront = new Certificate(this, "decklist-lol-tls-cert-use1", new CertificateProps {
                DomainName = resourceStack.DomainName,
                Validation = CertificateValidation.FromDns(resourceStack.decklist_lol_publicHostedZone)
            });
        }
    }
}