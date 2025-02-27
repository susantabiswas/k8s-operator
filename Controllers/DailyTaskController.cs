using CRD.Models;
using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CRD.Controllers
{
    public class DailyTaskController
    {
        private readonly Kubernetes _client;
        private readonly string _namespace;
        private readonly string _group = "example.com"; // Changed to match CRD yaml
        private readonly string _version = "v1";
        private readonly string _plural = "dailytasks";

        public DailyTaskController(Kubernetes client, string @namespace = "default")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _namespace = @namespace;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Starting DailyTask controller...");

            // Set up a watcher for our custom resource
            string resourceVersion = "";

            // Create the JSON options with our converter for both serialization and deserialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters =
                {
                    new EnumMemberJsonConverter()
                }
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // List existing resources to get resource version
                    var customObjects = await _client.CustomObjects.ListNamespacedCustomObjectAsync(
                        _group, _version, _namespace, _plural,
                        resourceVersion: resourceVersion,
                        cancellationToken: cancellationToken);

                    if (customObjects != null)
                    {
                        // Extract the resource version from the JSON response
                        if (customObjects is JsonElement jsonElement &&
                            jsonElement.TryGetProperty("metadata", out JsonElement metadata) &&
                            metadata.TryGetProperty("resourceVersion", out JsonElement resVersion))
                        {
                            resourceVersion = resVersion.GetString();
                            Console.WriteLine($"Using resource version: {resourceVersion}");
                        }

                        // Process any existing resources
                        if (customObjects is JsonElement rootElement &&
                            rootElement.TryGetProperty("items", out JsonElement items))
                        {
                            var itemsList = items.EnumerateArray();
                            foreach (var item in itemsList)
                            {
                                // Use our custom JSON options for deserializing
                                var dailyTask = JsonSerializer.Deserialize<V1DailyTask>(item.GetRawText(), jsonOptions);
                                if (dailyTask != null)
                                {
                                    await UpdateStatus(dailyTask);
                                }
                            }
                        }
                    }

                    // Watch for changes
                    using var watchResponse = await _client.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        _group, _version, _namespace, _plural,
                        watch: true,
                        resourceVersion: resourceVersion,
                        cancellationToken: cancellationToken);

                    // Create a custom handler to use our deserialization options
                    using var watchStream = watchResponse.Watch<object, object>(
                        onEvent: (type, itemObj) =>
                        {
                            try
                            {
                                // Convert the raw object to JSON first
                                string json = JsonSerializer.Serialize(itemObj);

                                // Then use our custom options to deserialize
                                var item = JsonSerializer.Deserialize<V1DailyTask>(json, jsonOptions);

                                Console.WriteLine($"Received {type} event for DailyTask: {item?.Metadata?.Name}");

                                if (item?.Metadata?.ResourceVersion != null)
                                {
                                    resourceVersion = item.Metadata.ResourceVersion;
                                }

                                if (type == WatchEventType.Added || type == WatchEventType.Modified)
                                {
                                    Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} : , event: {type}");
                                    Task.Run(async () => await UpdateStatus(item), cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing watch event: {ex.Message}");
                            }
                        },
                        onClosed: () => Console.WriteLine("Watch connection closed"),
                        onError: e => Console.WriteLine($"Watch error: {e.Message}")
                    );

                    await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken); // Keep connection open
                }
                catch (Exception ex) when (!(ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
                {
                    Console.WriteLine($"Error in controller: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    await Task.Delay(5000, cancellationToken); // Wait before retrying
                }
            }
        }

        private async Task UpdateStatus(V1DailyTask dailyTask)
        {
            try
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} : Updating status for DailyTask: {dailyTask.Metadata.Name}");
                Console.WriteLine($"Using group: {_group}, version: {_version}, namespace: {dailyTask.Metadata.NamespaceProperty ?? _namespace}, plural: {_plural}");

                // Create the updated status using the proper classes
                var newStatus = new V1DailyTaskStatus
                {
                    Today = new DayInfo
                    {
                        Time = DateTime.Now.ToString("HH:mm:ss"),
                        Day = DateTime.Now.DayOfWeek.ToString(),
                        rating = DayInfo.Rating.Good
                    }
                };

                try
                {
                    // First check if the resource actually exists
                    var existingObject = await _client.CustomObjects.GetNamespacedCustomObjectAsync(
                        _group, 
                        _version,
                        dailyTask.Metadata.NamespaceProperty ?? _namespace,
                        _plural,
                        dailyTask.Metadata.Name);
                    
                    Console.WriteLine("Resource exists, proceeding with status update");
                }
                catch (Exception checkEx)
                {
                    Console.WriteLine($"Error checking resource: {checkEx.Message}");
                    return;
                }

                // Use JsonSerializer to convert to JSON with custom options for enum conversion
                var options = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Converters = 
                    { 
                        // Use EnumMemberConverter instead of JsonStringEnumConverter
                        new EnumMemberJsonConverter()
                    }
                };
                
                // Create a patch body with our status object
                var patchBody = new 
                { 
                    status = newStatus 
                };

                // Apply the patch using JSON merge patch
                var patchJson = JsonSerializer.Serialize(patchBody, options);
                Console.WriteLine($"Patch JSON: {patchJson}");
                
                try
                {
                    var result = await _client.CustomObjects.PatchNamespacedCustomObjectStatusAsync(
                        new V1Patch(patchJson, V1Patch.PatchType.MergePatch),
                        _group,
                        _version,
                        dailyTask.Metadata.NamespaceProperty ?? _namespace,
                        _plural,
                        dailyTask.Metadata.Name);
                    
                    Console.WriteLine($"Status update successful!");
                }
                catch (Exception patchEx)
                {
                    Console.WriteLine($"Error patching status: {patchEx.Message}");
                    
                    // Try using regular patch if status patch fails
                    Console.WriteLine("Trying regular patch as fallback...");
                    try
                    {
                        var result = await _client.CustomObjects.PatchNamespacedCustomObjectAsync(
                            new V1Patch(patchJson, V1Patch.PatchType.MergePatch),
                            _group,
                            _version,
                            dailyTask.Metadata.NamespaceProperty ?? _namespace,
                            _plural,
                            dailyTask.Metadata.Name);
                            
                        Console.WriteLine($"Regular patch successful!");
                    }
                    catch (Exception regularPatchEx)
                    {
                        Console.WriteLine($"Regular patch also failed: {regularPatchEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating status: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
