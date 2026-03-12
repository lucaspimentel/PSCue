using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions.Azure;

public static class AzCommand
{
    public static Command Create()
    {
        return new Command("az")
        {
            SubCommands =
            [
                // account
                new("account", "Manage Azure subscription information.")
                {
                    SubCommands =
                    [
                        new("lock", "Manage Azure subscription level locks."),
                        new("management-group", "Manage Azure Management Groups."),
                        new("clear", "Clear all subscriptions from the CLI's local cache."),
                        new("get-access-token", "Get a token for utilities to access Azure."),
                        new("list", "Get a list of subscriptions for the logged in account."),
                        new("list-locations", "List supported regions for the current subscription."),
                        new("set", "Set a subscription to be the current active subscription."),
                        new("show", "Get the details of a subscription.")
                    ]
                },

                // acr
                new("acr", "Manage private registries with Azure Container Registries.")
                {
                    SubCommands =
                    [
                        new("agentpool", "Manage private Tasks agent pools with Azure Container Registries."),
                        new("artifact-streaming", "Manage artifact streaming for repositories or supported images."),
                        new("cache", "Manage cache rules in Azure Container Registries."),
                        new("config", "Configure policies for Azure Container Registries."),
                        new("connected-registry", "Manage connected registry resources."),
                        new("credential", "Manage login credentials for Azure Container Registries."),
                        new("credential-set", "Manage credential sets in Azure Container Registries."),
                        new("encryption", "Manage container registry encryption."),
                        new("helm", "Manage helm charts for Azure Container Registries."),
                        new("identity", "Manage service identities for a container registry."),
                        new("manifest", "Manage artifact manifests in Azure Container Registries."),
                        new("network-rule", "Manage network rules for Azure Container Registries."),
                        new("pack", "Manage Azure Container Registry Tasks that use Cloud Native Buildpacks."),
                        new("private-endpoint-connection", "Manage container registry private endpoint connections."),
                        new("private-link-resource", "Manage registry private link resources."),
                        new("replication", "Manage geo-replicated regions of Azure Container Registries."),
                        new("repository", "Manage repositories for Azure Container Registries."),
                        new("scope-map", "Manage scope access maps for Azure Container Registries."),
                        new("task", "Manage steps for building, testing and patching container images."),
                        new("taskrun", "Manage taskruns using Azure Container Registries."),
                        new("token", "Manage tokens for an Azure Container Registry."),
                        new("webhook", "Manage webhooks for Azure Container Registries."),
                        new("build", "Queue a quick build, providing streaming logs."),
                        new("check-health", "Get health information on the environment and optionally a target registry."),
                        new("check-name", "Check if an Azure Container Registry name is valid and available."),
                        new("create", "Create an Azure Container Registry."),
                        new("delete", "Delete an Azure Container Registry."),
                        new("import", "Import an image to an Azure Container Registry from another Container Registry."),
                        new("list", "List all container registries under the current subscription."),
                        new("login", "Log in to an Azure Container Registry through the Docker CLI."),
                        new("run", "Queue a quick run providing streamed logs."),
                        new("show", "Get the details of an Azure Container Registry."),
                        new("show-endpoints", "Display registry endpoints."),
                        new("show-usage", "Get the storage usage for an Azure Container Registry."),
                        new("update", "Update an Azure Container Registry.")
                    ]
                },

                // ad
                new("ad", "Manage Microsoft Entra ID entities needed for Azure RBAC through Microsoft Graph API.")
                {
                    SubCommands =
                    [
                        new("app", "Manage Microsoft Entra applications."),
                        new("group", "Manage Microsoft Entra groups."),
                        new("signed-in-user", "Show graph information about current signed-in user."),
                        new("sp", "Manage Microsoft Entra service principals."),
                        new("user", "Manage Microsoft Entra users.")
                    ]
                },

                new("advisor", "Manage Azure Advisor."),
                new("afd", "Manage Azure Front Door Standard/Premium."),

                // aks
                new("aks", "Azure Kubernetes Service.")
                {
                    SubCommands =
                    [
                        new("approuting", "Commands to manage App Routing addon."),
                        new("command", "See detail usage in 'az aks command invoke', 'az aks command result'."),
                        new("connection", "Commands to manage aks connections."),
                        new("machine", "Get information about machines in a nodepool."),
                        new("maintenanceconfiguration", "Commands to manage maintenance configurations."),
                        new("mesh", "Commands to manage Azure Service Mesh."),
                        new("namespace", "Commands to manage namespace in managed Kubernetes cluster."),
                        new("nodepool", "Commands to manage node pools in Kubernetes cluster."),
                        new("oidc-issuer", "OIDC issuer related commands."),
                        new("safeguards", "Manage Deployment Safeguards."),
                        new("trustedaccess", "Commands to manage trusted access security features."),
                        new("browse", "Show the dashboard for a Kubernetes cluster in a web browser."),
                        new("check-acr", "Validate an ACR is accessible from an AKS cluster."),
                        new("create", "Create a new managed Kubernetes cluster."),
                        new("delete", "Delete a managed Kubernetes cluster."),
                        new("disable-addons", "Disable Kubernetes addons."),
                        new("enable-addons", "Enable Kubernetes addons."),
                        new("get-credentials", "Get access credentials for a managed Kubernetes cluster."),
                        new("get-upgrades", "Get the upgrade versions available for a managed Kubernetes cluster."),
                        new("get-versions", "Get the versions available for creating a managed Kubernetes cluster."),
                        new("install-cli", "Download and install kubectl and kubelogin."),
                        new("list", "List managed Kubernetes clusters."),
                        new("operation-abort", "Abort last running operation on managed cluster."),
                        new("rotate-certs", "Rotate certificates and keys on a managed Kubernetes cluster."),
                        new("scale", "Scale the node pool in a managed Kubernetes cluster."),
                        new("show", "Show the details for a managed Kubernetes cluster."),
                        new("start", "Start a previously stopped Managed Cluster."),
                        new("stop", "Stop a managed cluster."),
                        new("update", "Update a managed Kubernetes cluster."),
                        new("update-credentials", "Update credentials for a managed Kubernetes cluster."),
                        new("upgrade", "Upgrade a managed Kubernetes cluster to a newer version."),
                        new("wait", "Wait for a managed Kubernetes cluster to reach a desired state.")
                    ]
                },

                new("ams", "Manage Azure Media Services resources."),
                new("apim", "Manage Azure API Management services."),
                new("appconfig", "Manage App Configurations."),

                // appservice
                new("appservice", "Manage Appservice.")
                {
                    SubCommands =
                    [
                        new("ase", "Manage App Service Environments."),
                        new("domain", "Manage custom domains."),
                        new("hybrid-connection", "Manage hybrid-connection keys."),
                        new("plan", "Manage App Service Plans."),
                        new("vnet-integration", "List virtual network integrations used in an appservice plan."),
                        new("list-locations", "List regions where a plan sku is available.")
                    ]
                },

                new("aro", "Manage Azure Red Hat OpenShift clusters."),

                // artifacts (Azure DevOps extension)
                new("artifacts", "Manage Azure Artifacts.")
                {
                    SubCommands =
                    [
                        new("universal", "Manage Universal Packages.")
                    ]
                },

                new("backup", "Manage Azure Backups."),
                new("batch", "Manage Azure Batch."),
                new("bicep", "Bicep CLI command group."),
                new("billing", "Manage Azure Billing."),

                // boards (Azure DevOps extension)
                new("boards", "Manage Azure Boards.")
                {
                    SubCommands =
                    [
                        new("area", "Manage area paths."),
                        new("iteration", "Manage iterations."),
                        new("work-item", "Manage work items."),
                        new("query", "Query for a list of work items.")
                    ]
                },

                new("bot", "Manage Microsoft Azure Bot Service."),
                new("cache", "Commands to manage CLI objects cached using the `--defer` argument."),
                new("capacity", "Manage capacity."),
                new("cdn", "Manage Azure Content Delivery Networks (CDNs)."),
                new("cloud", "Manage registered Azure clouds."),
                new("cognitiveservices", "Manage Azure Cognitive Services accounts."),
                new("compute-fleet", "Manage for Azure Compute Fleet."),
                new("compute-recommender", "Manage sku/zone/region recommender info for compute resources."),
                new("config", "Manage Azure CLI configuration."),
                new("configure", "Manage Azure CLI configuration. This command is interactive."),
                new("connection", "Commands to manage Service Connector local connections."),
                new("consumption", "Manage consumption of Azure resources."),

                // container
                new("container", "Manage Azure Container Instances.")
                {
                    SubCommands =
                    [
                        new("container-group-profile", "Manage Azure Container Instance Container Group Profile."),
                        new("attach", "Attach local standard output and error streams to a container."),
                        new("create", "Create a container group."),
                        new("delete", "Delete a container group."),
                        new("exec", "Execute a command from within a running container."),
                        new("export", "Export a container group in yaml format."),
                        new("list", "List container groups."),
                        new("logs", "Examine the logs for a container in a container group."),
                        new("restart", "Restart all containers in a container group."),
                        new("show", "Get the details of a container group."),
                        new("start", "Start all containers in a container group."),
                        new("stop", "Stop all containers in a container group.")
                    ]
                },

                // containerapp
                new("containerapp", "Manage Azure Container Apps.")
                {
                    SubCommands =
                    [
                        new("auth", "Manage containerapp authentication and authorization."),
                        new("compose", "Commands to create Azure Container Apps from Compose specifications."),
                        new("connection", "Commands to manage containerapp connections."),
                        new("dapr", "Commands to manage Dapr."),
                        new("env", "Commands to manage Container Apps environments."),
                        new("github-action", "Commands to manage GitHub Actions."),
                        new("hostname", "Commands to manage hostnames of a container app."),
                        new("identity", "Commands to manage managed identities."),
                        new("ingress", "Commands to manage ingress and traffic-splitting."),
                        new("job", "Commands to manage Container Apps jobs."),
                        new("logs", "Show container app logs."),
                        new("registry", "Commands to manage container registry information."),
                        new("replica", "Manage container app replicas."),
                        new("revision", "Commands to manage revisions."),
                        new("secret", "Commands to manage secrets."),
                        new("ssl", "Upload certificate and bind hostname."),
                        new("browse", "Open a containerapp in the browser."),
                        new("create", "Create a container app."),
                        new("delete", "Delete a container app."),
                        new("exec", "Open an SSH-like interactive shell within a container app replica."),
                        new("list", "List container apps."),
                        new("list-usages", "List usages of subscription level quotas in specific region."),
                        new("show", "Show details of a container app."),
                        new("show-custom-domain-verification-id", "Show the verification id for binding custom domains."),
                        new("up", "Create or update a container app as well as any associated resources."),
                        new("update", "Update a container app.")
                    ]
                },

                // cosmosdb
                new("cosmosdb", "Manage Azure Cosmos DB database accounts.")
                {
                    SubCommands =
                    [
                        new("cassandra", "Manage Cassandra resources of Azure Cosmos DB account."),
                        new("fleet", "Manage Azure Cosmos DB Fleet resources."),
                        new("fleetspace", "Manage Cosmos DB Fleetspace resources."),
                        new("gremlin", "Manage Gremlin resources of Azure Cosmos DB account."),
                        new("identity", "Manage Azure Cosmos DB managed service identities."),
                        new("keys", "Manage Azure Cosmos DB keys."),
                        new("locations", "Manage Azure Cosmos DB location properties."),
                        new("mongodb", "Manage MongoDB resources of Azure Cosmos DB account."),
                        new("network-rule", "Manage Azure Cosmos DB network rules."),
                        new("postgres", "Manage Azure Cosmos DB for PostgreSQL resources."),
                        new("private-endpoint-connection", "Manage Azure Cosmos DB private endpoint connections."),
                        new("private-link-resource", "Manage Azure Cosmos DB private link resources."),
                        new("restorable-database-account", "Manage restorable Azure Cosmos DB accounts."),
                        new("service", "Commands to perform operations on Service."),
                        new("sql", "Manage SQL resources of Azure Cosmos DB account."),
                        new("table", "Manage Table resources of Azure Cosmos DB account."),
                        new("check-name-exists", "Check if an Azure Cosmos DB account name exists."),
                        new("create", "Create a new Azure Cosmos DB database account."),
                        new("delete", "Delete an Azure Cosmos DB database account."),
                        new("failover-priority-change", "Change the failover priority for the Azure Cosmos DB database account."),
                        new("list", "List Azure Cosmos DB database accounts."),
                        new("offline-region", "Offline the specified region for the Azure Cosmos DB database account."),
                        new("restore", "Create a new Azure Cosmos DB database account by restoring from an existing one."),
                        new("show", "Get the details of an Azure Cosmos DB database account."),
                        new("update", "Update an Azure Cosmos DB database account.")
                    ]
                },

                new("data-boundary", "Data boundary operations."),
                new("databoxedge", "Manage device with databoxedge."),
                new("deployment", "Manage Azure Resource Manager template deployment at subscription scope."),
                new("deployment-scripts", "Manage deployment scripts at subscription or resource group scope."),

                // devops
                new("devops", "Manage Azure DevOps organization level operations.")
                {
                    SubCommands =
                    [
                        new("admin", "Manage administration operations."),
                        new("extension", "Manage extensions."),
                        new("project", "Manage team projects."),
                        new("security", "Manage security related operations."),
                        new("service-endpoint", "Manage service endpoints/connections."),
                        new("team", "Manage teams."),
                        new("user", "Manage users."),
                        new("wiki", "Manage wikis."),
                        new("configure", "Configure the Azure DevOps CLI or view your configuration."),
                        new("invoke", "Invoke request for any DevOps area and resource."),
                        new("login", "Set the credential (PAT) to use for a particular organization."),
                        new("logout", "Clear the credential for all or a particular organization.")
                    ]
                },

                new("disk", "Manage Azure Managed Disks."),
                new("disk-access", "Manage disk access resources."),
                new("disk-encryption-set", "Disk Encryption Set resource."),
                new("dls", "Manage Data Lake Store accounts and filesystems."),
                new("dms", "Manage Azure Data Migration Service (classic) instances."),
                new("eventgrid", "Manage Azure Event Grid topics, domains, domain topics, system topics partner topics, event subscriptions, system topic event subscriptions and partner topic event subscriptions."),
                new("eventhubs", "Eventhubs."),
                new("extension", "Manage and update CLI extensions."),
                new("feature", "Manage resource provider features."),
                new("feedback", "Send feedback to the Azure CLI Team."),
                new("find", "AI robot for Azure documentation and usage patterns."),

                // functionapp
                new("functionapp", "Manage function apps.")
                {
                    SubCommands =
                    [
                        new("config", "Configure a function app."),
                        new("connection", "Commands to manage functionapp connections."),
                        new("cors", "Manage Cross-Origin Resource Sharing (CORS)."),
                        new("deployment", "Manage function app deployments."),
                        new("flex-migration", "Manage migration of Linux Consumption function apps to the Flex Consumption plan."),
                        new("function", "Manage function app functions."),
                        new("hybrid-connection", "Manage hybrid-connections from functionapp."),
                        new("identity", "Manage web app's managed identity."),
                        new("keys", "Manage function app keys."),
                        new("log", "Manage function app logs."),
                        new("plan", "Manage App Service Plans for an Azure Function."),
                        new("runtime", "Manage a function app's runtime."),
                        new("scale", "Manage a function app's scale."),
                        new("vnet-integration", "Manage virtual network integrations from a functionapp."),
                        new("create", "Create a function app."),
                        new("delete", "Delete a function app."),
                        new("deploy", "Deploy a provided artifact to Azure functionapp."),
                        new("list", "List function apps."),
                        new("list-consumption-locations", "List available locations for running function apps."),
                        new("list-flexconsumption-locations", "List available locations for function apps on the Flex Consumption plan."),
                        new("list-flexconsumption-runtimes", "List available built-in stacks for function apps on the Flex Consumption plan."),
                        new("list-runtimes", "List available built-in stacks for function apps."),
                        new("restart", "Restart a function app."),
                        new("show", "Get the details of a function app."),
                        new("start", "Start a function app."),
                        new("stop", "Stop a function app."),
                        new("update", "Update a function app.")
                    ]
                },

                // group
                new("group", "Manage resource groups and template deployments.")
                {
                    SubCommands =
                    [
                        new("lock", "Manage Azure resource group locks."),
                        new("create", "Create a new resource group."),
                        new("delete", "Delete a resource group."),
                        new("exists", "Check if a resource group exists."),
                        new("export", "Capture a resource group as a template."),
                        new("list", "List resource groups."),
                        new("show", "Get a resource group."),
                        new("update", "Update a resource group."),
                        new("wait", "Place the CLI in a waiting state until a condition of the resource group is met.")
                    ]
                },

                new("hdinsight", "Manage HDInsight resources."),
                new("identity", "Managed Identities."),
                new("image", "Manage custom virtual machine images."),
                new("interactive", "Start interactive mode."),
                new("iot", "Manage Internet of Things (IoT) assets."),

                // keyvault
                new("keyvault", "Manage KeyVault keys, secrets, and certificates.")
                {
                    SubCommands =
                    [
                        new("backup", "Manage full HSM backup."),
                        new("certificate", "Manage certificates."),
                        new("key", "Manage keys."),
                        new("network-rule", "Manage network ACLs for vault or managed hsm."),
                        new("private-endpoint-connection", "Manage vault/HSM private endpoint connections."),
                        new("private-link-resource", "Manage vault/HSM private link resources."),
                        new("region", "Manage MHSM multi-regions."),
                        new("restore", "Manage full HSM restore."),
                        new("role", "Manage user roles for access control."),
                        new("secret", "Manage secrets."),
                        new("security-domain", "Manage security domain operations."),
                        new("setting", "Manage MHSM settings."),
                        new("check-name", "Check that the given name is valid and is not already in use."),
                        new("create", "Create a Vault or HSM."),
                        new("delete", "Delete a Vault or HSM."),
                        new("delete-policy", "Delete security policy settings for a Key Vault."),
                        new("list", "List Vaults and/or HSMs."),
                        new("list-deleted", "Get information about deleted Vaults or HSMs in a subscription."),
                        new("purge", "Permanently delete the specified Vault or HSM."),
                        new("recover", "Recover a Vault or HSM."),
                        new("set-policy", "Update security policy settings for a Key Vault."),
                        new("show", "Show details of a Vault or HSM."),
                        new("show-deleted", "Show details of a deleted Vault or HSM."),
                        new("update", "Update the properties of a Vault."),
                        new("update-hsm", "Update the properties of a HSM."),
                        new("wait", "Place the CLI in a waiting state until a condition of the Vault is met."),
                        new("wait-hsm", "Place the CLI in a waiting state until a condition of the HSM is met.")
                    ]
                },

                new("lab", "Manage azure devtest labs."),
                new("lock", "Manage Azure locks."),
                new("logicapp", "Manage logic apps."),
                new("login", "Log in to Azure."),
                new("logout", "Log out to remove access to Azure subscriptions."),
                new("managed-cassandra", "Azure Managed Cassandra."),
                new("managedapp", "Manage template solutions provided and maintained by Independent Software Vendors (ISVs)."),
                new("managedservices", "Manage the registration assignments and definitions in Azure."),
                new("maps", "Manage Azure Maps."),
                new("mariadb", "Manage Azure Database for MariaDB servers."),

                // monitor
                new("monitor", "Manage the Azure Monitor Service.")
                {
                    SubCommands =
                    [
                        new("account", "Manage monitor account."),
                        new("action-group", "Manage action groups."),
                        new("activity-log", "Manage activity logs."),
                        new("autoscale", "Manage autoscale settings."),
                        new("dashboard", "Manage Dashboard with Grafana resources."),
                        new("diagnostic-settings", "Manage service diagnostic settings."),
                        new("log-analytics", "Manage Azure log analytics."),
                        new("log-profiles", "Manage log profiles."),
                        new("metrics", "View Azure resource metrics."),
                        new("private-link-scope", "Manage monitor private link scope resource."),
                        new("clone", "Clone metrics alert rules from one resource to another.")
                    ]
                },

                new("mysql", "Manage Azure Database for MySQL servers."),
                new("netappfiles", "Manage Azure NetApp Files (ANF) Resources."),

                // network
                new("network", "Manage Azure Network resources.")
                {
                    SubCommands =
                    [
                        new("application-gateway", "Manage application-level routing and load balancing services."),
                        new("asg", "Manage application security groups (ASGs)."),
                        new("cross-region-lb", "Manage and configure cross-region load balancers."),
                        new("custom-ip", "Manage custom IP."),
                        new("ddos-custom-policy", "Manage Ddos Custom Policy."),
                        new("ddos-protection", "Manage DDoS Protection Plans."),
                        new("dns", "Manage DNS domains in Azure."),
                        new("express-route", "Manage dedicated private network fiber connections to Azure."),
                        new("lb", "Manage and configure load balancers."),
                        new("local-gateway", "Manage local gateways."),
                        new("nat", "Manage NAT resources."),
                        new("network-watcher", "Manage network watcher and its sub-resources."),
                        new("nic", "Manage network interfaces."),
                        new("nsg", "Manage Azure Network Security Groups (NSGs)."),
                        new("private-dns", "Manage Private DNS domains in Azure."),
                        new("private-endpoint", "Manage private endpoints."),
                        new("private-endpoint-connection", "Manage private endpoint connections."),
                        new("private-link-resource", "Manage private link resources."),
                        new("private-link-service", "Manage private link services."),
                        new("profile", "Manage network profiles."),
                        new("public-ip", "Manage public IP addresses."),
                        new("route-filter", "Manage route filters."),
                        new("route-table", "Manage route tables."),
                        new("routeserver", "Manage the route server."),
                        new("security-partner-provider", "Manage Azure security partner provider."),
                        new("service-endpoint", "Manage policies related to service endpoints."),
                        new("traffic-manager", "Manage the routing of incoming traffic."),
                        new("virtual-appliance", "Manage Azure Network Virtual Appliance."),
                        new("virtual-network-appliance", "Manage Virtual Network Appliance."),
                        new("vnet", "Manage virtual networks."),
                        new("vnet-gateway", "Use an Azure Virtual Network Gateway to establish secure, cross-premises connectivity."),
                        new("vpn-connection", "Manage VPN connections."),
                        new("watcher", "Manage the Azure Network Watcher."),
                        new("list-service-aliases", "List available service aliases in the region."),
                        new("list-service-tags", "List all service tags which are below to different resources."),
                        new("list-usages", "List the number of network resources in a region used against a subscription quota.")
                    ]
                },

                // pipelines (Azure DevOps extension)
                new("pipelines", "Manage Azure Pipelines.")
                {
                    SubCommands =
                    [
                        new("agent", "Manage agents."),
                        new("build", "Manage builds."),
                        new("folder", "Manage folders for organizing pipelines."),
                        new("pool", "Manage agent pools."),
                        new("queue", "Manage agent queues."),
                        new("release", "Manage releases."),
                        new("runs", "Manage pipeline runs."),
                        new("variable", "Manage pipeline variables."),
                        new("variable-group", "Manage variable groups."),
                        new("create", "Create a new Azure Pipeline (YAML based)."),
                        new("delete", "Delete a pipeline."),
                        new("list", "List pipelines."),
                        new("run", "Queue (run) a pipeline."),
                        new("show", "Get the details of a pipeline."),
                        new("update", "Update a pipeline.")
                    ]
                },

                new("policy", "Manage resource policies."),
                new("postgres", "Manage Azure Database for PostgreSQL."),
                new("ppg", "Manage Proximity Placement Groups."),
                new("private-link", "Private-link association CLI command group."),
                new("provider", "Manage resource providers."),
                new("redis", "Manage dedicated Redis caches for your Azure applications."),
                new("relay", "Manage Azure Relay Service namespaces, WCF relays, hybrid connections, and rules."),

                // repos (Azure DevOps extension)
                new("repos", "Manage Azure Repos.")
                {
                    SubCommands =
                    [
                        new("import", "Manage Git repositories import."),
                        new("policy", "Manage branch policy."),
                        new("pr", "Manage pull requests."),
                        new("ref", "Manage Git references."),
                        new("create", "Create a Git repository in a team project."),
                        new("delete", "Delete a Git repository in a team project."),
                        new("list", "List Git repositories of a team project."),
                        new("show", "Get the details of a Git repository."),
                        new("update", "Update the Git repository.")
                    ]
                },

                new("resource", "Manage Azure resources."),
                new("resourcemanagement", "Resourcemanagement CLI command group."),
                new("rest", "Invoke a custom request."),
                new("restore-point", "Manage restore point with res."),
                new("role", "Manage Azure role-based access control (Azure RBAC)."),
                new("search", "Manage Azure Search services, admin keys and query keys."),
                new("security", "Manage your security posture with Microsoft Defender for Cloud."),
                new("servicebus", "Servicebus."),
                new("sf", "Manage and administer Azure Service Fabric clusters."),
                new("sig", "Manage shared image gallery."),
                new("signalr", "Manage Azure SignalR Service."),
                new("snapshot", "Manage point-in-time copies of managed disks, native blobs, or other snapshots."),

                // sql
                new("sql", "Manage Azure SQL Databases and Data Warehouses.")
                {
                    SubCommands =
                    [
                        new("db", "Manage databases."),
                        new("dw", "Manage data warehouses."),
                        new("elastic-pool", "Manage elastic pools."),
                        new("failover-group", "Manage SQL Failover Groups."),
                        new("instance-failover-group", "Manage SQL Instance Failover Groups."),
                        new("instance-pool", "Manage instance pools."),
                        new("mi", "Manage SQL managed instances."),
                        new("midb", "Manage SQL Managed Instance databases."),
                        new("recoverable-midb", "Recoverable managed databases command group."),
                        new("server", "Manage SQL servers."),
                        new("stg", "Manage Server Trust Groups."),
                        new("virtual-cluster", "Manage SQL virtual clusters."),
                        new("vm", "Manage SQL virtual machines."),
                        new("list-usages", "Get all subscription usage metrics in a given location."),
                        new("show-usage", "Get a subscription usage metric.")
                    ]
                },

                new("sshkey", "Manage ssh public key with vm."),
                new("stack", "A deployment stack is a native Azure resource type that enables you to perform operations on a resource collection as an atomic unit."),
                new("staticwebapp", "Manage static apps."),

                // storage
                new("storage", "Manage Azure Cloud Storage resources.")
                {
                    SubCommands =
                    [
                        new("account", "Manage storage accounts."),
                        new("blob", "Manage object storage for unstructured data (blobs)."),
                        new("container", "Manage blob storage containers."),
                        new("container-rm", "Manage Azure containers using the Microsoft.Storage resource provider."),
                        new("cors", "Manage storage service Cross-Origin Resource Sharing (CORS)."),
                        new("directory", "Manage file storage directories."),
                        new("entity", "Manage table storage entities."),
                        new("file", "Manage file shares."),
                        new("fs", "Manage file systems in Azure Data Lake Storage Gen2 account."),
                        new("logging", "Manage storage service logging information."),
                        new("message", "Manage queue storage messages."),
                        new("metrics", "Manage storage service metrics."),
                        new("queue", "Manage storage queues."),
                        new("share", "Manage file shares."),
                        new("share-rm", "Manage Azure file shares using the Microsoft.Storage resource provider."),
                        new("sku", "Manage Sku."),
                        new("table", "Manage NoSQL key-value storage."),
                        new("copy", "Copy files or directories to or from Azure storage."),
                        new("remove", "Delete blobs or files from Azure Storage.")
                    ]
                },

                new("survey", "Take Azure CLI survey."),
                new("synapse", "Manage and operate Synapse Workspace, Spark Pool, SQL Pool."),
                new("tag", "Tag Management on a resource."),
                new("term", "Manage marketplace agreement with marketplaceordering."),
                new("ts", "Manage template specs at subscription or resource group scope."),
                new("upgrade", "Upgrade Azure CLI and extensions."),
                new("version", "Show the versions of Azure CLI modules and extensions in JSON format by default or format configured by --output."),

                // vm
                new("vm", "Manage Linux or Windows virtual machines.")
                {
                    SubCommands =
                    [
                        new("application", "Manage applications for VM."),
                        new("availability-set", "Group resources into availability sets."),
                        new("boot-diagnostics", "Troubleshoot the startup of an Azure Virtual Machine."),
                        new("diagnostics", "Configure the Azure Virtual Machine diagnostics extension."),
                        new("disk", "Manage the managed data disks attached to a VM."),
                        new("encryption", "Manage encryption of VM disks."),
                        new("extension", "Manage extensions on VMs."),
                        new("host", "Manage Dedicated Hosts for Virtual Machines."),
                        new("identity", "Manage service identities of a VM."),
                        new("image", "Information on available virtual machine images."),
                        new("monitor", "Manage monitor aspect for a vm."),
                        new("nic", "Manage network interfaces. See also `az network nic`."),
                        new("run-command", "Manage run commands on a Virtual Machine."),
                        new("secret", "Manage VM secrets."),
                        new("unmanaged-disk", "Manage the unmanaged data disks attached to a VM."),
                        new("user", "Manage user accounts for a VM."),
                        new("assess-patches", "Assess patches on a VM."),
                        new("auto-shutdown", "Manage auto-shutdown for VM."),
                        new("capture", "Capture information for a stopped VM."),
                        new("convert", "Convert a VM with unmanaged disks to use managed disks."),
                        new("create", "Create an Azure Virtual Machine."),
                        new("deallocate", "Deallocate a VM so that computing resources are no longer allocated."),
                        new("delete", "Delete a virtual machine."),
                        new("generalize", "Mark a VM as generalized, allowing it to be imaged for multiple deployments."),
                        new("get-instance-view", "Get instance information about a VM."),
                        new("install-patches", "Install patches on a VM."),
                        new("list", "List details of Virtual Machines."),
                        new("list-ip-addresses", "List IP addresses associated with a VM."),
                        new("list-sizes", "List available sizes for VMs."),
                        new("list-skus", "Get details for compute-related resource SKUs."),
                        new("list-usage", "List available usage resources for VMs."),
                        new("list-vm-resize-options", "List available resizing options for VMs."),
                        new("open-port", "Open a VM to inbound traffic on specified ports."),
                        new("perform-maintenance", "Perform maintenance on a virtual machine."),
                        new("reapply", "Reapply VMs."),
                        new("redeploy", "Redeploy an existing VM."),
                        new("reimage", "Reimage (upgrade the operating system) a virtual machine."),
                        new("resize", "Update a VM's size."),
                        new("restart", "Restart VMs."),
                        new("show", "Get the details of a VM."),
                        new("simulate-eviction", "Simulate the eviction of a Spot VM."),
                        new("start", "Start a stopped VM."),
                        new("stop", "Power off (stop) a running VM."),
                        new("update", "Update the properties of a VM."),
                        new("wait", "Place the CLI in a waiting state until a condition of the VM is met.")
                    ]
                },

                new("vmss", "Manage groupings of virtual machines in an Azure Virtual Machine Scale Set (VMSS)."),

                // webapp
                new("webapp", "Manage web apps.")
                {
                    SubCommands =
                    [
                        new("auth", "Manage webapp authentication and authorization."),
                        new("config", "Configure a web app."),
                        new("connection", "Commands to manage webapp connections."),
                        new("cors", "Manage Cross-Origin Resource Sharing (CORS)."),
                        new("deleted", "Manage deleted web apps."),
                        new("deployment", "Manage web app deployments."),
                        new("hybrid-connection", "Manage hybrid-connections from webapps."),
                        new("identity", "Manage web app's managed identity."),
                        new("log", "Manage web app logs."),
                        new("sitecontainers", "Manage linux web apps sitecontainers."),
                        new("traffic-routing", "Manage traffic routing for web apps."),
                        new("vnet-integration", "Manage virtual network integrations from a webapp."),
                        new("webjob", "Manage webjobs on a web app."),
                        new("browse", "Open a web app in a browser."),
                        new("create", "Create a web app."),
                        new("create-remote-connection", "Create a remote connection using a tcp tunnel to your web app."),
                        new("delete", "Delete a web app."),
                        new("deploy", "Deploy a provided artifact to Azure Web Apps."),
                        new("list", "List web apps."),
                        new("list-instances", "List all scaled out instances of a web app or web app slot."),
                        new("list-runtimes", "List available built-in stacks for web apps."),
                        new("restart", "Restart a web app."),
                        new("show", "Get the details of a web app."),
                        new("ssh", "SSH into the web container."),
                        new("start", "Start a web app."),
                        new("stop", "Stop a web app."),
                        new("up", "Create a webapp and deploy code from a local workspace."),
                        new("update", "Update an existing web app.")
                    ]
                }
            ],
            Parameters =
            [
                new("--debug", "Increase logging verbosity to show all debug logs."),
                new("--help", "Show help message and exit.") { Alias = "-h" },
                new("--only-show-errors", "Only show errors, suppressing warnings."),
                new("--output", "Output format.") { Alias = "-o" },
                new("--query", "JMESPath query string."),
                new("--subscription", "Name or ID of subscription."),
                new("--verbose", "Increase logging verbosity."),
                new("--version", "Display the current version.")
            ]
        };
    }
}
