using Microsoft.Extensions.DependencyInjection;
using Redis.OM;
using Redis.OM.Contracts;
using Redis.OM.Modeling;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Net.NetworkInformation;

public class Program
{
    private static IConnectionMultiplexer _multiplexer;
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        

        var config = new ConfigurationOptions
        {
            EndPoints = { { "localhost", 6379 } },
            SyncTimeout = 5000
        };

        _multiplexer = ConnectionMultiplexer.Connect(config);

        var db = _multiplexer.GetDatabase();

        var serviceProvider = services.BuildServiceProvider();
        var redisProvider = serviceProvider.GetRequiredService<IRedisConnectionProvider>();
        var redisConnection = serviceProvider.GetRequiredService<IConnectionMultiplexer>();

        // Get Redis collections
        var customerCollection = redisProvider.RedisCollection<Customer>();
        var productCollection = redisProvider.RedisCollection<Product>();
        var orderCollection = redisProvider.RedisCollection<Order>();

        

        // Uncomment the line below if you want to insert the product initially
        //await InsertSampleProduct(productCollection);
        
       // await Program.CreateCustomersWithOrders(serviceProvider, redisProvider);
       //  await RetrieveAllCustomersWithOrders(redisProvider, redisConnection, serviceProvider);
       //cal GetAllCustomers method to fetch all customers
       var stopwatch = Stopwatch.StartNew();
        var allCustomers = await GetAllCustomersAsync(_multiplexer, db);
        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"Fetched all customers. Total count: {allCustomers.Count}");
        Console.WriteLine($"GetAllCustomersAsync took {elapsedMilliseconds} milliseconds.");
        //write the first retrieved customer to the console
        Console.WriteLine(JsonConvert.SerializeObject(allCustomers[0], Formatting.Indented));

        var tasks = new List<Task>();
       
        // Retrieve all customers with their orders and display the time taken
       // await RetrieveAllCustomersWithOrders(redisProvider, redisConnection);
        // Fetch and display all customers
        // var allCustomers = await GetAllCustomersAsync(redisConnection);
        // Console.WriteLine($"Fetched all customers. Total count: {allCustomers.Count}");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IRedisConnectionProvider>(provider => new RedisConnectionProvider("redis://localhost:6379"));
        services.AddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect("localhost:6379"));
    }

    private static async Task RetrieveAllCustomersWithOrders(IRedisConnectionProvider redisProvider, IConnectionMultiplexer multiplexer, IServiceProvider serviceProvider)
    {
        var customerCollection = redisProvider.RedisCollection<Customer>();
        var orderCollection = redisProvider.RedisCollection<Order>();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var server = multiplexer.GetServer(multiplexer.GetEndPoints().First());
        var customerKeys = server.Keys(pattern: "Customer:*").Select(k => k.ToString()).ToList();

        var tasks = customerKeys.Select(async key =>
        {
            var db = multiplexer.GetDatabase();
            var customerJson = await db.ExecuteAsync("JSON.GET", key);
            var customer = customerJson.IsNull ? null : JsonConvert.DeserializeObject<Customer>(customerJson.ToString());
            if (customer == null) return null;
            
           // await Program.CreateOrdersIndex(serviceProvider);
             
           // var orders = orderCollection.Where(o => o.CustomerId == customer.Id).ToList();
                
            return new { Customer = customer };
        }).ToList();

        var customerOrders = await Task.WhenAll(tasks.Where(t => t != null));

        stopwatch.Stop();

        Console.WriteLine($"Retrieved {customerOrders.Length} customers with their orders in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public static async Task<Customer> GetCustomerByIdAsync(IConnectionMultiplexer multiplexer, string customerId)
    {
        var db = multiplexer.GetDatabase();
        var customerJson = await db.StringGetAsync($"Customer:{customerId}");
        return customerJson.IsNullOrEmpty ? null : JsonConvert.DeserializeObject<Customer>(customerJson);
    }

    public static async Task<List<Customer>> GetAllCustomersAsync(IConnectionMultiplexer multiplexer, IDatabase db)
    {
        var server = multiplexer.GetServer(multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: "Customer:*").Select(k => k.ToString()).ToArray();

        var tasks = keys.Select(key => db.ExecuteAsync("JSON.GET", key)).ToArray();

        await Task.WhenAll(tasks);

        var customers = tasks
            .Where(t => !t.Result.IsNull)
            .Select(t => JsonConvert.DeserializeObject<Customer>(t.Result.ToString()))
            .ToList();

        return customers;
    }

    public static async Task CreateCustomersWithOrders(IServiceProvider serviceProvider, IRedisConnectionProvider redisProvider)
    {
            var customerCollection = redisProvider.RedisCollection<Customer>();
            var orderCollection = redisProvider.RedisCollection<Order>();
            var tasks = new List<Task>();

            for (int i = 1; i <= 100; i++)  // Change back to 10000 if needed
            {
                var customer = new Customer
                {
                    Id = $"customer{i}",
                    Name = $"Customer {i}",
                    Email = $"customer{i}@example.com",
                    Address = new Address
                    {
                        Street = $"{i} Main St",
                        City = "Anytown",
                        State = "Anystate",
                        PostalCode = "12345"
                    },
                    Phone = $"123-456-78{i % 100:D2}",
                    RegisteredDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
                };

                tasks.Add(customerCollection.InsertAsync(customer));

                for (int j = 1; j <= 100; j++)
                {
                    var order = new Order
                    {
                        Id = $"order{i}-{j}",
                        CustomerId = customer.Id,
                        OrderDate = DateTime.UtcNow.AddDays(-j),
                        TotalAmount = (i * 100) + j
                    };

                    tasks.Add(orderCollection.InsertAsync(order));
                }
            }

            //call CreateOrdersIndex method to create the index
            await Program.CreateOrdersIndex(serviceProvider);
            await Task.WhenAll(tasks);
            Console.WriteLine("Inserted customers and their orders.");
    }

    
    public static async  Task CreateOrdersIndex(IServiceProvider serviceProvider)
    {
        var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
        //pass the config object to the GetDatabase method

        var db = multiplexer.GetDatabase();

        // Determine whether order-idx index exists
        try
        {
            var indexInfo = db.Execute("FT.INFO", "order-idx");
        }
        catch (RedisServerException ex)
        {
            // If the index does not exist, indexInfo will be null
            if (ex.Message.Contains("Unknown Index name"))
            {
                // Make the code below execute only when order-idx index does not exist
                db.Execute("FT.CREATE", "order-idx", "ON", "hash", "PREFIX", "1", "Order:", "SCHEMA", "CustomerId", "TEXT", "SORTABLE");
            }
        }
        
    }

    public async Task<List<Customer>> GetAllCustomers(IConnectionMultiplexer multiplexer)
    {
        var server = multiplexer.GetServer(multiplexer.GetEndPoints().First());
        var customerKeys = server.Keys(pattern: "Customer:*").ToList();

        var db = multiplexer.GetDatabase();

        var customers = new List<Customer>();

        foreach (var key in customerKeys)
        {
            var customerJson = await db.ExecuteAsync("JSON.GET", key);
            var customer = customerJson.IsNull ? null : JsonConvert.DeserializeObject<Customer>(customerJson.ToString());
            if (customer != null)
            {
                customers.Add(customer);
            }
        }

        return customers;
    }

    // private static async Task InsertSampleProduct(IRedisCollection<Product> productCollection)
    // {
    //     var product = new Product
    //     {
    //         Id = "product1",
    //         Name = "Laptop",
    //         Description = "A high-performance laptop",
    //         Price = 999.99M,
    //         Category = "Electronics",
    //         StockQuantity = 50000
    //     };

    //     await productCollection.InsertAsync(product);
    // }
}
