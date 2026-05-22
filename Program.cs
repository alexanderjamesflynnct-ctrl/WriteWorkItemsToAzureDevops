using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AzureDevOpsWorkItemReader;
using Microsoft.Extensions.Configuration; // Added for Configuration
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AzureDevOpsReader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Build the configuration
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2. Extract values with fallbacks to avoid null warnings
            // Use section:key syntax
            string orgUrl = config["AzureDevOps:OrgUrl"] ?? string.Empty;
            string personalAccessToken = config["AzureDevOps:PAT"] ?? string.Empty;
            string project = config["AzureDevOps:Project"] ?? string.Empty;

            // Simple validation to ensure config loaded
            if (string.IsNullOrEmpty(orgUrl) || string.IsNullOrEmpty(personalAccessToken))
            {
                Console.WriteLine("Error: Configuration values missing in appsettings.json");
                return;
            }

            var devOps = new DevOpsInteraction(orgUrl, personalAccessToken);
            
            try
            {
                // Note: You can also move this path to appsettings.json if desired
                string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "NewWorkItems.json");
                
                if (!File.Exists(jsonPath))
                {
                    Console.WriteLine($"Error: Input file not found at {jsonPath}");
                    return;
                }

                var workItemsToCreate = WorkItemFileHelper.LoadMultipleFieldsFromJson(jsonPath, project);

                Console.WriteLine($"Found {workItemsToCreate.Count} items in JSON. Starting creation...");

                foreach (var fields in workItemsToCreate)
                {
                    string type = "Task"; 
                    if (fields.TryGetValue("System.WorkItemType", out object? typeObj) && typeObj is not null)
                    {
                        type = typeObj.ToString() ?? "Task";
                        fields.Remove("System.WorkItemType"); 
                    }

                    try 
                    {
                        string displayTitle = "Unknown Title";
                        if (fields.TryGetValue("System.Title", out object? titleObj) && titleObj is not null)
                        {
                            displayTitle = titleObj.ToString() ?? "Unknown Title";
                        }

                        Console.Write($"Creating '{displayTitle}'... ");
                        
                        var newItem = await devOps.CreateWorkItem(project, type, fields);
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Success! (ID: {newItem.Id})");
                        Console.ResetColor();
                    }
                    catch (Exception itemEx)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed: {itemEx.Message}");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical Error: {ex.Message}");
            }
        }
    }
}