//      /\
//     /  \    _____   _ _  ___ _
//    / /\ \  |_  / | | | \'__/ _\
//   / ____ \  / /| |_| | | |  __/
//  /_/    \_\/___|\__,_|_|  \___|
//
//
// Welcome to the cool new Azure CLI!
//
// Use `az --version` to display the current version.
// Here are the base commands:
//
//     account             : Manage Azure subscription information.
//     acr                 : Manage private registries with Azure Container Registries.
//     ad                  : Manage Microsoft Entra ID (formerly known as Azure Active Directory, Azure
//                          AD, AAD) entities needed for Azure role-based access control (Azure RBAC)
//                          through Microsoft Graph API.
//     advisor             : Manage Azure Advisor.
//     afd                 : Manage Azure Front Door Standard/Premium.
//     aks                 : Manage Azure Kubernetes Services.
//     ams                 : Manage Azure Media Services resources.
//     apim                : Manage Azure API Management services.
//     appconfig           : Manage App Configurations.
//     appservice          : Manage App Service plans.
//     aro                 : Manage Azure Red Hat OpenShift clusters.
//     backup              : Manage Azure Backups.
//     batch               : Manage Azure Batch.
//     bicep               : Bicep CLI command group.
//     billing             : Manage Azure Billing.
//     bot                 : Manage Microsoft Azure Bot Service.
//     cache               : Commands to manage CLI objects cached using the `--defer` argument.
//     capacity            : Manage capacity.
//     cdn                 : Manage Azure Content Delivery Networks (CDNs).
//     cloud               : Manage registered Azure clouds.
//     cognitiveservices   : Manage Azure Cognitive Services accounts.
//     compute-fleet       : Manage for Azure Compute Fleet.
//     compute-recommender : Manage sku/zone/region recommender info for compute resources.
//     config              : Manage Azure CLI configuration.
//     configure           : Manage Azure CLI configuration. This command is interactive.
//     connection          : Commands to manage Service Connector local connections which allow local
//                          environment to connect Azure Resource. If you want to manage connection for
//                          compute service, please run 'az webapp/containerapp/spring connection'.
//     consumption         : Manage consumption of Azure resources.
//     container           : Manage Azure Container Instances.
//     containerapp        : Manage Azure Container Apps.
//     cosmosdb            : Manage Azure Cosmos DB database accounts.
//     data-boundary       : Data boundary operations.
//     databoxedge         : Manage device with databoxedge.
//     deployment          : Manage Azure Resource Manager template deployment at subscription scope.
//     deployment-scripts  : Manage deployment scripts at subscription or resource group scope.
//     disk                : Manage Azure Managed Disks.
//     disk-access         : Manage disk access resources.
//     disk-encryption-set : Disk Encryption Set resource.
//     dls                 : Manage Data Lake Store accounts and filesystems.
//     dms                 : Manage Azure Data Migration Service (classic) instances.
//     eventgrid           : Manage Azure Event Grid topics, domains, domain topics, system topics
//                          partner topics, event subscriptions, system topic event subscriptions and
//                          partner topic event subscriptions.
//     eventhubs           : Eventhubs.
//     extension           : Manage and update CLI extensions.
//     feature             : Manage resource provider features.
//     feedback            : Send feedback to the Azure CLI Team.
//     find                : I'm an AI robot, my advice is based on our Azure documentation as well as
//                          the usage patterns of Azure CLI and Azure ARM users. Using me improves
//                          Azure products and documentation.
//     functionapp         : Manage function apps. To install the Azure Functions Core tools see
//                          https://github.com/Azure/azure-functions-core-tools.
//     group               : Manage resource groups and template deployments.
//     hdinsight           : Manage HDInsight resources.
//     identity            : Managed Identities.
//     image               : Manage custom virtual machine images.
//     interactive         : Start interactive mode. Installs the Interactive extension if not
//                          installed already.
//     iot                 : Manage Internet of Things (IoT) assets.
//     keyvault            : Manage KeyVault keys, secrets, and certificates.
//     lab                 : Manage azure devtest labs.
//     lock                : Manage Azure locks.
//     logicapp            : Manage logic apps.
//     login               : Log in to Azure.
//     logout              : Log out to remove access to Azure subscriptions.
//     managed-cassandra   : Azure Managed Cassandra.
//     managedapp          : Manage template solutions provided and maintained by Independent Software
//                          Vendors (ISVs).
//     managedservices     : Manage the registration assignments and definitions in Azure.
//     maps                : Manage Azure Maps.
//     mariadb             : Manage Azure Database for MariaDB servers.
//     monitor             : Manage the Azure Monitor Service.
//     mysql               : Manage Azure Database for MySQL servers.
//     netappfiles         : Manage Azure NetApp Files (ANF) Resources.
//     network             : Manage Azure Network resources.
//     policy              : Manage resource policies.
//     postgres            : Manage Azure Database for PostgreSQL servers.
//     ppg                 : Manage Proximity Placement Groups.
//     private-link        : Private-link association CLI command group.
//     provider            : Manage resource providers.
//     redis               : Manage dedicated Redis caches for your Azure applications.
//     relay               : Manage Azure Relay Service namespaces, WCF relays, hybrid connections, and
//                          rules.
//     resource            : Manage Azure resources.
//     resourcemanagement  : Resourcemanagement CLI command group.
//     rest                : Invoke a custom request.
//     restore-point       : Manage restore point with res.
//     role                : Manage Azure role-based access control (Azure RBAC).
//     search              : Manage Azure Search services, admin keys and query keys.
//     security            : Manage your security posture with Microsoft Defender for Cloud.
//     servicebus          : Servicebus.
//     sf                  : Manage and administer Azure Service Fabric clusters.
//     sig                 : Manage shared image gallery.
//     signalr             : Manage Azure SignalR Service.
//     snapshot            : Manage point-in-time copies of managed disks, native blobs, or other
//                          snapshots.
//     sql                 : Manage Azure SQL Databases and Data Warehouses.
//     sshkey              : Manage ssh public key with vm.
//     stack               : A deployment stack is a native Azure resource type that enables you to
//                          perform operations on a resource collection as an atomic unit.
//     staticwebapp        : Manage static apps.
//     storage             : Manage Azure Cloud Storage resources.
//     survey              : Take Azure CLI survey.
//     synapse             : Manage and operate Synapse Workspace, Spark Pool, SQL Pool.
//     tag                 : Tag Management on a resource.
//     term                : Manage marketplace agreement with marketplaceordering.
//     ts                  : Manage template specs at subscription or resource group scope.
//     upgrade             : Upgrade Azure CLI and extensions.
//     version             : Show the versions of Azure CLI modules and extensions in JSON format by
//                          default or format configured by --output.
//     vm                  : Manage Linux or Windows virtual machines.
//     vmss                : Manage groupings of virtual machines in an Azure Virtual Machine Scale Set
//                          (VMSS).
//     webapp              : Manage web apps.

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
                new("account", "Manage Azure subscription information."),
                new("acr", "Manage private registries with Azure Container Registries."),
                new("ad", "Manage Microsoft Entra ID (formerly known as Azure Active Directory, Azure AD, AAD) entities needed for Azure role-based access control (Azure RBAC) through Microsoft Graph API."),
                new("advisor", "Manage Azure Advisor."),
                new("afd", "Manage Azure Front Door Standard/Premium."),
                new("aks", "Manage Azure Kubernetes Services."),
                new("ams", "Manage Azure Media Services resources."),
                new("apim", "Manage Azure API Management services."),
                new("appconfig", "Manage App Configurations."),
                new("appservice", "Manage App Service plans."),
                new("aro", "Manage Azure Red Hat OpenShift clusters."),
                new("backup", "Manage Azure Backups."),
                new("batch", "Manage Azure Batch."),
                new("bicep", "Bicep CLI command group."),
                new("billing", "Manage Azure Billing."),
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
                new("connection", "Commands to manage Service Connector local connections which allow local environment to connect Azure Resource. If you want to manage connection for compute service, please run 'az webapp/containerapp/spring connection'."),
                new("consumption", "Manage consumption of Azure resources."),
                new("container", "Manage Azure Container Instances."),
                new("containerapp", "Manage Azure Container Apps."),
                new("cosmosdb", "Manage Azure Cosmos DB database accounts."),
                new("data-boundary", "Data boundary operations."),
                new("databoxedge", "Manage device with databoxedge."),
                new("deployment", "Manage Azure Resource Manager template deployment at subscription scope."),
                new("deployment-scripts", "Manage deployment scripts at subscription or resource group scope."),
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
                new("find", "I'm an AI robot, my advice is based on our Azure documentation as well as the usage patterns of Azure CLI and Azure ARM users. Using me improves Azure products and documentation."),
                new("functionapp", "Manage function apps. To install the Azure Functions Core tools see https://github.com/Azure/azure-functions-core-tools."),
                new("group", "Manage resource groups and template deployments."),
                new("hdinsight", "Manage HDInsight resources."),
                new("identity", "Managed Identities."),
                new("image", "Manage custom virtual machine images."),
                new("interactive", "Start interactive mode. Installs the Interactive extension if not installed already."),
                new("iot", "Manage Internet of Things (IoT) assets."),
                new("keyvault", "Manage KeyVault keys, secrets, and certificates."),
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
                new("monitor", "Manage the Azure Monitor Service."),
                new("mysql", "Manage Azure Database for MySQL servers."),
                new("netappfiles", "Manage Azure NetApp Files (ANF) Resources."),
                new("network", "Manage Azure Network resources."),
                new("policy", "Manage resource policies."),
                new("postgres", "Manage Azure Database for PostgreSQL servers."),
                new("ppg", "Manage Proximity Placement Groups."),
                new("private-link", "Private-link association CLI command group."),
                new("provider", "Manage resource providers."),
                new("redis", "Manage dedicated Redis caches for your Azure applications."),
                new("relay", "Manage Azure Relay Service namespaces, WCF relays, hybrid connections, and rules."),
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
                new("sql", "Manage Azure SQL Databases and Data Warehouses."),
                new("sshkey", "Manage ssh public key with vm."),
                new("stack", "A deployment stack is a native Azure resource type that enables you to perform operations on a resource collection as an atomic unit."),
                new("staticwebapp", "Manage static apps."),
                new("storage", "Manage Azure Cloud Storage resources."),
                new("survey", "Take Azure CLI survey."),
                new("synapse", "Manage and operate Synapse Workspace, Spark Pool, SQL Pool."),
                new("tag", "Tag Management on a resource."),
                new("term", "Manage marketplace agreement with marketplaceordering."),
                new("ts", "Manage template specs at subscription or resource group scope."),
                new("upgrade", "Upgrade Azure CLI and extensions."),
                new("version", "Show the versions of Azure CLI modules and extensions in JSON format by default or format configured by --output."),
                new("vm", "Manage Linux or Windows virtual machines."),
                new("vmss", "Manage groupings of virtual machines in an Azure Virtual Machine Scale Set (VMSS)."),
                new("webapp", "Manage web apps.")
            ],
            Parameters =
            [
                new("--version", "Display the current version.")
            ]
        };
    }
}
