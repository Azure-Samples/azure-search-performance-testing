// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Search;
using Microsoft.Azure.Management.Search.Models;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SearchPerfTest
{
    
    class ManagementClient
    {
        static SearchManagementClient _managementClient;
        private string clientId = "";
        private string clientSecret = "";
        private string tenantId = "";

        private string resourceGroupName;
        private string searchServiceName;

        public ManagementClient(string resourceGroupName, string searchServiceName, string subscriptionId)
        {
            this.resourceGroupName = resourceGroupName;
            this.searchServiceName = searchServiceName;

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);

            _managementClient = new SearchManagementClient(credentials);
            _managementClient.SubscriptionId = subscriptionId;

        }

        public AdminKeyResult GetKeys()
        {
            return _managementClient.AdminKeys.Get(resourceGroupName, searchServiceName);
        }

        public void ScaleService(int? replicas, int partitions)
        {
            var searchService = _managementClient.Services.Get(resourceGroupName, searchServiceName);

            searchService.ReplicaCount = replicas;
            searchService.PartitionCount = partitions;
            _managementClient.Services.CreateOrUpdateAsync(resourceGroupName, searchServiceName, searchService);
        }

        public string GetStatus()
        {
            var searchService = _managementClient.Services.Get(resourceGroupName, searchServiceName);

            return searchService.Status.ToString();
        }

        public int? GetReplicaCount()
        {
            var searchService = _managementClient.Services.Get(resourceGroupName, searchServiceName);

            return searchService.ReplicaCount;
        }

    }
}
