---
page_type: sample
languages:
- yaml
- python
products:
- azure
- azure-devops
- azure-storage
- azure-cognitive-search
extensions:
  services: Containerinstance
name: Load Testing Pipeline with JMeter, ACI and Terraform
description: "Azure Pipeline that provisions JMeter on Azure Container Instance using Terraform for load testing scenarios with Azure Cognitive Search"
urlFragment: "jmeter-aci-terraform"
---

# Load Testing Pipeline with JMeter, ACI and Terraform

This project is a load testing pipeline for Azure Cognitive Search that leverages [Apache JMeter](https://jmeter.apache.org/) as an open source load and performance testing tool and [Terraform](https://www.terraform.io/) to dynamically provision and destroy the required infrastructure on Azure.

Note: This is a fork from [this original repo](https://github.com/Azure-Samples/jmeter-aci-terraform) customized for Azure Cognitive Search (ACS) REST API and syntax. It works with public facing IPs ACS and also with Private Endpoint ACS for private vnet scenarios where no public IP is exposed. 

## Key concepts

![Architecture](./docs/img/architecture.png)

The flow is triggered and controlled by an [Azure Pipeline](https://azure.microsoft.com/en-us/services/devops/pipelines/) on [Azure DevOps](https://azure.microsoft.com/en-in/services/devops/). The pipeline contains a set of tasks that are organized logically in `SETUP`, `TEST`, `RESULTS` and `TEARDOWN` groups.

| Task group              | Tasks  |
|-------------------------|--------|
| SETUP | <li>Check if the JMeter Docker image exists</li><li>Validate the JMX file that contains the JMeter test definition</li><li>Upload JMeter JMX file to Azure Storage Account File Share</li><li>Provision the infrastructure with Terraform</li> |
| TEST | <li>Run JMeter test execution and wait for completion</li> |
| RESULTS | <li>Show JMeter logs</li><li>Get JMeter artifacts (e.g. logs, dashboard)</li><li>Convert JMeter tests result (JTL format) to JUnit format</li><li>Publish JUnit test results to Azure Pipelines</li><li>Publish JMeter artifacts to Azure Pipelines</li> |
| TEARDOWN | <li>Destroy all ephemeral infrastructure with Terraform</li> |

On the `SETUP` phase, JMeter agents are provisioned as [Azure Container Instance (ACI)](https://azure.microsoft.com/en-us/services/container-instances/) using a [custom Docker image](./docker/Dockerfile) on Terraform. Through a [Remote Testing](https://jmeter.apache.org/usermanual/remote-test.html) approach, JMeter controller is responsible to configure all workers, consolidating all results and generating the resulting artifacts (dashboard, logs, etc).

The infrastructure provisioned by Terraform includes:

* Resource Group
* Virtual Network (VNet)
* Storage Account File Share
* 1 JMeter controller on ACI
* N JMeter workers on ACI

On the `RESULTS` phase, a [JMeter Report Dashboard](https://jmeter.apache.org/usermanual/generating-dashboard.html) and [Tests Results](https://docs.microsoft.com/en-us/azure/devops/pipelines/test/review-continuous-test-results-after-build?view=azure-devops) are published in the end of each load testing execution.

## Prerequisites

You should have the following Azure resources:

* [Azure DevOps Organization](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/create-organization?view=azure-devops)
* [Azure Container Registry (ACR)](https://azure.microsoft.com/en-us/services/container-registry/) with admin user enabled ![see screenshot example](./docs/img/container.png)

## A. Getting Started UI Mode

### 1. Create an Azure DevOps project and clone this repo

Enter https://dev.azure.com/your_org, create a new project and clone this repo

![Azure DevOps new project](./docs/img/devops_project.jpg)

In the "Repos" tab, you will get a warning saying that the repo is empty. Just click on "Import a repository", then on the Clone URL copy this url: https://github.com/ignaciofls/jmeter-aci-terraform

![Import this code by cloning the repo](./docs/img/import_repo.jpg)

### 2. Integrating with Azure credentials

Create a Service Connection in Azure Devops (that internally will use a Service Principal) as indicated ![in the Devops documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=azure-devops&tabs=yaml#create-a-service-connection). Keep the Service Connection name as it will be used in next step.

### 3. Creating the Variable Group

Create a Variable Group as indicated ![in the Devops documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/variable-groups?view=azure-devops&tabs=classic#use-a-variable-group) and add the following variables:
* TF_VAR_JMETER_ACR_NAME=$ACR_NAME
* TF_VAR_JMETER_ACR_RESOURCE_GROUP_NAME=$ACR_RESOURCE_GROUP
* TF_VAR_JMETER_DOCKER_IMAGE=$ACR_NAME.azurecr.io/jmeter
* AZURE_SERVICE_CONNECTION_NAME="your_service_connection_name" 
* AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID

### 4. Creating and Running the Docker Pipeline

Create a pipeline with "New Pipeline" (blue button, right side), chose "Azure Repos Git YAML", click on your existing repo (cloned in step 1), configure the pipeline with "Existing Azure Pipelines YAML file", the path of the existing file is "/pipelines/azure-pipelines.docker.yml". 

A couple of extra steps before going to the JMeter deployment pipeline:
- Create two variables:
    *ACR_NAME= myjmeteracr (or something similar)
    *ACR_RESOURCE_GROUP= your_rg_name
- Rename the new pipeline to 'jmeter-docker-build' (in the Pipelines tab, find the three dots inside your pipeline row and there you can rename it)

### 5. Creating the JMeter Pipeline

Replicate the steps as in step #4 but with yaml path='pipelines/azure-pipelines.load-test.yml' and rename to 'jmeter-load-test'.
For this pipeline we will need some extra variables (on top of the two others):
  * API-KEY set it to your key value (and keep it secret in Devops)
  * TF_VAR_JMETER_JMX_FILE=sample.jmx
  * TF_VAR_JMETER_WORKERS_COUNT=1 (or as many as you want for scalability of the Jmeter workers)

### 6. Define the test definition inside your JMX file

By default the test uses [`sample.jmx`](./jmeter/sample.jmx). This JMX file contains a test definition for performing HTTP requests on `your_instance.search.windows.net` endpoint through the `443` port. You can simply update the it with the test definition of your preference.

### 7. Run the JMeter Pipeline

Run the pipeline as in:

![ui-run-pipeline](./docs/img/ui-run-pipeline.png)

## Viewing Test Results

JMeter test results are created in a [JTL](https://cwiki.apache.org/confluence/display/JMETER/JtlFiles) file (`results.jtl`) with CSV formatting. A [Python script](https://github.com/Azure-Samples/jmeter-aci-terraform/blob/main/scripts/jtl_junit_converter.py) was created to convert JTL to [JUnit format](https://llg.cubic.org/docs/junit/) and used during the pipeline to have full integration with Azure DevOps test visualization.

![Azure DevOps with successful requests](./docs/img/azdo-test-results-success.jpg)

Error messages generated by JMeter for failed HTTP requests can also be seen on Azure DevOps.

![Azure DevOps with failed requests](./docs/img/azdo-test-results-fail.jpg)

## Viewing Artifacts

Some artifacts are published after the test ends. Some of them are a static JMeter Dashboard, logs and others.

![pipeline-artifacts](./docs/img/pipeline-artifacts.png)

> You can also download these build artifacts using ![`az pipelines runs artifact download`](https://docs.microsoft.com/en-us/cli/azure/ext/azure-devops/pipelines/runs/artifact?view=azure-cli-latest#ext-azure-devops-az-pipelines-runs-artifact-download).

After downloading the dashboard and unzipping it, open `dashboard/index.html` on your browser. Find an example under ![this path](./docs/loadtestresults_withprivendpoint_1xstandard.zip)

Some screenshots here: 
![jmeter-latencies](./docs/img/latency_example.jpg) and ![jmeter-dashboard](./docs/img/dashboard_example.jpg)

## Pipeline Configuration

All Terraform parameters can be configured using the Variable Group `JMETER_TERRAFORM_SETTINGS`. Please read [JMeter Pipeline Settings](./docs/jmeter-pipeline-settings.md) to know more details about it.

## Limitations

* **Load Test duration**
Please note that for [Microsoft hosted agents](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops#capabilities-and-limitations), you can have pipelines that runs up to 1 hour (private project) or 6 hours (public project). You can have your own agents to bypass this limitation.

* **ACI on VNET regions**
Please note that [not all regions](https://docs.microsoft.com/en-us/azure/container-instances/container-instances-virtual-network-concepts#where-to-deploy) currently support ACI and VNET integration. If you need private JMeter agents, you can deploy it in a different region and use VNET peering between them. Also note that vCPUs and memory limits change based on regions.

## Additional Documentation

* [Implementation Notes](./docs/implementation-notes.md)
* [Adding plugins to JMeter Docker image](./docs/adding-jmeter-plugins.md)
* [JMeter pipeline settings](./docs/jmeter-pipeline-settings.md)
* [Estimating costs](./docs/estimating-costs.md)
* [Integrating with Application Insights](./docs/integrating-application-insights.md)

## External References

* [User Manual: Remote Testing](https://jmeter.apache.org/usermanual/remote-test.html)
* [User Manual: Apache JMeter Distributed Testing Step-by-step](https://jmeter.apache.org/usermanual/jmeter_distributed_testing_step_by_step.html)
* [Azure DevOps CLI reference](https://docs.microsoft.com/en-us/cli/azure/ext/azure-devops/?view=azure-cli-latest)
* [Create your Azure Cognitive Search instance and populate an index with clinical trials docs](https://github.com/cynotebo/KM-Ready-Lab/blob/master/KM-Ready-Lab/workshops/Module%201.md)

## Future enhancements

* Creation of Container Registry with IaC as part of Terraform script
* Fully programmatic (CLI) provisioning and execution

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
