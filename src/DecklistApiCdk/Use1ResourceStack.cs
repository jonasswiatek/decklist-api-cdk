using System;
using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Constructs;
using MtgDecklistsCdk;

namespace DecklistApiCdk
{
    public class Use1ResourceStack : Stack
    {
        public ICertificate TlsCertificateForCloudFront;

        internal Use1ResourceStack(ResourceStack resourceStack, Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            TlsCertificateForCloudFront = new Certificate(this, "decklist-tls-cert-use1", new CertificateProps {
                DomainName = Program.DomainName,
                SubjectAlternativeNames = new [] { $"www.{Program.DomainName}" },
                Validation = CertificateValidation.FromDns(resourceStack.decklist_lol_publicHostedZone)
            });
        }
    }
}