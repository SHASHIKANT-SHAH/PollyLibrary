using Microsoft.AspNetCore.Mvc;
using Polly;
using RetryPattern.Models;
using RetryPattern.Services;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RetryPattern.Controllers
{
    public class HomeController : Controller
    {
        private readonly RetryServices _retryServices;

        public HomeController(RetryServices myService)
        {
            _retryServices = myService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var data = await _retryServices.GetDataFromApiAsync();
                // Return the view with the data from the API
                return View("Index", data);  // Pass data to the view
            }
            catch (Exception ex)
            {
                // Handle other exceptions (logging, show error page, etc.)
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return View("Error", ex.Message);  // Optionally return an error view
            }
        }

        private int sum(int a, int b)
        {
            return a+b;
        }

        public string CircuitBreaker()
        {
            var circuitBreakerPolicy = Policy
                .Handle<Exception>() // Specify the type of exception to handle (all exceptions in this case)
                .CircuitBreaker(
                    3, // Break after 3 consecutive failures
                    TimeSpan.FromSeconds(3) // Keep the circuit open for 30 seconds before retrying
                );

            //var fallbackPolicy = Policy
            //     .Handle<Exception>()
            //     .Fallback( (cancellationToken) =>
            //     {
            //         // This action will run when the operation fails
            //         Console.WriteLine("Fallback: Operation failed. Returning a default response.");
            //     },
            //     onFallback: async (action) =>
            //      {
            //          // This block runs when a fallback happens
            //          // 'outcome' is the exception that triggered the fallback
            //          // 'context' is the Polly execution context

            //          Console.WriteLine("Fallback triggered due to: " + action.Message);
            //          //Console.WriteLine("Context info: " + context?.PolicyKey); // Log some context info (optional)

            //          await Task.CompletedTask; // Ensure the async method completes
            //      });

            //var policyWrap = Policy.Wrap(circuitBreakerPolicy, fallbackPolicy);

            for (int i = 0; i < 15; i++) // Attempt 5 times to simulate repeated failures
            {
                try
                {
                    Console.WriteLine($"Attempt {i + 1}:");
                    circuitBreakerPolicy.Execute(() =>
                    {
                        Console.WriteLine("Executing operation...");
                        throw new Exception("Something went wrong"); // Simulating failure
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Caught exception: {ex.Message}");
                }

                // Small delay to simulate time between attempts
                System.Threading.Thread.Sleep(1000);

                
            }
            return "data";
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
