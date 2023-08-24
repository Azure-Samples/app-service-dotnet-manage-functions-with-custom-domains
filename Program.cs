// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ManageFunctionAppWithDomainSsl
{
    public class Program
    {
        private static readonly string CertificatePassword = Utilities.CreatePassword();

        /**
         * Azure App Service sample for managing function apps.
         *  - app service plan, function app
         *    - Create 2 function apps under the same new app service plan
         *  - domain
         *    - Create a domain
         *  - certificate
         *    - Upload a self-signed wildcard certificate
         *    - update both function apps to use the domain and the created wildcard SSL certificate
         */
        public static async Task RunSample(ArmClient client)
        {
            AzureLocation region  = AzureLocation.EastUS;
            string websiteName     = Utilities.CreateRandomName("website-");
            string planName       = Utilities.CreateRandomName("plan-");
            string app1Name       = Utilities.CreateRandomName("webapp1-");
            string app2Name       = Utilities.CreateRandomName("webapp2-");
            string rgName         = Utilities.CreateRandomName("rgNEMV_");
            string domainName     = Utilities.CreateRandomName("jsdkdemo-") + ".com";
            string certPassword   = Utilities.CreatePassword();
            var lro = await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;

            try {
                //============================================================
                // Create a function app with a new app service plan

                Utilities.Log("Creating function app " + app1Name + "...");

                var webSiteCollection = resourceGroup.GetWebSites();
                var webSiteData = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                    }
                };
                var webSite_lro = await webSiteCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, websiteName, webSiteData);
                var webSite = webSite_lro.Value;

                var planCollection = resourceGroup.GetAppServicePlans();
                var planData = new AppServicePlanData(region)
                {
                };
                var planResource_lro = planCollection.CreateOrUpdate(Azure.WaitUntil.Completed, planName, planData);
                var planResource = planResource_lro.Value;

                SiteFunctionCollection functionAppCollection = webSite.GetSiteFunctions();
                var functionData = new FunctionEnvelopeData()
                {
                };
                var funtion_lro = functionAppCollection.CreateOrUpdate(Azure.WaitUntil.Completed, app1Name, functionData);
                var function = funtion_lro.Value;

                Utilities.Log("Created function app " + function.Data.Name);
                Utilities.Print(function);

                //============================================================
                // Create a second function app with the same app service plan

                Utilities.Log("Creating another function app " + app2Name + "...");
                SiteFunctionCollection function2AppCollection = webSite.GetSiteFunctions();
                var function2Data = new FunctionEnvelopeData()
                {
                };
                var funtion2_lro = functionAppCollection.CreateOrUpdate(WaitUntil.Completed, app2Name, function2Data);
                var function2 = funtion2_lro.Value;

                Utilities.Log("Created function app " + function2.Data.Name);
                Utilities.Print(function);

                //============================================================
                // Purchase a domain (will be canceled for a full refund)

                Utilities.Log("Purchasing a domain " + domainName + "...");

                var domainCollection = resourceGroup.GetAppServiceDomains();
                var domainData = new AppServiceDomainData(region)
                {
                    ContactRegistrant = new RegistrationContactInfo("jondoe@contoso.com", "Jon", "Doe", "4258828080")
                    {
                        AddressMailing = new RegistrationAddressInfo("123 4th Ave", "Redmond", "UnitedStates", "98052", "WA")
                    },
                    IsDomainPrivacyEnabled = true,
                    IsAutoRenew = false
                };
                var domain_lro = domainCollection.CreateOrUpdate(WaitUntil.Completed, domainName, domainData);
                var domain = domain_lro.Value;
                Utilities.Log("Purchased domain " + domain.Data.Name);
                Utilities.Print(domain);

                //============================================================
                // Bind domain to function app 1

                Utilities.Log("Binding http://" + websiteName + "." + domainName + " to app " + websiteName + "...");

                var bindingsCollection = webSite.GetSiteHostNameBindings();
                var bindingsdata = new HostNameBindingData()
                {
                    DomainId = domain.Id,
                    CustomHostNameDnsRecordType = CustomHostNameDnsRecordType.CName,
                };
                var bindings_lro = bindingsCollection.CreateOrUpdate(WaitUntil.Completed, Utilities.CreateRandomName("bindings-"), bindingsdata);
                var bindings = bindings_lro.Value;
                Utilities.Log("Finished binding http://" + websiteName + "." + domainName + " to app " + websiteName);
                Utilities.Print(bindings);

            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    await resourceGroup.DeleteAsync(WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}