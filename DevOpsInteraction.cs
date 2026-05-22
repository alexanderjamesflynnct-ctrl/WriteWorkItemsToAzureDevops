using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.TeamFoundation.Core.WebApi; // For ProjectHttpClient
using System.Text.Json;                    // For JSON serialization
using System.IO;                           // For File writing

namespace AzureDevOpsWorkItemReader
{
    public class DevOpsInteraction
    {
        private readonly string _orgUrl;
        private readonly string _personalAccessToken;

        /// <summary>
        /// Initializes a new instance of the DevOpsInteraction class.
        /// </summary>
        /// <param name="orgUrl">The full URL to your DevOps Org (e.g., https://dev.azure.com/MyOrg)</param>
        /// <param name="personalAccessToken">Your Personal Access Token with Work Item Read permissions.</param>
        public DevOpsInteraction(string orgUrl, string personalAccessToken)
        {
            if (string.IsNullOrWhiteSpace(orgUrl)) 
                throw new ArgumentException("Organization URL cannot be empty.", nameof(orgUrl));
            
            if (string.IsNullOrWhiteSpace(personalAccessToken)) 
                throw new ArgumentException("PAT cannot be empty.", nameof(personalAccessToken));

            _orgUrl = orgUrl;
            _personalAccessToken = personalAccessToken;
        }

        /// <summary>
        /// Creates a new work item in the specified project.
        /// </summary>
        /// <param name="projectName">The name of the project.</param>
        /// <param name="workItemType">The type (e.g., "Task", "Bug", "User Story").</param>
        /// <param name="fields">A dictionary of field reference names and their values.</param>
        public async Task<WorkItem> CreateWorkItem(string projectName, string workItemType, Dictionary<string, object?> fields)
        {
            var credentials = new VssBasicCredential(string.Empty, _personalAccessToken);
            using (var connection = new VssConnection(new Uri(_orgUrl), credentials))
            {
                var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

                JsonPatchDocument patchDocument = new JsonPatchDocument();

                foreach (var field in fields)
                {
                    // Skip fields with null keys if any
                    if (string.IsNullOrEmpty(field.Key)) continue;

                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = field.Value // The library accepts object? here
                    });
                }

                return await witClient.CreateWorkItemAsync(patchDocument, projectName, workItemType);
            }
        }
 
        /// <summary>
        /// Helper method to recursively traverse the iteration tree.
        /// </summary>
        private void FlattenIterationNode(WorkItemClassificationNode node, string parentPath, List<object> resultList)
        {
            // Build the full path (e.g., Project\Parent\Child)
            string currentPath = string.IsNullOrEmpty(parentPath) 
                ? node.Name 
                : $"{parentPath}\\{node.Name}";

            resultList.Add(new
            {
                Id = node.Id,
                Name = node.Name,
                Path = currentPath,
                Attributes = node.Attributes // Contains startDate and finishDate if they exist
            });

            // Recurse into children if they exist
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    FlattenIterationNode(child, currentPath, resultList);
                }
            }
        }
    }
}