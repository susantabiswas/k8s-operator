using CRD.Controllers;
using k8s;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CRD
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Kubernetes CRD Controller...");
            
            try
            {
                // Load Kubernetes configuration
                var config = KubernetesClientConfiguration.IsInCluster() 
                    ? KubernetesClientConfiguration.InClusterConfig() 
                    : KubernetesClientConfiguration.BuildConfigFromConfigFile();
                
                // Add these lines to set HttpClientHandler options
                config.SkipTlsVerify = true; // Only use during development!

                // Create Kubernetes client
                var client = new Kubernetes(config);
                
                // Create and start the controller
                var controller = new DailyTaskController(client);
                
                // Set up a cancellation token source
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) => 
                {
                    e.Cancel = true;
                    cts.Cancel();
                    Console.WriteLine("Shutting down controller...");
                };
                
                Console.WriteLine("Controller running. Press Ctrl+C to exit.");
                
                // Start the controller
                await controller.StartAsync(cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
