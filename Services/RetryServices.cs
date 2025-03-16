using Microsoft.AspNetCore.Http;
using Polly;
using Polly.CircuitBreaker;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading.Tasks;

namespace RetryPattern.Services
{
    public class RetryServices
    {
        private readonly HttpClient _httpClient;

        public RetryServices(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<string> GetDataFromApiAsync()
        {
            // Define a retry policy with an exponential backoff strategy
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(
                    4, // Retry 4 times
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) // Exponential backoff
                );

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(9));  // Open after 3 failures

            var timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromSeconds(10)) as IAsyncPolicy;  // Timeout after 10 seconds

            // Define a fallback policy (for when retries and circuit breaker fail)
            var fallbackPolicy = Policy<string>
                 .Handle<Exception>()
                 .FallbackAsync(
                     fallbackValue: "Fallback response: Unable to fetch data", // Fallback value
                     onFallbackAsync: async (exception, context) =>
                     {
                         // Log the exception if necessary
                         Console.WriteLine("Fallback triggered: " + exception.Exception?.Message);
                         await Task.CompletedTask;
                     });

            var bulkheadPolicy = Policy
                 .BulkheadAsync(10, 5);  // Limit to 10 concurrent calls, 5 queued requests

            var rateLimitPolicy = Policy
                 .RateLimitAsync(10, TimeSpan.FromSeconds(1));  // Allow up to 10 requests per second

            var combinedPolicy = Policy.WrapAsync(retryPolicy, timeoutPolicy);

            if (_httpClient == null) throw new InvalidOperationException("_httpClient is not initialized.");
            if (retryPolicy == null) throw new InvalidOperationException("Retry policy is not initialized.");
            if (circuitBreakerPolicy == null) throw new InvalidOperationException("Circuit breaker policy is not initialized.");
            if (timeoutPolicy == null) throw new InvalidOperationException("Timeout policy is not initialized.");
            if (fallbackPolicy == null) throw new InvalidOperationException("Fallback policy is not initialized.");
            if (combinedPolicy == null) throw new InvalidOperationException("Combined policy is not initialized.");

            var i = 1;

            // Execute the operation with the retry policy
            return await combinedPolicy.ExecuteAsync(async () =>
            {
                Console.WriteLine("Attempt = " + i);

                // Make the HTTP call to the external service
                var response = await _httpClient.GetAsync("https://example.com/api/data");

                i++;

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            });
        }
    }

}

//How It Works:

//When making the API call, Polly first tries to execute it with the retry policy. If the request fails due to a transient issue (e.g., network problem), it retries a few times.
//If the retries don’t succeed or the circuit breaker is triggered (i.e., the API fails too many times), the circuit breaker policy will stop further retries to avoid overwhelming the service.
//If the operation is timed out (e.g., the API takes too long to respond), the timeout policy will trigger, aborting the operation.
//If all of the above policies fail, the fallback policy will be applied, returning a predefined fallback response (e.g., "Unable to fetch data, showing default data").

//Retry on Transient Failures:

//Scenario: Your application needs to make network requests (e.g., to a REST API or a database), and these requests can fail due to temporary network glitches or server unavailability.
//Policy Used: retryPolicy is the one responsible for automatically retrying the failed operation.
//How It Works: The retryPolicy will handle HttpRequestException (or any exception you define), retrying the operation multiple times with exponential backoff (TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))). This ensures that the system doesn't give up on the first failure, which is common in transient issues.


//Circuit Breaker to Prevent Overloading:

//Scenario: You don't want to overload the external service when it is in a failing state (e.g., too many retries in quick succession). A circuit breaker will protect your system by "opening" after a certain number of failures.
//Policy Used: circuitBreakerPolicy is used to implement this behavior.
//How It Works: After 3 consecutive failures, the circuit breaker will "open," meaning it will stop attempting the operation and will return immediately with a fallback or timeout. This gives the external service time to recover and prevents overloading it with requests.


//Timeout to Prevent Hanging:

//Scenario: Network calls or other asynchronous operations might take too long due to various reasons (e.g., slow server response). In this case, you want to prevent your application from hanging indefinitely while waiting for a response.
//Policy Used: timeoutPolicy is used to enforce a maximum time duration for the operation.
//How It Works: If the operation takes longer than the specified timeout (10 seconds in your case), the policy will cancel the operation and throw an exception.

//Fallback on External API Call Failure (Network Glitches or Server Errors)

//Scenario: Your application needs to fetch data from an external REST API. The API might fail due to network glitches, server unavailability, or timeouts.

//Policy Used:

//Fallback Policy: The fallback policy is used to return a predefined response if the operation fails, either due to an exception or another failure scenario (e.g., timeout, retries exhausted).
//How It Works:

//When an HTTP request is made to the external API, if an exception occurs (like a network issue or server error), the fallback policy will be triggered.
//The fallback policy provides a default response ("Fallback response: Unable to fetch data").
//The onFallbackAsync callback can be used to log the error or notify the user, ensuring that the failure doesn't crash the application but gives a graceful response instead.


//5.Bulkhead Isolation
//Description: Bulkhead isolation limits the number of concurrent requests to a particular resource, like a database or an external service. This prevents overwhelming the service by limiting the number of concurrent requests.
//Example Usage: Prevents a spike in traffic from affecting all parts of the system. For example, if you're making external API calls, you might want to limit the number of concurrent requests to prevent overloading the external service.

//6.Rate Limiting
//Description: A rate-limiting policy helps limit the number of requests over a given period of time to avoid overloading an external service. This is useful when you need to respect service-level agreements (SLAs) that impose rate limits.
//Example Usage: Limit API calls to a third-party service to avoid throttling or being blocked.