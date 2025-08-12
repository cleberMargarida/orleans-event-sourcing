using CustomerSilo;
using Orleans;
using Orleans.Hosting;

// Create the initial web application builder
var builder = WebApplication.CreateBuilder(args);

// Configure Orleans silo with event sourcing capabilities
builder.Services.AddOrleans(silo =>
{
    // Use localhost clustering for development (single node cluster)
    silo.UseLocalhostClustering();

    // Configure log-based consistency provider for event sourcing
    // This enables grains to store events and replay them to rebuild state
    silo.AddLogStorageBasedLogConsistencyProviderAsDefault();

    // Alternative: State-based consistency (commented out)
    // This would store snapshots instead of events
    //silo.AddStateStorageBasedLogConsistencyProviderAsDefault();

    // Configure Redis as the default grain storage
    // Events will be persisted to Redis for durability
    silo.AddRedisGrainStorageAsDefault(configureOptions: redis =>
    {
        redis.ConfigurationOptions = new()
        {
            EndPoints = { "localhost:6379" }, // Redis connection endpoint
        };
    });
});

// Build and start the Orleans application
var app = builder.Build();

app.Logger.LogInformation("Starting Hosting.");

app.Start();

// Demo: Working with an event-sourced customer grain
var customerId = Guid.Parse("d5db8652-0623-45df-b8a4-92b7beb57666");

// Get grain factory to create/retrieve grain instances
var grainFactory = app.Services.GetRequiredService<IGrainFactory>();
// Get the customer grain instance (will be created if doesn't exist)
var customer = grainFactory.GetGrain<ICustomerAccount>(customerId);

// Get initial balance (likely 0 for new customer)
var initial = await customer.GetBalance();
app.Logger.LogInformation("Initial balance: {Balance}", initial);

// Perform business operations that generate events
await customer.Deposit(100);   // Creates a "DepositEvent" 
await customer.Withdrawn(20);  // Creates a "WithdrawalEvent"

// Get final balance after operations
var final = await customer.GetBalance();
app.Logger.LogInformation("Final balance: {Balance}", final);

// === RESTART APPLICATION TO DEMONSTRATE EVENT REPLAY ===
// This simulates application restart to show event sourcing persistence

app.Logger.LogInformation("Restarting Hosting.");

// Gracefully shutdown the current application
await app.StopAsync();
await app.DisposeAsync();

// Create new application builder (simulating restart)
builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrleans(silo =>
{
    silo.UseLocalhostClustering();
    silo.AddLogStorageBasedLogConsistencyProviderAsDefault();
    silo.AddRedisGrainStorageAsDefault(configureOptions: redis =>
    {
        redis.ConfigurationOptions = new()
        {
            EndPoints = { "localhost:6379" },
        };
    });
});

// Start the "restarted" application
app = builder.Build();
app.Start();

// Get grain factory and customer grain again (fresh instances)
grainFactory = app.Services.GetRequiredService<IGrainFactory>();
customer = grainFactory.GetGrain<ICustomerAccount>(customerId);

// Critical demonstration: Get balance after restart
// This will trigger event replay from Redis storage
// The grain will reconstruct its state by replaying stored events:
// 1. DepositEvent(100) 
// 2. WithdrawalEvent(20)
// Final state: Balance = 80
initial = await customer.GetBalance();
app.Logger.LogInformation("Current balance: {Balance}", initial);

// Clean shutdown
await app.StopAsync();